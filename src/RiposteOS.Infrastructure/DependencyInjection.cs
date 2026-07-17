using System.Net;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Hangfire;
using Hangfire.PostgreSql;
using Gridify;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RiposteOS.Core.Sourcing;
using RiposteOS.Core.Documents;
using RiposteOS.Infrastructure.Documents;
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
        services.AddOptions<ObjectStorageOptions>()
            .Bind(configuration.GetSection(ObjectStorageOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.BucketName) && options.BucketName.Length <= 63, "ObjectStorage:BucketName is required and must not exceed 63 characters.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Region), "ObjectStorage:Region is required.")
            .Validate(options => string.IsNullOrWhiteSpace(options.ServiceUrl) || Uri.TryCreate(options.ServiceUrl, UriKind.Absolute, out _), "ObjectStorage:ServiceUrl must be an absolute URL.")
            .Validate(options => string.IsNullOrWhiteSpace(options.AccessKey) == string.IsNullOrWhiteSpace(options.SecretKey), "ObjectStorage credentials must be configured together.")
            .Validate(options => options.MaxDocumentSizeBytes is > 0 and <= StoredDocument.MaximumSize, "ObjectStorage:MaxDocumentSizeBytes is invalid.")
            .ValidateOnStart();
        services.AddSingleton<IAmazonS3>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ObjectStorageOptions>>().Value;
            var config = new AmazonS3Config
            {
                RegionEndpoint = RegionEndpoint.GetBySystemName(options.Region),
                ForcePathStyle = options.ForcePathStyle,
                Timeout = TimeSpan.FromSeconds(30),
                MaxErrorRetry = 3,
            };
            if (!string.IsNullOrWhiteSpace(options.ServiceUrl))
            {
                config.ServiceURL = options.ServiceUrl;
            }

            return string.IsNullOrWhiteSpace(options.AccessKey)
                ? new AmazonS3Client(config)
                : new AmazonS3Client(new BasicAWSCredentials(options.AccessKey, options.SecretKey), config);
        });
        services.AddScoped<IObjectStorage, S3ObjectStorage>();
        services.AddScoped<DocumentsFacade>();
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
        services.AddOptions<TedOptions>()
            .Bind(configuration.GetSection(TedOptions.SectionName))
            .Validate(options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _), "Ted:BaseUrl must be an absolute URL.")
            .Validate(options => options.InitialLookbackDays is >= 0 and <= 365, "Ted:InitialLookbackDays must be between 0 and 365.")
            .Validate(options => options.OverlapDays is >= 0 and <= 30, "Ted:OverlapDays must be between 0 and 30.")
            .ValidateOnStart();
        services.AddHttpClient<TedSource>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<TedOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        }).AddStandardResilienceHandler();
        services.AddScoped<IOpportunitySource>(serviceProvider =>
            serviceProvider.GetRequiredService<TedSource>());
        services.AddOptions<PlaceOptions>()
            .Bind(configuration.GetSection(PlaceOptions.SectionName))
            .Validate(options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _), "Place:BaseUrl must be an absolute URL.")
            .Validate(options => options.InitialLookbackDays is >= 0 and <= 365, "Place:InitialLookbackDays must be between 0 and 365.")
            .Validate(options => options.OverlapDays is >= 0 and <= 30, "Place:OverlapDays must be between 0 and 30.")
            .Validate(options => options.RequestDelayMilliseconds is >= 0 and <= 5_000, "Place:RequestDelayMilliseconds must be between 0 and 5000.")
            .ValidateOnStart();
        services.AddHttpClient<PlaceSource>((serviceProvider, client) =>
        {
            var placeOptions = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<PlaceOptions>>().Value;
            client.BaseAddress = new Uri(placeOptions.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("RiposteOS/1.0 (+https://github.com/guillaumeroussel/RiposteOS)");
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.All,
            CookieContainer = new CookieContainer(),
            UseCookies = true,
        }).AddStandardResilienceHandler();
        services.AddScoped<IOpportunitySource>(serviceProvider =>
            serviceProvider.GetRequiredService<PlaceSource>());
        services.AddScoped<OpportunityImporter>();
        services.AddScoped<SourcingImportJob>();
        services.AddScoped<ImportRunStore>();
        services.AddScoped<SourcingSettingsStore>();
        services.AddScoped<SourcingFacade>();
        services.AddScoped<SourcingSynchronizationJob>();
        services.AddOptions<SourcingSynchronizationOptions>()
            .Bind(configuration.GetSection(SourcingSynchronizationOptions.SectionName))
            .Validate(options => SourcingRecurringJobRegistrar.IsValidCron(options.Cron), "SourcingSynchronization:Cron must be a valid five-field cron expression.")
            .Validate(options => options.SuccessSlaHours is >= 1 and <= 168, "SourcingSynchronization:SuccessSlaHours must be between 1 and 168.")
            .ValidateOnStart();

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
        services.AddHostedService<SourcingRecurringJobRegistrar>();

        return services;
    }

    private static string GetConnectionString(IConfiguration configuration) =>
        configuration.GetConnectionString("Database")
        ?? throw new InvalidOperationException("Connection string 'Database' is missing.");
}
