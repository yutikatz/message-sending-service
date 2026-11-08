using Sms.Api.Services;
using Xunit;

namespace Sms.Tests;

public class TemplateRendererTests
{
    [Fact]
    public void Replaces_known_placeholders()
    {
        var ok = TemplateRenderer.TryRender(
            "שלום {{firstName}} {{lastName}}",
            new() { ["firstName"] = "יוסי", ["lastName"] = "כהן" },
            out var rendered, out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.Equal("שלום יוסי כהן", rendered);
    }

    [Fact]
    public void Unknown_placeholder_fails_loudly()
    {
        var ok = TemplateRenderer.TryRender("שלום {{nick}}", new(), out _, out var error);

        Assert.False(ok);
        Assert.Contains("nick", error);
    }
}
