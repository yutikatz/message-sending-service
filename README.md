# שירות שליחת SMS — Full Stack (ASP.NET Core 8 + Angular 18)

שירות לשליחת SMS לעובדים: API, ממשק ניהול, מסד נתונים, בדיקות אוטומטיות ו-CI/CD.
הרצה מלאה בפקודה אחת באמצעות Docker.

**דרישות מקדימות:** .NET 8 SDK · Node 20+ · Docker Desktop (אופציונלי — ניתן להריץ מקומית)

---

## 1. הרצה

### Docker (מומלץ — פקודה אחת)

```bash
docker compose up --build
```

| שירות | כתובת | תיאור |
|---|---|---|
| Web (Angular + nginx) | http://localhost:4200 | טופס שליחה וטבלת הודעות |
| API (.NET 8) | http://localhost:5080 | REST + Swagger על `/swagger` |
| SQL Server 2022 | `localhost:1433` | פנימי, נשמר ב-volume |

ה-API ממתין ל-healthcheck של SQL Server, כך שאין תלות בסדר העלייה.

**סיסמת SQL Server** מוגדרת כברירת מחדל ב-`docker-compose.yml`. לסביבה שאינה מקומית
אפשר לדרוס אותה בלי לגעת בקוד:

```bash
# Windows PowerShell
$env:SA_PASSWORD="MyStrongPassword"; docker compose up --build

# Linux / macOS
SA_PASSWORD=MyStrongPassword docker compose up --build
```

המשתנה `SA_PASSWORD` נקרא ב-`docker-compose.yml` (`${SA_PASSWORD:-Dev_password123!}`)
ופעם אחת בלבד — אין סודות בקוד ואין צורך בקובץ `.env` להרצה מקומית.

### מקומית (ללא Docker)

```bash
# טרמינל 1 — שרת API. יוצר ומאכלס את מסד הנתונים באתחול (SQLite)
dotnet run --project src/Sms.Api --urls http://localhost:5080

# טרמינל 2 — ממשק. ה-proxy מפנה /api ל-localhost:5080
cd web
npm install
npm start                                 # http://localhost:4200
```

### בדיקות

```bash
dotnet test
```

הבדיקות רצות מול מסד בזיכרון וספק מדומה — ללא רשת, ללא Docker, ללא מסד אמיתי.

### נתוני דוגמה

המסד נוצר ומאוכלס אוטומטית באתחול (`Data/SeedData.cs`): 4 עובדים, אחד עם שני מספרי טלפון,
ושניים החולקים אותו מספר — כדי להדגים שהטלפון אינו ייחודי. אין צורך בסקריפטים.

---

## 2. מה נבנה

```
src/Sms.Api/
  Program.cs              הרכבה, DI, אתחול DB + seed
  Data/                   Entities, AppDbContext, SeedData
  Services/
    SmsProvider.cs        ISmsProvider + MockSmsProvider
    MessageService.cs     מכונת מצבים + idempotency
    Validators.cs         ולידציית טלפון, גוף, תזמון
    TemplateRenderer.cs   {{placeholders}}
  Endpoints/MessageEndpoints.cs
tests/Sms.Tests/          13 בדיקות xUnit
web/                      Angular 18 standalone
  src/app/message-api.service.ts   כל התקשורת ל-API
  src/app/app.component.ts         טופס + טבלה + סינון
docker-compose.yml        web + api + sqlserver
.github/workflows/ci.yml
```

### API

| Method | Route | תיאור |
|---|---|---|
| POST | `/api/messages` | `{ to }` או `{ employeeInstanceId }` + `body` → `201 { id, status }` |
| GET | `/api/messages/{id}` | הודעה בודדת, `404` אם לא קיימת |
| GET | `/api/messages?status=&page=&pageSize=` | רשימה מסוננת ומדופדפת |
| GET | `/api/employees` | עובדים עם מופעיהם (לטופס) |

`Idempotency-Key` ב-header — בקשה חוזרת מחזירה `200` עם אותה הודעה, לא `201`.

```bash
curl -X POST http://localhost:5080/api/messages \
  -H "Content-Type: application/json" \
  -d '{"employeeInstanceId":1,"body":"הודעת בדיקה"}'
```

---

## 3. החלטות הנתונים — ונימוקן

הדרישה: אין מספר עובד; שם פרטי + שם משפחה + כתובת ייחודיים יחד; לעובד כמה מופעים עם טלפונים שונים; הטלפון אינו ייחודי.

```
Employee (FirstName, LastName, Address)    ← UNIQUE על שלושתם
   └── EmployeeInstance (PhoneNumber)      ← INDEX לא ייחודי
          └── Message (To, Body, Status)   ← To הוא snapshot
```

