using Hangfire;
using Hangfire.PostgreSql;
using Gridify;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RiposteOS.Core.Sourcing;
using RiposteOS.Infrastructure.Persistence;
using RiposteOS.Infrastructure.Persistence.Configurations;
using RiposteOS.Infrastructure.Sourcing;

namespace RiposteOS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = GetConnectionString(configuration);
        GridifyGlobalConfiguration.EnableEntityFrameworkCompatibilityLayer();

        services.AddDbContext<RiposteDbContext>(options => options.UseNpgsql(
            connectionString,
            npgsql => npgsql.MigrationsHistoryTable(
                "__EFMigrationsHistory",
                DatabaseSchemas.Infrastructure)));
        services
            .AddIdentityCore<IdentityUser<Guid>>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<RiposteDbContext>();
        services.AddSingleton(TimeProvider.System);
        services.AddOptions<BoampOptions>()
            .Bind(configuration.GetSection(BoampOptions.SectionName))
            .Validate(options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _), "Boamp:BaseUrl must be an absolute URL.")
            .Validate(options => options.InitialLookbackDays is >= 0 and <= 365, "Boamp:InitialLookbackDays must be between 0 and 365.")
            .Validate(options => options.OverlapDays is >= 0 and <= 30, "Boamp:OverlapDays must be between 0 and 30.")
            .ValidateOnStart();
        services.AddHttpClient<BoampSource>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<BoampOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        }).AddStandardResilienceHandler();
        services.AddScoped<IOpportunitySource>(serviceProvider =>
            serviceProvider.GetRequiredService<BoampSource>());
        services.AddScoped<OpportunityImporter>();
        services.AddScoped<SourcingImportJob>();
        services.AddScoped<ImportRunStore>();
        services.AddScoped<SourcingSettingsStore>();
        services.AddScoped<SourcingFacade>();

        services.AddHangfire(options => options.UsePostgreSqlStorage(
            storage => storage.UseNpgsqlConnection(connectionString),
            new PostgreSqlStorageOptions
            {
                SchemaName = DatabaseSchemas.Hangfire,
            }));

        return services;
    }

    public static IServiceCollection AddBackgroundProcessing(this IServiceCollection services)
    {
        services.AddHangfireServer();

        return services;
    }

    private static string GetConnectionString(IConfiguration configuration) =>
        configuration.GetConnectionString("Database")
        ?? throw new InvalidOperationException("Connection string 'Database' is missing.");
}
