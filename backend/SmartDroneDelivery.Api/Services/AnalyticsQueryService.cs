using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Security;

namespace SmartDroneDelivery.Api.Services;

/// <summary>Read-only LA-341 created-date/current-status reporting. No operational side effects.</summary>
public class AnalyticsQueryService(AppDbContext db, TimeProvider clock)
{
    private const string Definition = "created-current-v1";
    private static readonly string[] Statuses = ["PENDING", "APPROVED", "READY_FOR_TAKEOFF", "IN_TRANSIT", "ARRIVED", "DELIVERED", "CANCELLED", "FAILED"];

    public async Task<DashboardResult> DashboardAsync(Guid caller, AnalyticsQuery query, CancellationToken ct = default)
    {
        await using var tx = await SnapshotAsync(ct);
        await EnsureManagerAsync(caller, ct);
        var asOf = clock.GetUtcNow().UtcDateTime;
        var orders = Cohort(query);
        var counts = await orders.GroupBy(o => o.Status).Select(g => new { Status = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Status, x => x.Count, ct);
        var statusRows = Statuses.Select(s => new AnalyticsStatusCount(s, counts.GetValueOrDefault(s))).ToList();
        var delivered = counts.GetValueOrDefault("DELIVERED"); var failed = counts.GetValueOrDefault("FAILED");
        var eligible = delivered + failed;
        var durations = await orders.Where(o => o.Status == "DELIVERED" && o.ActualDepartureTime != null && o.ActualDeliveryTime != null && o.ActualDeliveryTime >= o.ActualDepartureTime)
            .GroupBy(o => 1).Select(g => new { Count = g.Count(), Minutes = g.Average(o => (o.ActualDeliveryTime!.Value - o.ActualDepartureTime!.Value).TotalMinutes) }).SingleOrDefaultAsync(ct);
        // Aggregate each direction in SQL; only bounded per-station aggregates are combined in memory.
        var origins = await orders.GroupBy(o => o.OriginStationId).Select(g => new { Id = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Id, x => x.Count, ct);
        var destinations = await orders.GroupBy(o => o.DestinationStationId).Select(g => new { Id = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Id, x => x.Count, ct);
        var sameStation = await orders.Where(o => o.OriginStationId == o.DestinationStationId).GroupBy(o => o.OriginStationId)
            .Select(g => new { Id = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Id, x => x.Count, ct);
        var ids = origins.Keys.Union(destinations.Keys).ToArray();
        var stations = await db.LandingStations.AsNoTracking().Where(s => ids.Contains(s.Id)).OrderBy(s => s.StationCode).ThenBy(s => s.Id)
            .Select(s => new { s.Id, s.StationCode, s.Name }).ToListAsync(ct);
        var usage = stations.Select(s => new AnalyticsStationUsage(s.Id,s.StationCode,s.Name,origins.GetValueOrDefault(s.Id),destinations.GetValueOrDefault(s.Id),origins.GetValueOrDefault(s.Id)+destinations.GetValueOrDefault(s.Id)-sameStation.GetValueOrDefault(s.Id))).ToList();
        var result = new DashboardResult(query.Period,Definition,asOf,clock.GetUtcNow().UtcDateTime,statusRows.Sum(x => x.Count),statusRows,
            new(delivered,failed,eligible,eligible == 0 ? null : decimal.Round(100m*delivered/eligible,2,MidpointRounding.AwayFromZero)),
            new(durations == null ? null : decimal.Round((decimal)durations.Minutes,2,MidpointRounding.AwayFromZero),durations?.Count ?? 0,delivered-(durations?.Count ?? 0)),"THROUGHPUT",usage);
        if (tx != null) await tx.CommitAsync(ct);
        return result;
    }

    public async Task<DeliveryReport> ReportAsync(Guid caller, AnalyticsQuery query, CancellationToken ct = default)
    {
        await using var tx = await SnapshotAsync(ct);
        await EnsureManagerAsync(caller, ct);
        var asOf = clock.GetUtcNow().UtcDateTime;
        var orders = Cohort(query);
        var count = await orders.CountAsync(ct);
        var offset = ((long)query.Page-1)*query.PageSize;
        var rows = offset >= count ? [] : await orders.OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.Id).Skip((int)offset).Take(query.PageSize)
            .Select(o => new { o.Id,o.TrackingNumber,o.Status,Origin = new ReportStation(o.OriginStation.Id,o.OriginStation.StationCode,o.OriginStation.Name),Destination = new ReportStation(o.DestinationStation.Id,o.DestinationStation.StationCode,o.DestinationStation.Name),o.CreatedAt,o.ActualDepartureTime,o.ActualDeliveryTime }).ToListAsync(ct);
        var items = rows.Select(o => new DeliveryReportRow(o.Id,o.TrackingNumber,o.Status,o.Origin,o.Destination,Utc(o.CreatedAt),Utc(o.ActualDepartureTime),Utc(o.ActualDeliveryTime),
            o.Status == "DELIVERED" && o.ActualDepartureTime is {} departure && o.ActualDeliveryTime is {} delivery && delivery >= departure
                ? decimal.Round((decimal)(delivery-departure).TotalMinutes,2,MidpointRounding.AwayFromZero) : null)).ToList();
        if (tx != null) await tx.CommitAsync(ct);
        return new(query.Period,Definition,asOf,items,query.Page,query.PageSize,count,(int)((count+(long)query.PageSize-1)/query.PageSize));
    }

    private IQueryable<DeliveryOrder> Cohort(AnalyticsQuery query) => db.DeliveryOrders.AsNoTracking().Where(o => o.CreatedAt >= query.Period.StartUtc && o.CreatedAt < query.Period.EndExclusiveUtc);
    private Task<IDbContextTransaction?> SnapshotAsync(CancellationToken ct) => db.Database.IsRelational() ? BeginAsync(ct) : Task.FromResult<IDbContextTransaction?>(null);
    private async Task<IDbContextTransaction?> BeginAsync(CancellationToken ct) => await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead,ct);
    private async Task EnsureManagerAsync(Guid caller, CancellationToken ct)
    {
        if (!await db.Users.AsNoTracking().AnyAsync(u => u.Id == caller && u.Status == "ACTIVE" && u.Role.Code == AppRoles.Manager,ct))
            throw new ApiException(403,"An active MANAGER account is required.");
    }
    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value,DateTimeKind.Utc);
    private static DateTime? Utc(DateTime? value) => value is {} v ? Utc(v) : null;
}
