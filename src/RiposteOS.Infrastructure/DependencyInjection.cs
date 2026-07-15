using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RiposteOS.Infrastructure.Persistence;

namespace RiposteOS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = GetConnectionString(configuration);

        services.AddDbContext<RiposteDbContext>(options => options.UseNpgsql(connectionString));
        services
            .AddIdentityCore<IdentityUser<Guid>>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<RiposteDbContext>();

        return services;
    }

    public static IServiceCollection AddBackgroundProcessing(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = GetConnectionString(configuration);

        services.AddHangfire(options => options.UsePostgreSqlStorage(storage =>
            storage.UseNpgsqlConnection(connectionString)));
        services.AddHangfireServer();

        return services;
    }

    private static string GetConnectionString(IConfiguration configuration) =>
        configuration.GetConnectionString("Database")
        ?? throw new InvalidOperationException("Connection string 'Database' is missing.");
}
