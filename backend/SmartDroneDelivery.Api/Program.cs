using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.Middleware;
using SmartDroneDelivery.Api.Options;
using SmartDroneDelivery.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("Missing Jwt configuration section.");
if (Encoding.UTF8.GetByteCount(jwt.Key) < 32)
    throw new InvalidOperationException("Jwt:Key must be at least 32 bytes long.");

builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<ILandingStationService, LandingStationService>();
builder.Services.AddScoped<ICustomerAddressService, CustomerAddressService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.Configure<MinioOptions>(builder.Configuration.GetSection(MinioOptions.SectionName));
builder.Services.AddSingleton<IAvatarStorage, MinioAvatarStorage>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddSingleton<IPackageStorage, MinioPackageStorage>();
builder.Services.AddScoped<IPackageService, PackageService>();
builder.Services.AddScoped<IDeliveryOrderService, DeliveryOrderService>();
builder.Services.AddScoped<IDeliveryOrderQueryService, DeliveryOrderQueryService>();
builder.Services.AddScoped<IOrderTransitionWriter, OrderTransitionWriter>();
builder.Services.AddScoped<IOrderReviewService, OrderReviewService>();
builder.Services.AddScoped<ISchedulingService, SchedulingService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<FailureService>();
builder.Services.AddScoped<DeliveryRouteEstimate>();
builder.Services.AddOptions<DeliveryFlightOptions>()
    .Bind(builder.Configuration.GetSection(DeliveryFlightOptions.SectionName))
    .Validate(o => double.IsFinite(o.CruiseSpeedMps) && o.CruiseSpeedMps > 0,
        "DeliveryFlight:CruiseSpeedMps must be finite and greater than zero.")
    .Validate(o => o.CruiseSpeedMps >= 0.001,
        "DeliveryFlight:CruiseSpeedMps must be at least 0.001.")
    .Validate(o => o.CruiseSpeedMps <= 999.99,
        "DeliveryFlight:CruiseSpeedMps must be at most 999.99.").ValidateOnStart();
builder.Services.AddOptions<DroneSimulatorOptions>()
    .Bind(builder.Configuration.GetSection(DroneSimulatorOptions.SectionName))
    .Validate(o => double.IsFinite(o.TickIntervalSeconds) && o.TickIntervalSeconds >= 0.001 && o.TickIntervalSeconds <= (uint.MaxValue - 1d) / 1000,
        "DroneSimulator:TickIntervalSeconds must be between 0.001 and 4294967.294 seconds.")
    .Validate(o => o.AltitudeMeters >= 0 && o.AltitudeMeters <= 9999.99m,
        "DroneSimulator:AltitudeMeters must be between 0 and 9999.99.")
    .Validate(o => o.HasValidScenarios(), "DroneSimulator scenario offsets must be finite, between 0 and 86400 seconds; telemetry loss requires both offset and a positive duration at most 86400 seconds.")
    .ValidateOnStart();
builder.Services.AddScoped<DroneSimulatorService>();
builder.Services.AddHostedService<DroneSimulatorWorker>();
builder.Services.AddScoped<IStationSlotService, StationSlotService>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Keep the claim names of the token ("sub", "role") instead of the legacy .NET mapping.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "sub",
            RoleClaimType = "role"
        };
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                await ApiProblemDetails.WriteAsync(context.HttpContext, StatusCodes.Status401Unauthorized,
                    "Missing, invalid or expired token.");
            },
            OnForbidden = context => ApiProblemDetails.WriteAsync(context.HttpContext, StatusCodes.Status403Forbidden,
                "You do not have permission to perform this action.")
        };
    });
// Secure by default: an endpoint without [Authorize] or [AllowAnonymous] still needs a signed-in user.
builder.Services.AddAuthorization(options =>
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

builder.Services.AddScoped<IDeliveryOrderMutationService, DeliveryOrderMutationService>();
builder.Services.AddScoped<IStationOperationService, StationOperationService>();
builder.Services.Configure<PickupTimeoutOptions>(builder.Configuration.GetSection(PickupTimeoutOptions.SectionName));
builder.Services.AddScoped<IPickupTimeoutService, PickupTimeoutService>();
builder.Services.AddHostedService<PickupTimeoutWorker>();
builder.Services.AddScoped<IStationAvailabilityService, StationAvailabilityService>();

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
        options.InvalidModelStateResponseFactory = ApiProblemDetails.FromModelState);
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddScoped<IDroneService, DroneService>();
builder.Services.AddScoped<IFlightTelemetryService, FlightTelemetryService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the access token returned by /api/auth/login."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Seed data
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        await SeedData.InitializeAsync(services);

        // Demo stations and accounts: Development only, and only when Seed:DevelopmentData is on (LA-325).
        if (app.Environment.IsDevelopment() && app.Configuration.GetValue<bool>("Seed:DevelopmentData"))
            await SeedData.SeedDevelopmentDataAsync(services);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseMiddleware<ApiExceptionMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program;
