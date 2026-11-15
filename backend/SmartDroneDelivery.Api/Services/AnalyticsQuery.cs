using System.Globalization;
using Microsoft.AspNetCore.Http;
using SmartDroneDelivery.Api.DTOs;

namespace SmartDroneDelivery.Api.Services;

/// <summary>Strict LA-341 query parsing; dates are calendar days in UTC+07:00.</summary>
public record AnalyticsQuery(ReportingPeriod Period, int Page = 1, int PageSize = 20)
{
    public static AnalyticsQuery Parse(IQueryCollection query, bool report)
    {
        var errors = new Dictionary<string, string[]>();
        var allowed = report ? new[] { "fromDate", "toDate", "page", "pageSize" } : new[] { "fromDate", "toDate" };
        foreach (var (key, values) in query)
            if (!allowed.Contains(key, StringComparer.Ordinal) || values.Count != 1)
                errors[key] = ["Unknown or repeated query parameter."];
        var fromOk = DateOnly.TryParseExact(query["fromDate"].ToString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var from);
        var toOk = DateOnly.TryParseExact(query["toDate"].ToString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var to);
        if (!fromOk) errors["fromDate"] = ["Required date in YYYY-MM-DD format."];
        if (!toOk) errors["toDate"] = ["Required date in YYYY-MM-DD format."];
        if (fromOk && toOk && (to < from || to.DayNumber - from.DayNumber >= 366))
            errors["toDate"] = ["Select one to 366 inclusive days, in chronological order."];
        DateTime start = default, end = default;
        if (fromOk && toOk) {
            try {
                start = DateTime.SpecifyKind(from.ToDateTime(TimeOnly.MinValue).AddHours(-7), DateTimeKind.Utc);
                end = DateTime.SpecifyKind(to.AddDays(1).ToDateTime(TimeOnly.MinValue).AddHours(-7), DateTimeKind.Utc);
            } catch (ArgumentOutOfRangeException) { errors["fromDate"] = ["Period boundaries must fit the supported UTC timestamp range."]; }
        }
        int ReadPage(string name, int fallback, int max) {
            if (!query.ContainsKey(name)) return fallback;
            if (int.TryParse(query[name], NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value >= 1 && value <= max) return value;
            errors[name] = [$"Must be an integer between 1 and {max}."]; return fallback;
        }
        var page = report ? ReadPage("page", 1, int.MaxValue) : 1;
        var pageSize = report ? ReadPage("pageSize", 20, 100) : 20;
        if (errors.Count > 0) throw new ApiException(400, "One or more query parameters are invalid.", errors);
        return new(new(from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), "Asia/Ho_Chi_Minh", start, end), page, pageSize);
    }
}
