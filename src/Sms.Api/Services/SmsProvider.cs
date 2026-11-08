namespace Sms.Api.Services;

public record SmsSendResult(bool Accepted, string? ProviderMessageId, string? Error);

/// <summary>
/// The vendor seam. Adding a second vendor means adding an implementation and a
/// routing rule - it never touches MessageService or the API contract.
/// </summary>
public interface ISmsProvider
{
    string Name { get; }
    bool CanHandle(string to, string body);
    Task<SmsSendResult> SendAsync(string to, string body, CancellationToken ct = default);
    Task<string?> GetStatusAsync(string providerMessageId, CancellationToken ct = default);
}

/// <summary>
/// Stands in for the real vendor. No network calls, so it is safe in unit tests.
/// A body containing "FAIL" is rejected deterministically to keep failure paths testable.
/// </summary>
public class MockSmsProvider : ISmsProvider
{
    public string Name => "mock";

    public bool CanHandle(string to, string body) => true;

    public Task<SmsSendResult> SendAsync(string to, string body, CancellationToken ct = default)
    {
        if (body.Contains("FAIL", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(new SmsSendResult(false, null, "provider_rejected"));

        return Task.FromResult(new SmsSendResult(true, Guid.NewGuid().ToString("N"), null));
    }

    public Task<string?> GetStatusAsync(string providerMessageId, CancellationToken ct = default)
        => Task.FromResult<string?>("delivered");
}
