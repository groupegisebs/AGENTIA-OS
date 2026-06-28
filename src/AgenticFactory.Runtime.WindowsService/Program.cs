using AgenticFactory.Infrastructure;
using AgenticFactory.Runtime.WindowsService;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddAgenticInfrastructure(builder.Configuration);
builder.Services.AddHostedService<RuntimeWorker>();
builder.Services.AddWindowsService(options => options.ServiceName = "AgenticFactoryRuntime");

var host = builder.Build();
host.Run();
