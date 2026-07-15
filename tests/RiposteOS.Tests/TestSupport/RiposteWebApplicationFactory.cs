using Hangfire;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RiposteOS.Infrastructure.Persistence;

namespace RiposteOS.Tests.TestSupport;

public sealed class RiposteWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = Guid.NewGuid().ToString();

    public RecordingBackgroundJobClient Jobs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<RiposteDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<RiposteDbContext>>();
            services.RemoveAll<RiposteDbContext>();
            services.AddDbContext<RiposteDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));
            services.RemoveAll<IBackgroundJobClient>();
            services.AddSingleton<IBackgroundJobClient>(Jobs);
        });
    }

    public async Task ResetAsync()
    {
        Jobs.Reset();
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RiposteDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();
    }
}
