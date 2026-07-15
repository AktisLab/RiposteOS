using RiposteOS.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddBackgroundProcessing(builder.Configuration);

var host = builder.Build();
host.Run();
