using Sms.Api.Services;

namespace Sms.Tests;

/// <summary>Fake vendor: no I/O, scripted outcomes, call counter.</summary>
public class FakeSmsProvider : ISmsProvider
{
    public string Name => "fake";
    public bool RejectNext { get; set; }
    public bool ThrowNext { get; set; }
    public int Calls { get; private set; }

    public bool CanHandle(string to, string body) => true;

    public Task<SmsSendResult> SendAsync(string to, string body, CancellationToken ct = default)
    {
        Calls++;
        if (ThrowNext) throw new HttpRequestException("provider timeout");
        return Task.FromResult(RejectNext
            ? new SmsSendResult(false, null, "rejected")
            : new SmsSendResult(true, "prov-1", null));
    }

    public Task<string?> GetStatusAsync(string providerMessageId, CancellationToken ct = default)
        => Task.FromResult<string?>(null);
}
