using Sms.Api.Data;
using Sms.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Sms.Api.Endpoints;

public record SendMessageRequest(string? To, string? Body, int? EmployeeInstanceId, DateTime? ScheduledFor);

public static class MessageEndpoints
{
    public static void MapMessageEndpoints(this WebApplication app)
    {
        var messages = app.MapGroup("/api/messages").WithTags("Messages");

        // POST /api/messages -> { id, status }
        messages.MapPost("/", async (SendMessageRequest req, HttpRequest http,
            MessageService service, AppDbContext db, CancellationToken ct) =>
        {
            string to;

            // employeeInstanceId is preferred: it ties the message to a real employee.
            // A raw phone is accepted only when it already belongs to an instance.
            if (req.EmployeeInstanceId is int instanceId)
            {
                var instance = await db.EmployeeInstances.FindAsync(new object[] { instanceId }, ct);
                if (instance is null)
                    return ValidationError("employeeInstanceId", "Unknown employee instance.");
                to = instance.PhoneNumber;
            }
            else
            {
                var normalized = PhoneValidator.NormalizePhone(req.To);
                if (normalized is null)
                    return ValidationError("to", "Phone must be E.164, e.g. +972501234567.");

                var known = await db.EmployeeInstances.AnyAsync(i => i.PhoneNumber == normalized, ct);
                if (!known)
                    return ValidationError("to", "Phone does not belong to any employee instance.");
                to = normalized;
            }

            if (!PhoneValidator.IsValidBody(req.Body))
                return ValidationError("body", $"Body is required, max {PhoneValidator.MaxBodyLength} chars.");

            PhoneValidator.NormalizeSchedule(req.ScheduledFor, DateTime.UtcNow, out var scheduleError);
            if (scheduleError is not null) return ValidationError("scheduledFor", scheduleError);

            var key = http.Headers["Idempotency-Key"].FirstOrDefault();
            var (message, created) = await service.CreateAndSendAsync(to, req.Body!, key, ct);

            return Results.Json(new { id = message.Id, status = message.Status.ToString() },
                statusCode: created ? StatusCodes.Status201Created : StatusCodes.Status200OK);
        });

        // GET /api/messages/{id}
        messages.MapGet("/{id:int}", async (int id, AppDbContext db, CancellationToken ct) =>
        {
            var message = await db.Messages.FindAsync(new object[] { id }, ct);
            return message is null ? Results.NotFound() : Results.Ok(message);
        });

        // GET /api/messages?status=Sent&page=1&pageSize=20
        messages.MapGet("/", async (AppDbContext db, MessageService service, CancellationToken ct,
            MessageStatus? status, int page = 1, int pageSize = 20) =>
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var items = await service.ListAsync(status, (page - 1) * pageSize, pageSize, ct);
            var total = await db.Messages.CountAsync(m => status == null || m.Status == status, ct);

            return Results.Ok(new { page, pageSize, total, items });
        });

        // GET /api/employees - feeds the Angular form.
        app.MapGet("/api/employees", async (AppDbContext db, CancellationToken ct) =>
            Results.Ok(await db.Employees.Include(e => e.Instances)
                .Select(e => new
                {
                    e.Id,
                    e.FirstName,
                    e.LastName,
                    e.Address,
                    instances = e.Instances.Select(i => new { i.Id, i.PhoneNumber })
                })
                .ToListAsync(ct))).WithTags("Employees");
    }

    private static IResult ValidationError(string field, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [field] = new[] { message } });
}
