using Hangfire;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RiposteOS.Infrastructure.Documents;
using RiposteOS.Infrastructure.Persistence;
using RiposteOS.Infrastructure.Persistence.Configurations;

namespace RiposteOS.Tests.TestSupport;

public sealed class PostgreSqlWebApplicationFactory(string connectionString)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<RiposteDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<RiposteDbContext>>();
            services.RemoveAll<RiposteDbContext>();
            services.AddDbContext<RiposteDbContext>(options => options.UseNpgsql(
                connectionString,
                npgsql => npgsql.UseVector().MigrationsHistoryTable(
                    "__EFMigrationsHistory",
                    DatabaseSchemas.Infrastructure)));
            services.RemoveAll<IBackgroundJobClient>();
            services.AddSingleton<IBackgroundJobClient, RecordingBackgroundJobClient>();
            services.RemoveAll<IRecurringJobManager>();
            services.AddSingleton<IRecurringJobManager, RecordingRecurringJobManager>();
            services.RemoveAll<IObjectStorage>();
            services.AddSingleton<IObjectStorage, TestObjectStorage>();
        });
    }
}
