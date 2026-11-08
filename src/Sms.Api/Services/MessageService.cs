using Microsoft.EntityFrameworkCore;
using Sms.Api.Data;

namespace Sms.Api.Services;

public class MessageService
{
    private readonly AppDbContext _db;
    private readonly ISmsProvider _provider;

    public MessageService(AppDbContext db, ISmsProvider provider)
    {
        _db = db;
        _provider = provider;
    }

    /// <summary>
    /// Creates a message and attempts delivery right away.
    /// Idempotent by key: a repeated request returns the original message and sends nothing.
    /// </summary>
    public async Task<(Message message, bool created)> CreateAndSendAsync(
        string to, string body, string? idempotencyKey, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existing = await _db.Messages
                .FirstOrDefaultAsync(m => m.IdempotencyKey == idempotencyKey, ct);
            if (existing is not null) return (existing, false);
        }

        var message = new Message
        {
            To = to,
            Body = body.Trim(),
            IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey
        };

        _db.Messages.Add(message);
        await _db.SaveChangesAsync(ct);

        await SendAsync(message, ct);
        return (message, true);
    }

    /// <summary>The state machine: only Pending -> Sending -> Sent/Failed is allowed.</summary>
    public async Task SendAsync(Message message, CancellationToken ct)
    {
        if (message.Status != MessageStatus.Pending)
            throw new InvalidOperationException($"Cannot send a message in status {message.Status}.");

        message.Status = MessageStatus.Sending;
        await _db.SaveChangesAsync(ct);

        try
        {
            var result = await _provider.SendAsync(message.To, message.Body, ct);
            message.ProviderName = _provider.Name;

            if (result.Accepted)
            {
                message.Status = MessageStatus.Sent;
                message.ProviderMessageId = result.ProviderMessageId;
                message.SentAt = DateTime.UtcNow;
            }
            else
            {
                message.Status = MessageStatus.Failed;
                message.Error = result.Error;
            }
        }
        catch (Exception ex)
        {
            // A vendor outage must not surface as a 500 to the caller.
            message.Status = MessageStatus.Failed;
            message.Error = ex.Message;
        }

        await _db.SaveChangesAsync(ct);
    }

    public Task<List<Message>> ListAsync(MessageStatus? status, int skip, int take, CancellationToken ct) =>
        _db.Messages
            .Where(m => status == null || m.Status == status)
            .OrderByDescending(m => m.CreatedAt)
            .Skip(skip).Take(take)
            .ToListAsync(ct);
}
