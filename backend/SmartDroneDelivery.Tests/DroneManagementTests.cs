using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.DTOs;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Security;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

/// <summary>LA-128: Drone management API (docs/api/drone-api.md sections 2 and 3).</summary>
public class DroneManagementTests : IClassFixture<AuthApiFactory>
{
    private const string Password = "Passw0rd123";
    private static int _counter;

    private readonly AuthApiFactory _factory;
    private readonly HttpClient _client;

    public DroneManagementTests(AuthApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private record Actor(Guid Id, string Access);

    private async Task<Actor> NewUserAsync(string roleCode)
    {
        var n = Interlocked.Increment(ref _counter);
        var email = $"drone-user-{n}@example.com";
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
                FullName = $"Drone Actor {n}",
                PhoneNumber = $"084{n:D7}",
                Status = "ACTIVE"
            });
            await db.SaveChangesAsync();
        });

        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<JsonElement>();
        return new Actor(userId, auth.GetProperty("accessToken").GetString()!);
    }

    private async Task<LandingStation> CreateStationAsync()
    {
        var n = Interlocked.Increment(ref _counter);
        var station = new LandingStation
        {
            Id = Guid.NewGuid(),
            StationCode = $"DS-{n:D4}",
            Name = $"Drone Hub {n}",
            Address = $"{n} Air Rd",
            Latitude = 10.7m,
            Longitude = 106.7m,
            TotalSlots = 2,
            Status = "OPERATIONAL"
        };
        await _factory.WithDbAsync(async db =>
        {
            db.LandingStations.Add(station);
            await db.SaveChangesAsync();
        });
        return station;
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
    public async Task Create_provisions_drone_in_MAINTENANCE_and_enforces_validation()
    {
        var dispatcher = await NewUserAsync(AppRoles.Dispatcher);
        var customer = await NewUserAsync(AppRoles.Customer);
        var station = await CreateStationAsync();
        var serial = $"DRN-TEST-{Interlocked.Increment(ref _counter):D4}";

        // Customer cannot create -> 403
        var forbiddenRes = await SendAsync(HttpMethod.Post, "/api/drones", customer.Access, new
        {
            serialNumber = serial,
            modelName = "Aero X",
            maxPayloadGram = 2500m,
            maxFlightRangeKm = 20m,
            batteryCapacityMah = 18000
        });
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenRes.StatusCode);

        // Dispatcher creates successfully -> 201 Created with MAINTENANCE status
        var createRes = await SendAsync(HttpMethod.Post, "/api/drones", dispatcher.Access, new
        {
            serialNumber = serial,
            modelName = "Aero X",
            maxPayloadGram = 2500m,
            maxFlightRangeKm = 20m,
            batteryCapacityMah = 18000,
            currentStationId = station.Id
        });
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var json = await JsonAsync(createRes);
        var droneId = Guid.Parse(json.GetProperty("id").GetString()!);
        Assert.Equal($"/api/drones/{droneId}", createRes.Headers.Location!.OriginalString);
        Assert.Equal(serial, json.GetProperty("serialNumber").GetString());
        Assert.Equal("MAINTENANCE", json.GetProperty("status").GetString());
        Assert.Equal(station.Id.ToString(), json.GetProperty("currentStationId").GetString());

        await _factory.WithDbAsync(async db =>
        {
            var audit = await db.AuditLogs.SingleAsync(a => a.EntityId == droneId.ToString() && a.Action == "DRONE_CREATE");
            Assert.Equal(dispatcher.Id, audit.UserId);
        });

        // Duplicate serial number -> 409
        var dupRes = await SendAsync(HttpMethod.Post, "/api/drones", dispatcher.Access, new
        {
            serialNumber = serial,
            modelName = "Another",
            maxPayloadGram = 1000m,
            maxFlightRangeKm = 10m,
            batteryCapacityMah = 5000
        });
        Assert.Equal(HttpStatusCode.Conflict, dupRes.StatusCode);

        // Validation limits (payload > 3000g, non-existent station) -> 400
        var badRes = await SendAsync(HttpMethod.Post, "/api/drones", dispatcher.Access, new
        {
            serialNumber = $"DRN-BAD-{Interlocked.Increment(ref _counter)}",
            modelName = "Too Heavy",
            maxPayloadGram = 3500m,
            maxFlightRangeKm = 10m,
            batteryCapacityMah = 5000,
            currentStationId = Guid.NewGuid()
        });
        Assert.Equal(HttpStatusCode.BadRequest, badRes.StatusCode);
    }

    [Fact]
    public async Task List_and_GetById_support_filters_activeMissionId_and_flying_station_masking()
    {
        var admin = await NewUserAsync(AppRoles.Admin);
        var station = await CreateStationAsync();
        var serial = $"DRN-FLY-{Interlocked.Increment(ref _counter):D4}";

        Guid droneId = Guid.Empty;
        Guid missionId = Guid.Empty;

        await _factory.WithDbAsync(async db =>
        {
            var drone = new Drone
            {
                SerialNumber = serial,
                ModelName = "Interceptor",
                MaxPayloadGram = 2000,
                MaxFlightRangeKm = 30,
                BatteryCapacityMah = 20000,
                Status = "FLYING",
                CurrentStationId = station.Id
            };
            db.Drones.Add(drone);
            droneId = drone.Id;

            var role = await db.Roles.SingleAsync(r => r.Code == AppRoles.Customer);
            var cust = new User
            {
                RoleId = role.Id,
                Email = $"cust-{Interlocked.Increment(ref _counter)}@example.com",
                PasswordHash = new PasswordHasher().Hash(Password),
                FullName = "Customer",
                PhoneNumber = $"088{_counter:D7}",
                Status = "ACTIVE"
            };
            var pkg = new Package { Sender = cust, PackageName = "P", WeightGram = 500, LengthCm = 5, WidthCm = 5, HeightCm = 5 };
            var order = new DeliveryOrder
            {
                TrackingNumber = $"DD-DRN-{_counter:D4}",
                Customer = cust,
                Package = pkg,
                OriginStationId = station.Id,
                DestinationStationId = station.Id,
                RecipientName = "R",
                RecipientPhone = "0900000000",
                PickupSecureCode = "999888"
            };
            var mission = new FlightMission
            {
                Drone = drone,
                Order = order,
                FlightStatus = "IN_AIR",
                StartTime = DateTime.UtcNow
            };
            db.FlightMissions.Add(mission);
            await db.SaveChangesAsync();
            missionId = mission.Id;
        });

        // Query by status and search
        var listRes = await SendAsync(HttpMethod.Get, $"/api/drones?status=FLYING&search={serial.ToLowerInvariant()}", admin.Access);
        Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);
        var list = await JsonAsync(listRes);
        Assert.Single(list.EnumerateArray());
        var item = list[0];
        Assert.Equal("FLYING", item.GetProperty("status").GetString());
        // Masked when FLYING
        Assert.Equal(JsonValueKind.Null, item.GetProperty("currentStationId").ValueKind);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("currentStationName").ValueKind);

        // GetById returns activeMissionId
        var detailRes = await SendAsync(HttpMethod.Get, $"/api/drones/{droneId}", admin.Access);
        Assert.Equal(HttpStatusCode.OK, detailRes.StatusCode);
        var detail = await JsonAsync(detailRes);
        Assert.Equal(missionId.ToString(), detail.GetProperty("activeMissionId").GetString());
    }

    [Fact]
    public async Task Update_and_Maintenance_state_transitions_and_concurrency()
    {
        var dispatcher = await NewUserAsync(AppRoles.Dispatcher);
        var serial = $"DRN-MTC-{Interlocked.Increment(ref _counter):D4}";

        var createRes = await SendAsync(HttpMethod.Post, "/api/drones", dispatcher.Access, new
        {
            serialNumber = serial,
            modelName = "Mtc V1",
            maxPayloadGram = 2000m,
            maxFlightRangeKm = 15m,
            batteryCapacityMah = 10000
        });
        var drone = await JsonAsync(createRes);
        var droneId = Guid.Parse(drone.GetProperty("id").GetString()!);

        // PUT update specifications
        var updateRes = await SendAsync(HttpMethod.Put, $"/api/drones/{droneId}", dispatcher.Access, new
        {
            modelName = "Mtc V2",
            maxPayloadGram = 2200m,
            maxFlightRangeKm = 18m,
            batteryCapacityMah = 12000
        });
        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);
        var updated = await JsonAsync(updateRes);
        Assert.Equal("Mtc V2", updated.GetProperty("modelName").GetString());

        // Disallowed field in PUT -> 400
        var badFieldRes = await SendAsync(HttpMethod.Put, $"/api/drones/{droneId}", dispatcher.Access, new
        {
            modelName = "Mtc V3",
            maxPayloadGram = 2200m,
            maxFlightRangeKm = 18m,
            batteryCapacityMah = 12000,
            serialNumber = "CHANGED"
        });
        Assert.Equal(HttpStatusCode.BadRequest, badFieldRes.StatusCode);

        // Maintenance COMPLETE from MAINTENANCE -> IDLE (readyForService = true)
        var completeRes = await SendAsync(HttpMethod.Put, $"/api/drones/{droneId}/maintenance", dispatcher.Access, new
        {
            action = "COMPLETE",
            expectedStatus = "MAINTENANCE",
            inspectionPassed = true,
            readyForService = true,
            reason = "Initial flight readiness check completed."
        });
        Assert.Equal(HttpStatusCode.OK, completeRes.StatusCode);
        var readyDrone = await JsonAsync(completeRes);
        Assert.Equal("IDLE", readyDrone.GetProperty("status").GetString());

        // Stale expectedStatus on repeated call -> 409
        var staleRes = await SendAsync(HttpMethod.Put, $"/api/drones/{droneId}/maintenance", dispatcher.Access, new
        {
            action = "COMPLETE",
            expectedStatus = "MAINTENANCE",
            inspectionPassed = true,
            readyForService = true,
            reason = "Duplicate call."
        });
        Assert.Equal(HttpStatusCode.Conflict, staleRes.StatusCode);

        // Maintenance START from IDLE -> MAINTENANCE
        var startRes = await SendAsync(HttpMethod.Put, $"/api/drones/{droneId}/maintenance", dispatcher.Access, new
        {
            action = "START",
            expectedStatus = "IDLE",
            reason = "Scheduled rotor check."
        });
        Assert.Equal(HttpStatusCode.OK, startRes.StatusCode);
        Assert.Equal("MAINTENANCE", (await JsonAsync(startRes)).GetProperty("status").GetString());

        // Maintenance COMPLETE to CHARGING (readyForService = false)
        var toChargeRes = await SendAsync(HttpMethod.Put, $"/api/drones/{droneId}/maintenance", dispatcher.Access, new
        {
            action = "COMPLETE",
            expectedStatus = "MAINTENANCE",
            inspectionPassed = true,
            readyForService = false,
            reason = "Rotor checked, battery needs recharge."
        });
        Assert.Equal(HttpStatusCode.OK, toChargeRes.StatusCode);
        Assert.Equal("CHARGING", (await JsonAsync(toChargeRes)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Maintenance_and_Update_are_refused_with_409_when_flight_mission_is_active()
    {
        var dispatcher = await NewUserAsync(AppRoles.Dispatcher);
        var station = await CreateStationAsync();
        var serial = $"DRN-ACT-{Interlocked.Increment(ref _counter):D4}";

        var drone = new Drone
        {
            SerialNumber = serial,
            ModelName = "Air Carrier",
            MaxPayloadGram = 2500,
            MaxFlightRangeKm = 20,
            BatteryCapacityMah = 18000,
            Status = "IDLE",
            CurrentStationId = station.Id
        };

        await _factory.WithDbAsync(async db =>
        {
            db.Drones.Add(drone);
            var custRole = await db.Roles.SingleAsync(r => r.Code == AppRoles.Customer);
            var cust = new User
            {
                RoleId = custRole.Id,
                Email = $"cust-act-{_counter}@example.com",
                PasswordHash = new PasswordHasher().Hash(Password),
                FullName = "Customer Act",
                PhoneNumber = $"089{_counter:D7}",
                Status = "ACTIVE"
            };
            var pkg = new Package { Sender = cust, PackageName = "Package", WeightGram = 500, LengthCm = 5, WidthCm = 5, HeightCm = 5 };
            var order = new DeliveryOrder
            {
                TrackingNumber = $"DD-ACT-{_counter:D4}",
                Customer = cust,
                Package = pkg,
                OriginStationId = station.Id,
                DestinationStationId = station.Id,
                RecipientName = "Recipient",
                RecipientPhone = "0900000001",
                PickupSecureCode = "123123"
            };
            var mission = new FlightMission
            {
                Drone = drone,
                Order = order,
                FlightStatus = "PLANNED"
            };
            db.FlightMissions.Add(mission);
            await db.SaveChangesAsync();
        });

        // Maintenance START while drone has PLANNED mission -> 409
        var maintRes = await SendAsync(HttpMethod.Put, $"/api/drones/{drone.Id}/maintenance", dispatcher.Access, new
        {
            action = "START",
            expectedStatus = "IDLE",
            reason = "Routine check."
        });
        Assert.Equal(HttpStatusCode.Conflict, maintRes.StatusCode);

        // PUT update specifications while drone has PLANNED mission -> 409
        var updateRes = await SendAsync(HttpMethod.Put, $"/api/drones/{drone.Id}", dispatcher.Access, new
        {
            modelName = "Air Carrier V2",
            maxPayloadGram = 2600m,
            maxFlightRangeKm = 22m,
            batteryCapacityMah = 19000
        });
        Assert.Equal(HttpStatusCode.Conflict, updateRes.StatusCode);
    }

    [Fact]
    public async Task Create_duplicate_serial_number_concurrent_and_sequential_returns_409_Conflict()
    {
        var admin = await NewUserAsync(AppRoles.Admin);
        var serial = $"DRN-DUP-{Interlocked.Increment(ref _counter):D4}";

        var t1 = SendAsync(HttpMethod.Post, "/api/drones", admin.Access, new
        {
            serialNumber = serial,
            modelName = "Falcon 1",
            maxPayloadGram = 1500m,
            maxFlightRangeKm = 15m,
            batteryCapacityMah = 10000
        });

        var t2 = SendAsync(HttpMethod.Post, "/api/drones", admin.Access, new
        {
            serialNumber = serial,
            modelName = "Falcon 2",
            maxPayloadGram = 1600m,
            maxFlightRangeKm = 16m,
            batteryCapacityMah = 12000
        });

        var responses = await Task.WhenAll(t1, t2);
        var codes = responses.Select(r => r.StatusCode).ToList();
        Assert.Contains(HttpStatusCode.Created, codes);
        Assert.Contains(HttpStatusCode.Conflict, codes);
    }

    [Fact]
    public async Task Non_serial_DbUpdateException_with_key_in_message_is_rethrown_and_not_masked_as_409()
    {
        var admin = await NewUserAsync(AppRoles.Admin);

        // 1. Verify IsDuplicateSerialException strictly checks Postgres 23505 + IX_drones_SerialNumber
        var genericKeyError = new DbUpdateException(
            "An error occurred while saving the entity changes.",
            new Exception("violates foreign key constraint or duplicate key on another table"));
        Assert.False(DroneService.IsDuplicateSerialException(genericKeyError));

        var otherUniquePgError = new DbUpdateException(
            "Unique violation on non-serial constraint",
            new PostgresException("duplicate key value", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation, constraintName: "PK_drones"));
        Assert.False(DroneService.IsDuplicateSerialException(otherUniquePgError));

        var fkPgError = new DbUpdateException(
            "FK violation",
            new PostgresException("violates foreign key constraint", "ERROR", "ERROR", PostgresErrorCodes.ForeignKeyViolation, constraintName: "FK_drones_landing_stations_CurrentStationId"));
        Assert.False(DroneService.IsDuplicateSerialException(fkPgError));

        var serialUniquePgError = new DbUpdateException(
            "Serial unique violation",
            new PostgresException("duplicate key value", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation, constraintName: DroneService.SerialNumberUniqueConstraint));
        Assert.True(DroneService.IsDuplicateSerialException(serialUniquePgError));

        // 2. Verify CreateAsync rethrows non-serial DbUpdateException instead of converting to 409 Conflict
        var dbName = $"drone-err-test-{Guid.NewGuid()}";
        var baseOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        await using (var seedDb = new AppDbContext(baseOptions))
        {
            var role = new Role { Id = 1, Code = AppRoles.Admin, Name = "Admin" };
            seedDb.Roles.Add(role);
            seedDb.Users.Add(new User
            {
                Id = admin.Id,
                RoleId = role.Id,
                Email = "admin-err@example.com",
                PasswordHash = "hash",
                FullName = "Admin",
                PhoneNumber = "0900000000",
                Status = "ACTIVE"
            });
            await seedDb.SaveChangesAsync();
        }

        var throwingOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .AddInterceptors(new FailingSaveChangesInterceptor(genericKeyError))
            .Options;

        await using var failingDb = new AppDbContext(throwingOptions);
        var service = new DroneService(failingDb);

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() =>
            service.CreateAsync(admin.Id, new CreateDroneRequest
            {
                SerialNumber = "DRN-ERR-0001",
                ModelName = "Falcon",
                MaxPayloadGram = 1500m,
                MaxFlightRangeKm = 15m,
                BatteryCapacityMah = 10000
            }));
        Assert.Same(genericKeyError, ex);
    }

    [Fact]
    public async Task Maintenance_concurrent_requests_detect_conflict()
    {
        var dispatcher = await NewUserAsync(AppRoles.Dispatcher);
        var serial = $"DRN-CONC-{Interlocked.Increment(ref _counter):D4}";
        var droneId = Guid.Empty;

        await _factory.WithDbAsync(async db =>
        {
            var drone = new Drone
            {
                SerialNumber = serial,
                ModelName = "Falcon Concurrent",
                MaxPayloadGram = 2000,
                MaxFlightRangeKm = 20,
                BatteryCapacityMah = 15000,
                Status = "IDLE"
            };
            db.Drones.Add(drone);
            await db.SaveChangesAsync();
            droneId = drone.Id;
        });

        var task1 = SendAsync(HttpMethod.Put, $"/api/drones/{droneId}/maintenance", dispatcher.Access, new
        {
            action = "START",
            expectedStatus = "IDLE",
            reason = "Routine inspection 1."
        });

        var task2 = SendAsync(HttpMethod.Put, $"/api/drones/{droneId}/maintenance", dispatcher.Access, new
        {
            action = "START",
            expectedStatus = "IDLE",
            reason = "Routine inspection 2."
        });

        var responses = await Task.WhenAll(task1, task2);
        var statusCodes = responses.Select(r => r.StatusCode).ToList();

        // Exactly one must succeed, the concurrent second must conflict
        Assert.Contains(HttpStatusCode.OK, statusCodes);
        Assert.Contains(HttpStatusCode.Conflict, statusCodes);
    }

    private sealed class FailingSaveChangesInterceptor(Exception exceptionToThrow) : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            throw exceptionToThrow;
        }
    }
}


