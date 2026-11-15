namespace SmartDroneDelivery.Api.DTOs;

public record ReportingPeriod(string FromDate, string ToDate, string TimeZone, DateTime StartUtc, DateTime EndExclusiveUtc, string CohortField = "createdAt");
public record AnalyticsStatusCount(string Status, int Count);
public record AnalyticsSuccessRate(int DeliveredCount, int FailedCount, int EligibleCount, decimal? Percent);
public record AnalyticsAverageDelivery(decimal? Minutes, int SampleCount, int ExcludedCount);
public record AnalyticsStationUsage(Guid StationId, string StationCode, string StationName, int OriginOrderCount, int DestinationOrderCount, int TotalDistinctOrders);
public record DashboardResult(ReportingPeriod Period, string MetricDefinition, DateTime AsOf, DateTime GeneratedAt, int TotalOrders,
    IReadOnlyList<AnalyticsStatusCount> OrdersByStatus, AnalyticsSuccessRate SuccessRate, AnalyticsAverageDelivery AverageDelivery,
    string StationUsageMetric, IReadOnlyList<AnalyticsStationUsage> StationUsage);
public record ReportStation(Guid Id, string StationCode, string Name);
public record DeliveryReportRow(Guid OrderId, string TrackingNumber, string Status, ReportStation OriginStation, ReportStation DestinationStation,
    DateTime CreatedAt, DateTime? ActualDepartureTime, DateTime? ActualDeliveryTime, decimal? DurationMinutes);
public record DeliveryReport(ReportingPeriod Period, string MetricDefinition, DateTime AsOf, IReadOnlyList<DeliveryReportRow> Items,
    int Page, int PageSize, int TotalCount, int TotalPages);
