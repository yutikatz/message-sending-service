# SPEC — SMS Service

האפיון שממנו נבנה הפרויקט. כל סעיף מנומק.

---

## 1. היקף

שליחת SMS לעובדים דרך ספק מדומה; מעקב סטטוס; שמירה ב-DB; בדיקות; ממשק; דוקר; CI.
מחוץ להיקף: אימות, הרשאות, ריבוי ספקים בפועל, deploy.

---

## 2. מודל נתונים

**Employee** — `Id`, `FirstName`, `LastName`, `Address`.
אילוץ: `UNIQUE(FirstName, LastName, Address)` — מממש "הם ייחודיים" ברמת ה-DB.

**EmployeeInstance** — `Id`, `EmployeeId` (FK, CASCADE), `PhoneNumber`.
`INDEX` לא ייחודי על `PhoneNumber`: אותו מספר יכול לשמש כמה עובדים.

**Message** — `Id`, `To`, `Body`, `Status`, `IdempotencyKey` (UNIQUE), `ProviderName`,
`ProviderMessageId`, `Error`, `CreatedAt`, `SentAt`.
`INDEX(Status, CreatedAt)` לשאילתת הסינון.

### נימוקים
- `To` כ-snapshot: ההיסטוריה לא משתנה כשמספר הטלפון משתנה.
- `Sending` נפרד: מזהה הודעה תקועה לעומת הודעה שלא נשלחה.
- `ProviderName`: ביקורת + בסיס לספק שני.
- `IdempotencyKey` UNIQUE: הגנה תחת race, לא רק בקוד.

---

## 3. API

### POST /api/messages
```json
{ "employeeInstanceId": 1, "body": "..." }
// או
{ "to": "+972501234567", "body": "..." }
```
Header: `Idempotency-Key` (אופציונלי).

`201 { id, status }` נוצר · `200 { id, status }` חוזר · `400` ולידציה.

### GET /api/messages/{id}
`200 Message` · `404`.

### GET /api/messages?status=Sent&page=1&pageSize=20
`{ page, pageSize, total, items[] }`; `pageSize` מוגבל ל-1..100.

### GET /api/employees
עובדים עם מופעיהם.

---

## 4. ולידציה

| שדה | חוק |
|---|---|
| `to` | E.164 `^\+[1-9]\d{7,14}$` |
| `to` | נורמליזציה: `0XXXXXXXXX` → `+972…`, הסרת `-` ורווחים |
| `to` | חייב להיות קיים ב-`EmployeeInstances` |
| `body` | לא ריק, ≤ 640 תווים |
| `employeeInstanceId` | קיים |
| `scheduledFor` | עתידי, ≤ 30 יום |

הנחה: קהל ישראלי; מספר בלי `+` שאינו מתחיל ב-0 → שגיאה, לא ניחוש.

---

## 5. מכונת מצבים

```
Pending ──► Sending ──► Sent
                  └───► Failed
Delivered — מה-callback של ספק; לא ממומש (ספק מדומה)
```

`SendAsync` זורק `InvalidOperationException` אם `Status != Pending` — אי-אפשר לשלוח פעמיים.
כל חריגת ספק נתפסת, נרשמת ב-`Error`, והבקשה לא מחזירה 500.

---

## 6. ספק

```csharp
public interface ISmsProvider {
    string Name { get; }
    bool CanHandle(string to, string body);
    Task<SmsSendResult> SendAsync(string to, string body, CancellationToken ct = default);
    Task<string?> GetStatusAsync(string providerMessageId, CancellationToken ct = default);
}
```

`MockSmsProvider` — ללא רשת; גוף המכיל `FAIL` נדחה דטרמיניסטית לבדיקת מסלולי כשל.

---

## 7. Idempotency

1. בקשה עם `Idempotency-Key`.
2. חיפוש ב-`Messages`; קיים → מוחזר (`200`).
3. אחרת נוצר ונשלח; ה-Unique Index אוכף גם תחת race.

---

## 8. בדיקות (קריטריוני קבלה)

| # | תרחיש | תוצאה |
|---|---|---|
| T1 | שליחה תקינה | `Sent`, `SentAt` מוגדר |
| T2 | ספק דוחה | `Failed`, `Error` |
| T3 | ספק זורק | `Failed`, `Error` מכיל הודעה |
| T4 | אותו Key פעמיים | הודעה אחת, `Calls == 1` |
| T5 | שליחה חוזרת של הודעה שאינה Pending | `InvalidOperationException` |
| T6 | סינון סטטוס | רק הסטטוס המבוקש |
| T7 | נורמליזציה | `+972501234567` |
| T8 | קלט פסול | `null` |
| T9 | placeholder לא מוכר | שגיאה |

---

## 9. תשתית

**Docker:** `docker-compose.yml` — `web` (nginx + Angular), `api` (.NET), `sqlserver` עם
`healthcheck` ו-`depends_on: condition: service_healthy`.— SQL Server.

**CI/CD:** `restore → build → test (TRX + coverage) → frontend build → publish → docker build`.
`publish` ו-`push` רק על branch ראשי. GitLab ו-GitHub.

---

## 10. הנחות יסוד

1. טלפונים ב-E.164; קלט מקומי נתפס כישראלי.
2. גוף ≤ 640 תווי UTF-16 (4 SMS).
3. סטטוסים: Pending/Sending/Sent/Delivered/Failed; receipts אמיתיים מחוץ להיקף.
4. `to` גולמי מתקבל רק אם הוא קיים במערכת.
5. אין אימות משתמשים — לפי הדרישה.
6. SQLite כברירת מחדל; SQL Server בקונפיגורציה אחת.
7. ספק מדומה אינו מחזיר callback — `Delivered` אינו מושג אוטומטית.
