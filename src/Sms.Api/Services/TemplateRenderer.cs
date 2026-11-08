using System.Text.RegularExpressions;

namespace Sms.Api.Services;

/// <summary>
/// Replaces {{field}} placeholders with employee values. An unknown placeholder is a
/// validation error - never send "{{firstName}}" to a real person.
/// </summary>
public static class TemplateRenderer
{
    private static readonly Regex Placeholder = new(@"\{\{\s*([A-Za-z]+)\s*\}\}", RegexOptions.Compiled);

    public static bool TryRender(string template, Dictionary<string, string> values,
        out string rendered, out string? error)
    {
        error = null;
        rendered = template;

        foreach (Match m in Placeholder.Matches(template))
        {
            var key = m.Groups[1].Value;
            if (!values.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
            {
                error = $"Unknown placeholder '{key}'.";
                return false;
            }
            rendered = rendered.Replace(m.Value, value);
        }

        return true;
    }
}
