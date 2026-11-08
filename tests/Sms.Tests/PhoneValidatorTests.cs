using Sms.Api.Services;
using Xunit;

namespace Sms.Tests;

public class PhoneValidatorTests
{
    [Theory]
    [InlineData("+972501234567")]
    [InlineData("0501234567")]
    [InlineData("050-123-4567")]
    public void Normalize_returns_e164_for_valid_input(string input)
        => Assert.Equal("+972501234567", PhoneValidator.NormalizePhone(input));

    [Theory]
    [InlineData("12345")]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("972501234567")]
    public void Normalize_returns_null_for_invalid_input(string? input)
        => Assert.Null(PhoneValidator.NormalizePhone(input));

    [Fact]
    public void Body_length_boundaries()
    {
        Assert.False(PhoneValidator.IsValidBody("   "));
        Assert.True(PhoneValidator.IsValidBody(new string('a', 640)));
        Assert.False(PhoneValidator.IsValidBody(new string('a', 641)));
    }

    [Fact]
    public void Schedule_in_the_past_is_rejected()
    {
        var now = new DateTime(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc);
        PhoneValidator.NormalizeSchedule(now.AddMinutes(-1), now, out var error);
        Assert.NotNull(error);
    }
}
