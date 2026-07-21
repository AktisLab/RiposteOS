using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Pgvector.EntityFrameworkCore;
using RiposteOS.Infrastructure.Persistence.Configurations;

namespace RiposteOS.Infrastructure.Persistence;

public sealed class RiposteDbContextFactory : IDesignTimeDbContextFactory<RiposteDbContext>
{
    public RiposteDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Database")
            ?? "Host=localhost;Database=riposteos;Username=riposteos;Password=riposteos";
        var options = new DbContextOptionsBuilder<RiposteDbContext>()
            .UseNpgsql(
                connectionString,
                npgsql => npgsql.UseVector().MigrationsHistoryTable(
                    "__EFMigrationsHistory",
                    DatabaseSchemas.Infrastructure))
            .Options;

        return new RiposteDbContext(options);
    }
}
