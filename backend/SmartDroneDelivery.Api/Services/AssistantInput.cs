using System.Text;
using System.Text.RegularExpressions;

namespace SmartDroneDelivery.Api.Services;

public static class AssistantInput
{
    private static readonly Regex Dates = new(@"\b\d{4}-\d{2}-\d{2}(?:[ T]\d{2}:\d{2}(?::\d{2})?(?:\.\d+)?(?:Z| UTC)?)?\b",
        RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private static readonly Regex Sensitive = new(
        @"password|passwd|bearer\s|api[ _-]?key|secret|token|pickup[ -]?(secure[ -]?)?code|\botp\b|mật khẩu|mã nhận|mã xác nhận|địa\s*chỉ|số\s*nhà|người\s*nhận(?!\s+(?:đã|có|nhận|sẽ|chưa)\b)|\brecipient(?!\s+(?:received|has|have|got|confirmed|already|yet|receive)\b)|điện\s*thoại|\b(address|contact|phone|email|street|road|avenue|name)\b|https?://|www\.|(?:\d[\s().-]*){6,}|[\w.+-]+@[\w.-]+\.[a-z]{2,}",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    public const string Hidden = "[Message hidden because it may contain sensitive information.]";
    public static bool IsSensitive(string text)
    {
        try { return Sensitive.IsMatch(Dates.Replace(text, "[date]")); }
        catch (RegexMatchTimeoutException) { return true; }
    }
    public static string Validate(string? text)
    {
        var trimmed = text?.Trim() ?? "";
        if (trimmed.EnumerateRunes().Count() is < 1 or > 2000)
            throw new ApiException(400, "Message must contain between 1 and 2000 Unicode characters after trimming.");
        return trimmed;
    }
    public static string SafeHistory(string text) => text.EnumerateRunes().Take(4001).Count() > 4000 || IsSensitive(text) ? Hidden : text;
}
