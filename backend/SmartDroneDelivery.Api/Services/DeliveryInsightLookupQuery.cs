using System.Globalization;

namespace SmartDroneDelivery.Api.Services;

public record DeliveryInsightLookupQuery(string? SearchKeyword, string? Status,
    DateTime? FromUtc, DateTime? ToExclusiveUtc, int Page, int PageSize)
{
    private static readonly string[] Statuses =
        ["PENDING", "APPROVED", "READY_FOR_TAKEOFF", "IN_TRANSIT", "ARRIVED", "DELIVERED", "CANCELLED", "FAILED"];

    public static DeliveryInsightLookupQuery Parse(IQueryCollection query)
    {
        var errors = new Dictionary<string, string[]>();
        string[] allowed = ["searchKeyword", "status", "fromDate", "toDate", "page", "pageSize"];
        foreach (var (key, values) in query)
            if (!allowed.Contains(key, StringComparer.Ordinal) || values.Count != 1)
                errors[key] = ["Unknown or repeated query parameter."];

        var keyword = query["searchKeyword"].ToString().Trim();
        if (query["searchKeyword"].ToString().Length > 50)
            errors["searchKeyword"] = ["Search keyword must be at most 50 characters."];
        var status = query["status"].ToString().Trim().ToUpperInvariant();
        if (status.Length > 0 && !Statuses.Contains(status))
            errors["status"] = ["Invalid order status filter."];

        DateOnly? ReadDate(string name)
        {
            if (!query.ContainsKey(name)) return null;
            if (DateOnly.TryParseExact(query[name].ToString(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var date)) return date;
            errors[name] = ["Expected a calendar date in YYYY-MM-DD format."];
            return null;
        }
        var from = ReadDate("fromDate");
        var to = ReadDate("toDate");
        if (from.HasValue && to.HasValue && from.Value > to.Value)
            errors["toDate"] = ["ToDate must not precede FromDate."];
        DateTime? start = null, end = null;
        try { if (from.HasValue) start = DateTime.SpecifyKind(from.Value.ToDateTime(TimeOnly.MinValue).AddHours(-7), DateTimeKind.Utc); }
        catch (ArgumentOutOfRangeException) { errors["fromDate"] = ["Date boundary is outside the supported UTC range."]; }
        try { if (to.HasValue) end = DateTime.SpecifyKind(to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue).AddHours(-7), DateTimeKind.Utc); }
        catch (ArgumentOutOfRangeException) { errors["toDate"] = ["Date boundary is outside the supported UTC range."]; }

        int ReadPage(string name, int fallback, int maximum)
        {
            if (!query.ContainsKey(name)) return fallback;
            if (int.TryParse(query[name], NumberStyles.None, CultureInfo.InvariantCulture, out var value)
                && value >= 1 && value <= maximum) return value;
            errors[name] = [$"Must be an integer between 1 and {maximum}."];
            return fallback;
        }
        var page = ReadPage("page", 1, int.MaxValue);
        var pageSize = ReadPage("pageSize", 20, 100);
        if (errors.Count > 0) throw new ApiException(400, "One or more query parameters are invalid.", errors);
        return new(keyword.Length == 0 ? null : keyword, status.Length == 0 ? null : status, start, end, page, pageSize);
    }
}
