using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Features;
using RiposteOS.Api.Consultations;
using RiposteOS.Api.Documents;
using RiposteOS.Api.Sourcing;
using RiposteOS.Infrastructure.Documents;
using RiposteOS.Infrastructure;
using RiposteOS.Infrastructure.Persistence;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.Configure<FormOptions>(options =>
    options.MultipartBodyLengthLimit = (builder.Configuration.GetValue<long?>("ObjectStorage:MaxDocumentSizeBytes") ?? 52_428_800) + 16_384);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<RiposteDbContext>("database", tags: ["ready"])
    .AddCheck<ObjectStorageHealthCheck>("object-storage", tags: ["ready"]);

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference("/docs");
}

app.MapGet("/", () => TypedResults.Ok(new
{
    service = "RiposteOS.Api",
    status = "ok",
}));

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
});
app.MapSourcingEndpoints();
app.MapDocumentsEndpoints();
app.MapConsultationsEndpoints();

app.Run();

public partial class Program;
