using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

/// <summary>
/// Marks a test that needs a real PostgreSQL server (LA-322). The server comes from the environment variable
/// <see cref="PostgresApiFactory.ConnectionVariable"/>; without it the test is skipped, so a plain
/// <c>dotnet test</c> still works on a machine with no database.
/// </summary>
public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (PostgresApiFactory.ServerConnectionString is null)
            Skip = $"Set {PostgresApiFactory.ConnectionVariable} to a PostgreSQL server to run this test, " +
                   "for example: Host=localhost;Port=5433;Username=postgres;Password=Password123!";
    }
}

/// <summary>
/// Runs the real API against a real PostgreSQL database. Each factory creates its own empty database
/// (applied with the real migrations) on the server and drops it again on dispose.
/// The EF Core InMemory provider used by the other tests cannot check the conditional updates and
/// transactions that only run on a relational database.
/// </summary>
public sealed class PostgresApiFactory : WebApplicationFactory<Program>
{
    public const string ConnectionVariable = "TEST_POSTGRES_CONNECTION";

    /// <summary>Set to 1 to retain unique test databases for local inspection; default/CI still drops them.</summary>
    public const string KeepDatabasesVariable = "TEST_POSTGRES_KEEP_DATABASES";

    /// <summary>Connection string of the server (any database), or null when no server is configured.</summary>
    public static string? ServerConnectionString =>
        Environment.GetEnvironmentVariable(ConnectionVariable) is { Length: > 0 } value ? value : null;

    private readonly string _databaseName = $"la322_{Guid.NewGuid():N}";
    private bool _databaseCreated;

    /// <summary>Name of the database created for this factory.</summary>
    public string DatabaseName => _databaseName;

    /// <summary>Connection string of that database, for tests that need their own connection (for example to hold a row lock).</summary>
    public string DatabaseConnectionString =>
        new NpgsqlConnectionStringBuilder(ServerConnectionString ?? throw new InvalidOperationException($"{ConnectionVariable} is not set.")) { Database = _databaseName }.ConnectionString;

    public FakeAvatarStorage Avatars { get; } = new();
    public FakePackageStorage Packages { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var server = ServerConnectionString
            ?? throw new InvalidOperationException($"{ConnectionVariable} is not set.");
        var appConnection = new NpgsqlConnectionStringBuilder(server) { Database = _databaseName }.ConnectionString;
        CreateDatabase(server);

        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["Seed:DevelopmentData"] = "false" }));
        builder.ConfigureServices(services =>
        {
            foreach (var descriptor in services
                         .Where(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>)
                                     || d.ServiceType == typeof(DbContextOptions)).ToList())
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<AppDbContext>(options => options.UseNpgsql(appConnection));

            services.RemoveAll<IAvatarStorage>();
            services.AddSingleton<IAvatarStorage>(Avatars);
            services.RemoveAll<IPackageStorage>();
            services.AddSingleton<IPackageStorage>(Packages);
        });
    }

    public async Task WithDbAsync(Func<AppDbContext, Task> action)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await action(db);
    }

    private void CreateDatabase(string serverConnection)
    {
        using var connection = new NpgsqlConnection(serverConnection);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE \"{_databaseName}\"";
        command.ExecuteNonQuery();
        _databaseCreated = true;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing || !_databaseCreated || ServerConnectionString is not { } server) return;

        NpgsqlConnection.ClearAllPools();
        if (Environment.GetEnvironmentVariable(KeepDatabasesVariable) == "1")
        {
            _databaseCreated = false;
            return;
        }
        using var connection = new NpgsqlConnection(server);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)";
        command.ExecuteNonQuery();
        _databaseCreated = false;
    }
}