/// <summary>LA-128: Real PostgreSQL verification for unique constraint violation and maintenance transaction.</summary>
public class DronePostgresTests : IClassFixture<PostgresApiFactory>, IDisposable
{
    private const string Password = "Passw0rd123";
    private static int _counter;
    private readonly PostgresApiFactory _factory;
    private readonly HttpClient _client;

    public DronePostgresTests(PostgresApiFactory factory)
    {
        _factory = factory;
        _client = PostgresApiFactory.ServerConnectionString is null ? new HttpClient() : factory.CreateClient();
    }

    public void Dispose() => _client.Dispose();

    private async Task<(Guid Id, string Access)> NewUserAsync(string roleCode)
    {
        var n = Interlocked.Increment(ref _counter);
        var email = $"pg-drone-{n}@example.com";
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
                FullName = $"Pg Drone User {n}",
                PhoneNumber = $"086{n:D7}",
                Status = "ACTIVE"
            });
            await db.SaveChangesAsync();
        });

        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (userId, auth.GetProperty("accessToken").GetString()!);
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

    [PostgresFact]
    public async Task Postgres_Create_duplicate_serial_number_concurrent_and_unique_constraint_returns_409()
    {
        var (_, adminToken) = await NewUserAsync(AppRoles.Admin);
        var serial = $"PG-DRN-{Interlocked.Increment(ref _counter):D4}";

        var t1 = SendAsync(HttpMethod.Post, "/api/drones", adminToken, new
        {
            serialNumber = serial,
            modelName = "Pg Carrier 1",
            maxPayloadGram = 2000m,
            maxFlightRangeKm = 25m,
            batteryCapacityMah = 16000
        });

        var t2 = SendAsync(HttpMethod.Post, "/api/drones", adminToken, new
        {
            serialNumber = serial,
            modelName = "Pg Carrier 2",
            maxPayloadGram = 2000m,
            maxFlightRangeKm = 25m,
            batteryCapacityMah = 16000
        });

        var responses = await Task.WhenAll(t1, t2);
        var codes = responses.Select(r => r.StatusCode).ToList();

        Assert.Contains(HttpStatusCode.Created, codes);
        Assert.Contains(HttpStatusCode.Conflict, codes);

        await _factory.WithDbAsync(async db =>
        {
            var count = await db.Drones.CountAsync(d => d.SerialNumber == serial);
            Assert.Equal(1, count);
        });
    }

    [PostgresFact]
    public async Task Postgres_Other_database_constraint_error_is_rethrown_and_not_masked_as_duplicate_serial()
    {
        var (adminId, _) = await NewUserAsync(AppRoles.Admin);
        var n = Interlocked.Increment(ref _counter);
        var station = new LandingStation
        {
            Id = Guid.NewGuid(),
            StationCode = $"PG-FK-STN-{n:D4}",
            Name = $"FK Station {n}",
            Address = "Addr",
            Latitude = 10.7m,
            Longitude = 106.7m,
            TotalSlots = 1,
            Status = "OPERATIONAL"
        };

        await _factory.WithDbAsync(async db =>
        {
            db.LandingStations.Add(station);
            await db.SaveChangesAsync();
        });

        // Interceptor deletes the station right before DroneService saves the new drone,
        // causing PostgreSQL to raise a real 23503 foreign_key_violation ("violates foreign key constraint FK_drones_landing_stations_CurrentStationId").
        // Even though the Postgres error message contains "key", DroneService MUST NOT mask it as 409 duplicate serial!
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_factory.DatabaseConnectionString)
            .AddInterceptors(new DeleteStationBeforeSaveInterceptor(_factory.DatabaseConnectionString, station.Id))
            .Options;

        await using var dbWithInterceptor = new AppDbContext(options);
        var droneService = new DroneService(dbWithInterceptor);

        var dbEx = await Assert.ThrowsAsync<DbUpdateException>(() =>
            droneService.CreateAsync(adminId, new CreateDroneRequest
            {
                SerialNumber = $"PG-FK-DRN-{n:D4}",
                ModelName = "Carrier FK",
                MaxPayloadGram = 2000m,
                MaxFlightRangeKm = 25m,
                BatteryCapacityMah = 16000,
                CurrentStationId = station.Id
            }));

        var pgEx = Assert.IsType<PostgresException>(dbEx.InnerException);
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, pgEx.SqlState);
    }

    [PostgresFact]
    public async Task Postgres_Concurrent_maintenance_start_serializes_and_rejects_second_with_409()
    {
        var (_, dispToken) = await NewUserAsync(AppRoles.Dispatcher);
        var serial = $"PG-DRN-CONC-{Interlocked.Increment(ref _counter):D4}";
        var droneId = Guid.Empty;

        await _factory.WithDbAsync(async db =>
        {
            var drone = new Drone
            {
                SerialNumber = serial,
                ModelName = "Pg Carrier Concurrent",
                MaxPayloadGram = 2000,
                MaxFlightRangeKm = 25,
                BatteryCapacityMah = 16000,
                Status = "IDLE"
            };
            db.Drones.Add(drone);
            await db.SaveChangesAsync();
            droneId = drone.Id;
        });

        var t1 = SendAsync(HttpMethod.Put, $"/api/drones/{droneId}/maintenance", dispToken, new
        {
            action = "START",
            expectedStatus = "IDLE",
            reason = "Routine inspection 1"
        });

        var t2 = SendAsync(HttpMethod.Put, $"/api/drones/{droneId}/maintenance", dispToken, new
        {
            action = "START",
            expectedStatus = "IDLE",
            reason = "Routine inspection 2"
        });

        var responses = await Task.WhenAll(t1, t2);
        var codes = responses.Select(r => r.StatusCode).ToList();

        Assert.Contains(HttpStatusCode.OK, codes);
        Assert.Contains(HttpStatusCode.Conflict, codes);

        await _factory.WithDbAsync(async db =>
        {
            var drone = await db.Drones.SingleAsync(d => d.Id == droneId);
            Assert.Equal("MAINTENANCE", drone.Status);
        });
    }

    private sealed class DeleteStationBeforeSaveInterceptor(string connectionString, Guid stationId) : SaveChangesInterceptor
    {
        private bool _deleted;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (!_deleted)
            {
                _deleted = true;
                await using var conn = new NpgsqlConnection(connectionString);
                await conn.OpenAsync(cancellationToken);
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "DELETE FROM landing_stations WHERE \"Id\" = @id";
                cmd.Parameters.AddWithValue("id", stationId);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
