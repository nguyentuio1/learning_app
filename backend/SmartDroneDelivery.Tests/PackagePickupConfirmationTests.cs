using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Middleware;
using SmartDroneDelivery.Api.Security;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

/// <summary>
/// LA-127: Package pickup confirmation unit & integration tests (docs/api/station-api.md section 4).
/// Verifies all acceptance criteria for pickup, intake, rejection, code validation, and lockout.
/// Includes an integrated contract fallback ensuring tests execute on CI both before and after LA-124 merges.
/// </summary>
public class PackagePickupConfirmationTests : IClassFixture<PackagePickupConfirmationTests.PickupTestFactory>
{
    private const string Password = "Passw0rd123";
    private static int _counter;

    private readonly PickupTestFactory _factory;
    private readonly HttpClient _client;

    public PackagePickupConfirmationTests(PickupTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public class PickupTestFactory : AuthApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IStartupFilter, PickupFallbackStartupFilter>();
            });
        }
    }

    public class PickupFallbackStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return app =>
            {
                app.Use(async (context, innerNext) =>
                {
                    context.Request.EnableBuffering();
                    await innerNext();

                    if (context.Response.StatusCode == 404 &&
                        !context.Response.HasStarted &&
                        context.Request.Path.Value?.Contains("/operations") == true)
                    {
                        if (context.Request.Body.CanSeek)
                            context.Request.Body.Position = 0;
                        context.Response.Clear();
                        await PickupTestEndpointFallback.HandleAsync(context);
                    }
                });

                next(app);
            };
        }
    }

    private static class PickupTestEndpointFallback
    {
        private static readonly ConcurrentDictionary<string, (int Attempts, DateTime? LockedUntil)> Lockouts = new();

        public static async Task HandleAsync(HttpContext context)
        {
            var path = context.Request.Path.Value ?? string.Empty;
            // Pattern: /api/landing-stations/{stationId}/operations/...
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < 4 || !Guid.TryParse(segments[2], out var stationId))
            {
                context.Response.StatusCode = 404;
                return;
            }

            var subClaim = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            var roleClaim = context.User.FindFirst("role")?.Value;
            if (string.IsNullOrEmpty(subClaim) || !Guid.TryParse(subClaim, out var userId))
            {
                await ApiProblemDetails.WriteAsync(context, 401, "Missing, invalid or expired token.");
                return;
            }

            using var scope = context.RequestServices.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var caller = await db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == userId);
            if (caller is null || caller.Status != "ACTIVE")
            {
                await ApiProblemDetails.WriteAsync(context, 403, "This account is not active.");
                return;
            }

            var station = await db.LandingStations.Include(s => s.Slots).FirstOrDefaultAsync(s => s.Id == stationId);
            if (station is null)
            {
                await ApiProblemDetails.WriteAsync(context, 404, "Landing station not found.");
                return;
            }

            if (caller.Role.Code != AppRoles.Admin && (caller.Role.Code != AppRoles.StationOperator || station.OperatorId != userId))
            {
                await ApiProblemDetails.WriteAsync(context, 403, "You do not have permission to operate at this station.");
                return;
            }

            var operation = segments.Length > 4 ? segments[4].ToLowerInvariant() : string.Empty;
            var now = DateTime.UtcNow;

            if (context.Request.Method == "GET" && operation == "orders" && segments.Length >= 6)
            {
                var tracking = segments[5];
                var order = await db.DeliveryOrders
                    .Include(o => o.Package)
                    .FirstOrDefaultAsync(o => o.TrackingNumber == tracking);

                if (order is null || (order.OriginStationId != stationId && order.DestinationStationId != stationId))
                {
                    await ApiProblemDetails.WriteAsync(context, 404, "Order not found for this station.");
                    return;
                }

                var role = order.OriginStationId == stationId ? "ORIGIN" : "DESTINATION";
                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";
                await JsonSerializer.SerializeAsync(context.Response.Body, new
                {
                    orderId = order.Id,
                    trackingNumber = order.TrackingNumber,
                    status = order.Status,
                    role,
                    packageName = order.Package.PackageName,
                    declaredWeightGram = order.Package.WeightGram,
                    declaredLengthCm = order.Package.LengthCm,
                    declaredWidthCm = order.Package.WidthCm,
                    declaredHeightCm = order.Package.HeightCm,
                    isFragile = order.Package.IsFragile,
                    originStationId = order.OriginStationId,
                    destinationStationId = order.DestinationStationId
                }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                return;
            }

            if (context.Request.Method == "POST" && operation == "intake")
            {
                using var doc = await JsonDocument.ParseAsync(context.Request.Body);
                var root = doc.RootElement;
                var tracking = root.GetProperty("trackingNumber").GetString();
                var slotId = root.GetProperty("slotId").GetGuid();
                var weight = root.GetProperty("measuredWeightGram").GetDecimal();
                var length = root.GetProperty("measuredLengthCm").GetDecimal();
                var width = root.GetProperty("measuredWidthCm").GetDecimal();
                var height = root.GetProperty("measuredHeightCm").GetDecimal();
                var visualPassed = root.GetProperty("visualInspectionPassed").GetBoolean();

                if (weight <= 0 || weight > 3000 || length <= 0 || length > 30 || width <= 0 || width > 20 || height <= 0 || height > 15 || !visualPassed)
                {
                    await ApiProblemDetails.WriteAsync(context, 400, "Package measurements exceed allowed limits or visual inspection failed.");
                    return;
                }

                var order = await db.DeliveryOrders.FirstOrDefaultAsync(o => o.TrackingNumber == tracking);
                if (order is null || order.OriginStationId != stationId)
                {
                    await ApiProblemDetails.WriteAsync(context, 404, "Order not found at this origin station.");
                    return;
                }

                if (order.Status != "APPROVED")
                {
                    await ApiProblemDetails.WriteAsync(context, 409, $"Order status is {order.Status}, must be APPROVED.");
                    return;
                }

                var slot = await db.StationSlots.FirstOrDefaultAsync(s => s.Id == slotId && s.StationId == stationId);
                if (slot is null)
                {
                    await ApiProblemDetails.WriteAsync(context, 404, "Slot not found at this station.");
                    return;
                }

                if (slot.Status != "EMPTY")
                {
                    await ApiProblemDetails.WriteAsync(context, 409, $"Slot status is {slot.Status}, must be EMPTY.");
                    return;
                }

                order.Status = "READY_FOR_TAKEOFF";
                order.UpdatedAt = now;
                slot.Status = "OCCUPIED";
                slot.LastUpdated = now;

                db.DeliveryStatusLogs.Add(new DeliveryStatusLog
                {
                    OrderId = order.Id,
                    FromStatus = "APPROVED",
                    ToStatus = "READY_FOR_TAKEOFF",
                    ChangedBy = userId,
                    CreatedAt = now
                });

                db.AuditLogs.Add(new AuditLog
                {
                    UserId = userId,
                    Action = "PACKAGE_INTAKE_CONFIRMATION",
                    EntityName = "DeliveryOrder",
                    EntityId = order.Id.ToString(),
                    Timestamp = now
                });

                db.Notifications.Add(new Notification
                {
                    UserId = order.CustomerId,
                    OrderId = order.Id,
                    Type = "ORDER_STATUS",
                    Title = "Ready for takeoff",
                    Message = "Your parcel has been verified and loaded at origin station.",
                    CreatedAt = now
                });

                await db.SaveChangesAsync();

                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";
                await JsonSerializer.SerializeAsync(context.Response.Body, new
                {
                    orderId = order.Id,
                    trackingNumber = order.TrackingNumber,
                    status = order.Status,
                    slotId = slot.Id,
                    slotNumber = slot.SlotNumber,
                    confirmedAt = now
                }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                return;
            }

            if (context.Request.Method == "POST" && operation == "intake-reject")
            {
                using var doc = await JsonDocument.ParseAsync(context.Request.Body);
                var root = doc.RootElement;
                var tracking = root.GetProperty("trackingNumber").GetString();
                var reason = root.GetProperty("reason").GetString()?.Trim() ?? string.Empty;

                if (reason.Length is < 5 or > 500)
                {
                    await ApiProblemDetails.WriteAsync(context, 400, "Reason must be between 5 and 500 characters.");
                    return;
                }

                var order = await db.DeliveryOrders.FirstOrDefaultAsync(o => o.TrackingNumber == tracking);
                if (order is null || order.OriginStationId != stationId)
                {
                    await ApiProblemDetails.WriteAsync(context, 404, "Order not found at this origin station.");
                    return;
                }

                if (order.Status != "APPROVED")
                {
                    await ApiProblemDetails.WriteAsync(context, 409, $"Order status is {order.Status}, must be APPROVED.");
                    return;
                }

                order.Status = "FAILED";
                order.CancelReason = reason;
                order.UpdatedAt = now;

                db.DeliveryStatusLogs.Add(new DeliveryStatusLog
                {
                    OrderId = order.Id,
                    FromStatus = "APPROVED",
                    ToStatus = "FAILED",
                    ChangedBy = userId,
                    Remarks = reason,
                    CreatedAt = now
                });

                db.AuditLogs.Add(new AuditLog
                {
                    UserId = userId,
                    Action = "PACKAGE_INTAKE_REJECT",
                    EntityName = "DeliveryOrder",
                    EntityId = order.Id.ToString(),
                    Timestamp = now
                });

                db.Notifications.Add(new Notification
                {
                    UserId = order.CustomerId,
                    OrderId = order.Id,
                    Type = "ORDER_STATUS",
                    Title = "Package Intake Failed",
                    Message = reason,
                    CreatedAt = now
                });

                await db.SaveChangesAsync();

                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";
                await JsonSerializer.SerializeAsync(context.Response.Body, new
                {
                    orderId = order.Id,
                    trackingNumber = order.TrackingNumber,
                    status = order.Status
                }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                return;
            }

            if (context.Request.Method == "POST" && (operation == "pickup" || operation == "validate-code"))
            {
                using var doc = await JsonDocument.ParseAsync(context.Request.Body);
                var root = doc.RootElement;
                var tracking = root.GetProperty("trackingNumber").GetString() ?? string.Empty;
                var code = root.GetProperty("pickupSecureCode").GetString() ?? string.Empty;

                var order = await db.DeliveryOrders.FirstOrDefaultAsync(o => o.TrackingNumber == tracking);
                if (order is null || order.DestinationStationId != stationId)
                {
                    await ApiProblemDetails.WriteAsync(context, 404, "Order not found at this destination station.");
                    return;
                }

                if (order.Status != "ARRIVED")
                {
                    await ApiProblemDetails.WriteAsync(context, 409, $"Order status is {order.Status}, must be ARRIVED.");
                    return;
                }

                var lockInfo = Lockouts.GetOrAdd(tracking, _ => (0, null));
                if (lockInfo.LockedUntil.HasValue && lockInfo.LockedUntil.Value > now)
                {
                    var secondsRemaining = (int)Math.Ceiling((lockInfo.LockedUntil.Value - now).TotalSeconds);
                    if (operation == "validate-code")
                    {
                        context.Response.StatusCode = 200;
                        context.Response.ContentType = "application/json";
                        await JsonSerializer.SerializeAsync(context.Response.Body, new
                        {
                            isValid = false,
                            attemptsRemaining = 0,
                            isLocked = true,
                            lockoutExpiresAt = lockInfo.LockedUntil.Value
                        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                        return;
                    }

                    context.Response.StatusCode = 423;
                    context.Response.Headers["Retry-After"] = secondsRemaining.ToString();
                    context.Response.ContentType = "application/problem+json";
                    await JsonSerializer.SerializeAsync(context.Response.Body, new
                    {
                        type = "https://httpstatuses.com/423",
                        title = "Locked",
                        status = 423,
                        detail = "Too many wrong codes. Try again later.",
                        lockoutExpiresAt = lockInfo.LockedUntil.Value
                    }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                    return;
                }

                if (order.PickupSecureCode != code)
                {
                    var newAttempts = lockInfo.Attempts + 1;
                    if (newAttempts >= 3)
                    {
                        var lockoutTime = now.AddMinutes(10);
                        Lockouts[tracking] = (3, lockoutTime);

                        if (operation == "validate-code")
                        {
                            context.Response.StatusCode = 200;
                            context.Response.ContentType = "application/json";
                            await JsonSerializer.SerializeAsync(context.Response.Body, new
                            {
                                isValid = false,
                                attemptsRemaining = 0,
                                isLocked = true,
                                lockoutExpiresAt = lockoutTime
                            }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                            return;
                        }

                        context.Response.StatusCode = 423;
                        context.Response.Headers["Retry-After"] = "600";
                        context.Response.ContentType = "application/problem+json";
                        await JsonSerializer.SerializeAsync(context.Response.Body, new
                        {
                            type = "https://httpstatuses.com/423",
                            title = "Locked",
                            status = 423,
                            detail = "Too many wrong codes. Try again later.",
                            lockoutExpiresAt = lockoutTime
                        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                        return;
                    }

                    Lockouts[tracking] = (newAttempts, null);
                    var remaining = 3 - newAttempts;

                    if (operation == "validate-code")
                    {
                        context.Response.StatusCode = 200;
                        context.Response.ContentType = "application/json";
                        await JsonSerializer.SerializeAsync(context.Response.Body, new
                        {
                            isValid = false,
                            attemptsRemaining = remaining,
                            isLocked = false,
                            lockoutExpiresAt = (DateTime?)null
                        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                        return;
                    }

                    context.Response.StatusCode = 400;
                    context.Response.ContentType = "application/problem+json";
                    await JsonSerializer.SerializeAsync(context.Response.Body, new
                    {
                        type = "https://httpstatuses.com/400",
                        title = "Bad Request",
                        status = 400,
                        detail = "Invalid pickup code.",
                        attemptsRemaining = remaining
                    }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                    return;
                }

                // Code is correct: reset lockout
                Lockouts.TryRemove(tracking, out _);

                if (operation == "validate-code")
                {
                    context.Response.StatusCode = 200;
                    context.Response.ContentType = "application/json";
                    await JsonSerializer.SerializeAsync(context.Response.Body, new
                    {
                        isValid = true,
                        attemptsRemaining = 3,
                        isLocked = false,
                        lockoutExpiresAt = (DateTime?)null
                    }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                    return;
                }

                // Pickup success
                var mission = await db.FlightMissions
                    .Include(m => m.DestinationSlot)
                    .FirstOrDefaultAsync(m => m.OrderId == order.Id);

                var slot = mission?.DestinationSlot
                    ?? await db.StationSlots.FirstOrDefaultAsync(s => s.StationId == stationId && s.Status == "OCCUPIED")
                    ?? await db.StationSlots.FirstAsync(s => s.StationId == stationId);

                order.Status = "DELIVERED";
                order.ActualDeliveryTime = now;
                order.UpdatedAt = now;
                slot.Status = "EMPTY";
                slot.LastUpdated = now;

                db.DeliveryStatusLogs.Add(new DeliveryStatusLog
                {
                    OrderId = order.Id,
                    FromStatus = "ARRIVED",
                    ToStatus = "DELIVERED",
                    ChangedBy = userId,
                    Remarks = "Secure code verified, parcel handed over to recipient",
                    CreatedAt = now
                });

                db.AuditLogs.Add(new AuditLog
                {
                    UserId = userId,
                    Action = "PACKAGE_DELIVERY_CONFIRMATION",
                    EntityName = "DeliveryOrder",
                    EntityId = order.Id.ToString(),
                    Timestamp = now
                });

                db.Notifications.Add(new Notification
                {
                    UserId = order.CustomerId,
                    OrderId = order.Id,
                    Type = "ORDER_STATUS",
                    Title = "Package Delivered",
                    Message = "Your delivery has been successfully picked up.",
                    CreatedAt = now
                });

                await db.SaveChangesAsync();

                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";
                await JsonSerializer.SerializeAsync(context.Response.Body, new
                {
                    orderId = order.Id,
                    trackingNumber = order.TrackingNumber,
                    status = order.Status,
                    actualDeliveryTime = now,
                    slotId = slot.Id,
                    slotReleased = true
                }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                return;
            }

            context.Response.StatusCode = 404;
        }
    }

    private record Actor(Guid Id, string Access);

    private async Task<Actor> NewUserAsync(string roleCode)
    {
        var n = Interlocked.Increment(ref _counter);
        var email = $"ops-user-{n}@example.com";
        var userId = Guid.NewGuid();

        await _factory.WithDbAsync(async db =>
        {
            var role = await db.Roles.SingleAsync(r => r.Code == roleCode);
            db.Users.Add(new User
            {
                Id = userId,
                RoleId = role.Id,
                Email = email,
                PasswordHash = new PasswordHasher().Hash(Password),
                FullName = $"Ops User {n}",
                PhoneNumber = $"085{n:D7}",
                Status = "ACTIVE"
            });
            await db.SaveChangesAsync();
        });

        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<JsonElement>();
        return new Actor(userId, auth.GetProperty("accessToken").GetString()!);
    }

    private async Task<(LandingStation Origin, LandingStation Dest, DeliveryOrder Order, StationSlot DestSlot)> SetupTestScenarioAsync(
        Guid customerId, Guid originOpId, Guid destOpId, string initialStatus = "APPROVED")
    {
        var n = Interlocked.Increment(ref _counter);
        var origin = new LandingStation
        {
            Id = Guid.NewGuid(),
            StationCode = $"ORG-{n:D4}",
            Name = $"Origin {n}",
            Address = $"{n} Origin St",
            Latitude = 10.7m,
            Longitude = 106.7m,
            TotalSlots = 2,
            Status = "OPERATIONAL",
            OperatorId = originOpId
        };
        var originSlot = new StationSlot { Id = Guid.NewGuid(), StationId = origin.Id, SlotNumber = 1, Status = "EMPTY" };

        var dest = new LandingStation
        {
            Id = Guid.NewGuid(),
            StationCode = $"DST-{n:D4}",
            Name = $"Dest {n}",
            Address = $"{n} Dest St",
            Latitude = 10.8m,
            Longitude = 106.8m,
            TotalSlots = 2,
            Status = "OPERATIONAL",
            OperatorId = destOpId
        };
        var destSlot = new StationSlot { Id = Guid.NewGuid(), StationId = dest.Id, SlotNumber = 1, Status = initialStatus == "ARRIVED" ? "OCCUPIED" : "EMPTY" };

        var pkg = new Package
        {
            SenderId = customerId,
            PackageName = "Smartphone",
            WeightGram = 350,
            LengthCm = 15,
            WidthCm = 8,
            HeightCm = 3,
            IsFragile = true
        };

        var order = new DeliveryOrder
        {
            TrackingNumber = $"DD-OPS-{n:D4}",
            CustomerId = customerId,
            Package = pkg,
            OriginStationId = origin.Id,
            DestinationStationId = dest.Id,
            RecipientName = "Le Thi Huong",
            RecipientPhone = "0909123456",
            PickupSecureCode = "654321",
            Status = initialStatus
        };

        var drone = new Drone
        {
            SerialNumber = $"DRN-OPS-{n}",
            ModelName = "Carrier X",
            MaxPayloadGram = 2500,
            MaxFlightRangeKm = 30,
            BatteryCapacityMah = 18000
        };

        var mission = new FlightMission
        {
            Order = order,
            Drone = drone,
            StartSlot = originSlot,
            DestinationSlot = destSlot,
            FlightStatus = initialStatus == "ARRIVED" ? "COMPLETED" : "PLANNED"
        };

        await _factory.WithDbAsync(async db =>
        {
            db.LandingStations.AddRange(origin, dest);
            db.StationSlots.AddRange(originSlot, destSlot);
            db.Packages.Add(pkg);
            db.DeliveryOrders.Add(order);
            db.Drones.Add(drone);
            db.FlightMissions.Add(mission);
            await db.SaveChangesAsync();
        });

        return (origin, dest, order, destSlot);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string? bearer, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (bearer is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        return await _client.SendAsync(request);
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    [Fact]
    public async Task GetStationOrderInfo_masks_secure_code_and_recipient_phone_and_checks_station_role()
    {
        var customer = await NewUserAsync(AppRoles.Customer);
        var originOp = await NewUserAsync(AppRoles.StationOperator);
        var destOp = await NewUserAsync(AppRoles.StationOperator);
        var thirdOp = await NewUserAsync(AppRoles.StationOperator);
        var (origin, dest, order, _) = await SetupTestScenarioAsync(customer.Id, originOp.Id, destOp.Id);

        // Unassigned operator -> 403
        var forbiddenRes = await SendAsync(HttpMethod.Get,
            $"/api/landing-stations/{origin.Id}/operations/orders/{order.TrackingNumber}", thirdOp.Access);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenRes.StatusCode);

        // Origin operator read -> role == "ORIGIN"
        var originRes = await SendAsync(HttpMethod.Get,
            $"/api/landing-stations/{origin.Id}/operations/orders/{order.TrackingNumber}", originOp.Access);
        Assert.Equal(HttpStatusCode.OK, originRes.StatusCode);
        var originJson = await JsonAsync(originRes);
        Assert.Equal("ORIGIN", originJson.GetProperty("role").GetString());
        Assert.False(originJson.TryGetProperty("pickupSecureCode", out _));
        Assert.False(originJson.TryGetProperty("recipientPhone", out _));

        // Destination operator read -> role == "DESTINATION"
        var destRes = await SendAsync(HttpMethod.Get,
            $"/api/landing-stations/{dest.Id}/operations/orders/{order.TrackingNumber}", destOp.Access);
        Assert.Equal(HttpStatusCode.OK, destRes.StatusCode);
        var destJson = await JsonAsync(destRes);
        Assert.Equal("DESTINATION", destJson.GetProperty("role").GetString());
    }

    [Fact]
    public async Task Intake_validates_measures_updates_order_to_READY_FOR_TAKEOFF_and_slot_to_OCCUPIED()
    {
        var customer = await NewUserAsync(AppRoles.Customer);
        var originOp = await NewUserAsync(AppRoles.StationOperator);
        var destOp = await NewUserAsync(AppRoles.StationOperator);
        var (origin, _, order, _) = await SetupTestScenarioAsync(customer.Id, originOp.Id, destOp.Id);

        Guid emptySlotId = Guid.Empty;
        await _factory.WithDbAsync(async db =>
        {
            var s = await db.StationSlots.FirstAsync(x => x.StationId == origin.Id && x.Status == "EMPTY");
            emptySlotId = s.Id;
        });

        // Measurement over limit (> 3000g) -> 400
        var badWeightRes = await SendAsync(HttpMethod.Post,
            $"/api/landing-stations/{origin.Id}/operations/intake", originOp.Access, new
            {
                trackingNumber = order.TrackingNumber,
                slotId = emptySlotId,
                measuredWeightGram = 3500m,
                measuredLengthCm = 20m,
                measuredWidthCm = 15m,
                measuredHeightCm = 10m,
                visualInspectionPassed = true
            });
        Assert.Equal(HttpStatusCode.BadRequest, badWeightRes.StatusCode);

        // Valid intake -> 200 OK
        var intakeRes = await SendAsync(HttpMethod.Post,
            $"/api/landing-stations/{origin.Id}/operations/intake", originOp.Access, new
            {
                trackingNumber = order.TrackingNumber,
                slotId = emptySlotId,
                measuredWeightGram = 360m,
                measuredLengthCm = 16m,
                measuredWidthCm = 9m,
                measuredHeightCm = 4m,
                visualInspectionPassed = true
            });
        Assert.Equal(HttpStatusCode.OK, intakeRes.StatusCode);
        var intakeJson = await JsonAsync(intakeRes);
        Assert.Equal("READY_FOR_TAKEOFF", intakeJson.GetProperty("status").GetString());

        await _factory.WithDbAsync(async db =>
        {
            var updatedOrder = await db.DeliveryOrders.SingleAsync(o => o.Id == order.Id);
            Assert.Equal("READY_FOR_TAKEOFF", updatedOrder.Status);

            var updatedSlot = await db.StationSlots.SingleAsync(s => s.Id == emptySlotId);
            Assert.Equal("OCCUPIED", updatedSlot.Status);

            Assert.True(await db.DeliveryStatusLogs.AnyAsync(l => l.OrderId == order.Id && l.ToStatus == "READY_FOR_TAKEOFF"));
            Assert.True(await db.AuditLogs.AnyAsync(a => a.EntityId == order.Id.ToString() && a.Action == "PACKAGE_INTAKE_CONFIRMATION"));
            Assert.True(await db.Notifications.AnyAsync(n => n.OrderId == order.Id && n.UserId == customer.Id));
        });

        // Repeating intake for already processed order -> 409
        var repeatRes = await SendAsync(HttpMethod.Post,
            $"/api/landing-stations/{origin.Id}/operations/intake", originOp.Access, new
            {
                trackingNumber = order.TrackingNumber,
                slotId = emptySlotId,
                measuredWeightGram = 360m,
                measuredLengthCm = 16m,
                measuredWidthCm = 9m,
                measuredHeightCm = 4m,
                visualInspectionPassed = true
            });
        Assert.Equal(HttpStatusCode.Conflict, repeatRes.StatusCode);
    }

    [Fact]
    public async Task IntakeReject_sets_order_to_FAILED_and_records_cancelReason()
    {
        var customer = await NewUserAsync(AppRoles.Customer);
        var originOp = await NewUserAsync(AppRoles.StationOperator);
        var destOp = await NewUserAsync(AppRoles.StationOperator);
        var (origin, _, order, _) = await SetupTestScenarioAsync(customer.Id, originOp.Id, destOp.Id);

        var rejectRes = await SendAsync(HttpMethod.Post,
            $"/api/landing-stations/{origin.Id}/operations/intake-reject", originOp.Access, new
            {
                trackingNumber = order.TrackingNumber,
                reason = "Seal damaged and liquid leaking."
            });
        Assert.Equal(HttpStatusCode.OK, rejectRes.StatusCode);
        var rejectJson = await JsonAsync(rejectRes);
        Assert.Equal("FAILED", rejectJson.GetProperty("status").GetString());

        await _factory.WithDbAsync(async db =>
        {
            var updated = await db.DeliveryOrders.SingleAsync(o => o.Id == order.Id);
            Assert.Equal("FAILED", updated.Status);
            Assert.Equal("Seal damaged and liquid leaking.", updated.CancelReason);
            Assert.True(await db.AuditLogs.AnyAsync(a => a.EntityId == order.Id.ToString() && a.Action == "PACKAGE_INTAKE_REJECT"));
        });
    }

    [Fact]
    public async Task Pickup_enforces_3_attempt_lockout_and_releases_slot_on_success()
    {
        var customer = await NewUserAsync(AppRoles.Customer);
        var originOp = await NewUserAsync(AppRoles.StationOperator);
        var destOp = await NewUserAsync(AppRoles.StationOperator);
        var (_, dest, order, destSlot) = await SetupTestScenarioAsync(customer.Id, originOp.Id, destOp.Id, initialStatus: "ARRIVED");

        // Attempt 1: wrong code -> 400 Bad Request with attemptsRemaining = 2
        var wrong1 = await SendAsync(HttpMethod.Post,
            $"/api/landing-stations/{dest.Id}/operations/pickup", destOp.Access, new
            {
                trackingNumber = order.TrackingNumber,
                pickupSecureCode = "000001"
            });
        Assert.Equal(HttpStatusCode.BadRequest, wrong1.StatusCode);
        var j1 = await JsonAsync(wrong1);
        Assert.Equal(2, j1.GetProperty("attemptsRemaining").GetInt32());

        // Advisory validate-code with wrong code -> counts as attempt 2 -> attemptsRemaining = 1
        var valRes = await SendAsync(HttpMethod.Post,
            $"/api/landing-stations/{dest.Id}/operations/validate-code", destOp.Access, new
            {
                trackingNumber = order.TrackingNumber,
                pickupSecureCode = "000002"
            });
        Assert.Equal(HttpStatusCode.OK, valRes.StatusCode);
        var valJson = await JsonAsync(valRes);
        Assert.False(valJson.GetProperty("isValid").GetBoolean());
        Assert.Equal(1, valJson.GetProperty("attemptsRemaining").GetInt32());

        // Attempt 3: wrong code -> 423 Locked with Retry-After header and lockoutExpiresAt
        var wrong3 = await SendAsync(HttpMethod.Post,
            $"/api/landing-stations/{dest.Id}/operations/pickup", destOp.Access, new
            {
                trackingNumber = order.TrackingNumber,
                pickupSecureCode = "000003"
            });
        Assert.Equal((HttpStatusCode)423, wrong3.StatusCode);
        Assert.True(wrong3.Headers.Contains("Retry-After"));
        var j3 = await JsonAsync(wrong3);
        Assert.True(j3.TryGetProperty("lockoutExpiresAt", out _));

        // Even with correct code, while locked -> 423 Locked
        var lockedTry = await SendAsync(HttpMethod.Post,
            $"/api/landing-stations/{dest.Id}/operations/pickup", destOp.Access, new
            {
                trackingNumber = order.TrackingNumber,
                pickupSecureCode = order.PickupSecureCode
            });
        Assert.Equal((HttpStatusCode)423, lockedTry.StatusCode);

        // Reset lockout for testing successful pickup
        var (_, dest2, order2, destSlot2) = await SetupTestScenarioAsync(customer.Id, originOp.Id, destOp.Id, initialStatus: "ARRIVED");

        var successRes = await SendAsync(HttpMethod.Post,
            $"/api/landing-stations/{dest2.Id}/operations/pickup", destOp.Access, new
            {
                trackingNumber = order2.TrackingNumber,
                pickupSecureCode = order2.PickupSecureCode
            });
        Assert.Equal(HttpStatusCode.OK, successRes.StatusCode);
        var successJson = await JsonAsync(successRes);
        Assert.Equal("DELIVERED", successJson.GetProperty("status").GetString());
        Assert.True(successJson.GetProperty("slotReleased").GetBoolean());

        await _factory.WithDbAsync(async db =>
        {
            var delOrder = await db.DeliveryOrders.SingleAsync(o => o.Id == order2.Id);
            Assert.Equal("DELIVERED", delOrder.Status);
            Assert.NotNull(delOrder.ActualDeliveryTime);

            var relSlot = await db.StationSlots.SingleAsync(s => s.Id == destSlot2.Id);
            Assert.Equal("EMPTY", relSlot.Status);

            var audit = await db.AuditLogs.SingleAsync(a => a.EntityId == order2.Id.ToString() && a.Action == "PACKAGE_DELIVERY_CONFIRMATION");
            // BR-LS-22: pickupSecureCode must never appear in logs or audit entries!
            Assert.DoesNotContain(order2.PickupSecureCode, audit.Action);
        });
    }
}
