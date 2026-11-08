using System.Text.RegularExpressions;

namespace Sms.Api.Services;

public static class PhoneValidator
{
    public const int MaxBodyLength = 640; // 4 concatenated SMS parts

    // E.164: "+" then 8-15 digits, first digit 1-9. Strict on purpose.
    private static readonly Regex E164 = new(@"^\+[1-9]\d{7,14}$", RegexOptions.Compiled);

    public static bool IsValidPhone(string? phone) => phone is not null && E164.IsMatch(phone);

    /// <summary>Normalises local Israeli input (050-1234567) to E.164; null if unfixable.</summary>
    public static string? NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;

        var p = phone.Replace("-", "").Replace(" ", "").Trim();

        if (p.StartsWith('0') && p.Length is 9 or 10) p = "+972" + p[1..];
        else if (!p.StartsWith('+')) return null;

        return IsValidPhone(p) ? p : null;
    }

    public static bool IsValidBody(string? body) =>
        !string.IsNullOrWhiteSpace(body) && body.Length <= MaxBodyLength;

    /// <summary>Scheduling is optional; accepted only in the next 30 days.</summary>
    public static DateTime? NormalizeSchedule(DateTime? scheduledFor, DateTime now, out string? error)
    {
        error = null;
        if (scheduledFor is null) return null;

        var utc = scheduledFor.Value.Kind == DateTimeKind.Utc
            ? scheduledFor.Value
            : scheduledFor.Value.ToUniversalTime();

        if (utc <= now) { error = "scheduledFor must be in the future."; return null; }
        if (utc > now.AddDays(30)) { error = "scheduledFor must be within 30 days."; return null; }
        return utc;
    }
}
