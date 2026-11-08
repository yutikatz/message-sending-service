namespace Sms.Api.Data;

public static class SeedData
{
    public static void EnsureSeeded(AppDbContext db)
    {
        if (db.Employees.Any()) return;

        db.Employees.AddRange(
            new Employee
            {
                FirstName = "יוסי", LastName = "כהן", Address = "הרצל 5, בני ברק",
                Instances = new() { new() { PhoneNumber = "+972501234567" },
                                    new() { PhoneNumber = "+972529876543" } }
            },
            new Employee
            {
                FirstName = "שרה", LastName = "לוי", Address = "רשב\"י 12, ירושלים",
                Instances = new() { new() { PhoneNumber = "+972521112233" } }
            },
            new Employee
            {
                // Shares a phone with the previous employee on purpose.
                FirstName = "אברהם", LastName = "מזרחי", Address = "הרב קוק 3, פתח תקווה",
                Instances = new() { new() { PhoneNumber = "+972521112233" } }
            },
            new Employee
            {
                FirstName = "רחל", LastName = "בן דוד", Address = "ויצמן 8, רחובות",
                Instances = new() { new() { PhoneNumber = "+972544445555" } }
            });

        db.SaveChanges();
    }
}