1. **המפתח העסקי של העובד** הוא השלישייה, עם `IsUnique()` ב-`OnModelCreating`. כך הייחודיות נאכפת ע"י ה-DB ולא ע"י האפליקציה.
2. **`Message.To` נשמר כמחרוזת ולא כ-FK בלבד.** מספר טלפון של עובד מתחלף; מה נשלח ולאן — לא. הודעה חייבת להישאר עם המספר שאליו נשלחה בפועל.
3. **`POST` מעדיף `employeeInstanceId`.** שליחה לטלפון חופשי מנותקת מהעובד ואין לה הגנה מפני מספר אקראי; לכן `to` גולמי מתקבל **רק** אם הוא קיים ב-`EmployeeInstances` (`AnyAsync` ב-`MessageEndpoints`).
4. **`Sending` הוא סטטוס מפורש** ולא בוליאני — בלעדיו "תקוע באמצע" נראה כמו "בדרך".
5. **`ProviderName` נשמר על ההודעה** — ביקורת על מי שלח, והכנה לספק שני.

---

## 4. ולידציה

| שדה | כלל |
|---|---|
| טלפון | E.164 `^\+[1-9]\d{7,14}$`; `050-1234567` מנורמל ל-`+972501234567` |
| טלפון | חייב להשתייך למופע עובד קיים |
| גוף | לא ריק, עד 640 תווים |
| `scheduledFor` | עתידי, עד 30 יום |
| `employeeInstanceId` | קיים |

---

## 5. בדיקות

| קובץ | מה מוכח |
|---|---|
| `MessageServiceTests` | `Pending→Sent`; דחיית ספק → `Failed` עם סיבה; חריגת ספק נתפסת ולא מפילה בקשה; idempotency (שיחה אחת לספק); איסור שליחה כפולה של הודעה שאינה `Pending`; סינון סטטוס |
| `PhoneValidatorTests` | פורמט מקומי/בינלאומי, גבולות אורך גוף, תזמון בעבר |
| `TemplateRendererTests` | החלפה תקינה; כשל רועש על placeholder לא מוכר |

כל הבדיקות רצות מול `FakeSmsProvider` — ללא רשת, ללא DB, שניות בודדות ב-CI.

---

## 6. CI/CD

`.github/workflows/ci.yml`:

```
restore → build → test (+coverage, TRX) → frontend build → docker build
```

- `dotnet test` עם `--collect:"XPlat Code Coverage"` ו-report של TRX.
- בנייה של שני ה-images בכל PR כדי לתפוס שבירת Dockerfile לפני merge.
- `publish` ו-`docker push` רק על ה-branch הראשי.

---

## 7. מה בחרתי **לא** לעשות, ולמה

| לא נעשה | נימוק |
|---|---|
| אימות והרשאות | "אין צורך באימות משתמשים" — במפורש. הוספה ללא דרישה = קוד ללא כיסוי. |
| תור אמיתי (Rabbit/Kafka) | ההיקף סינכרוני. `SendAsync(Message)` הוא כבר היחידה שה-worker העתידי יקרא, ולכן המעבר הוא שינוי שליטה ולא כתיבה מחדש. |
| Retry אוטומטי | מסוכן בלי הבחנה 4xx/5xx. היום כל כשל נרשם ב-`Error` — נקודת הפתיחה ל-retry מושכל. |
| EF Migrations | `EnsureCreated` + seed מספקים את דרישת "פקודה אחת". בפרויקט מתמשך — Migrations. |
| Delivery receipts | לא נדרש; `Delivered` מוגדר, אך נקבע רק מול callback של ספק אמיתי. |
| בדיקות UI (Karma) | ה-UI אופציונלי ולא בציון; הוספת בדיקות Angular כאן הייתה שורפת זמן על החלק הלא-נמדד. |
| קובץ `.env` | הסיסמה היחידה בפרויקט מגיעה ממשתנה סביבה עם ברירת מחדל ב-compose. `.env` היה מוסיף קובץ סודות להסתרה בלי שום רווח. `.gitignore` חוסם אותו למקרה שייווצר. |

## 8. מה הייתי מוסיף בעוד זמן

1. Worker + queue לשליחה א-סינכרונית, ו-reaper להודעות תקועות ב-`Sending`.
2. Webhook לסטטוסי מסירה אמיתיים.
3. Benchmark תפוקה מול הספק.
4. בדיקות אינטגרציה עם `WebApplicationFactory`.

---

## 9. שימוש ב-AI — הצהרה

| היכן | מה |
|---|---|
| Scaffolding | מבנה Solution, `csproj`, `angular.json`, `tsconfig`, Dockerfiles |
| בדיקות | ניסוח ראשוני של מקרי בדיקה; התאמה ללוגיקה בפועל ידנית |
| תיעוד | README, SPEC ומסמך חלק ב' — נכתבו בסיוע ועברו עריכה |

**לא נוצר ב-AI:** מודל הנתונים (הפרדת `Employee`/`EmployeeInstance`, שמירת `To` כ-snapshot),
מכונת המצבים, העדפת `employeeInstanceId` על פני טלפון גולמי, ומדיניות ה-idempotency.
אלו החלטות שהתקבלו מתוך הדרישה ומשיקולי מקרי קצה. כל שורה בקוד נקראה ומובנת.

---

## 10. מסמכי הגשה

| קובץ | תוכן |
|---|---|
| `README.md` | מסמך זה — מה נבנה, החלטות, הרצה, מגבלות |
| `SPEC.md` | אפיון מלא: סכימה, API, ולידציה, קריטריוני קבלה, הנחות יסוד |

---

## 11. זמן השקעה בפועל

**≈ 3.5 שעות.**