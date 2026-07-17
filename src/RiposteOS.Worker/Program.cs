using RiposteOS.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddBackgroundProcessing();

var host = builder.Build();
host.Run();
