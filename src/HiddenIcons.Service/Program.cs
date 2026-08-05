using HiddenIcons.Core;
using HiddenIcons.Service;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options => options.ServiceName = "Hidden Icons Service");
builder.Services.AddSingleton<ConfigStore>();
builder.Services.AddSingleton<ProcessSupervisor>();
builder.Services.AddHostedService<Worker>();
await builder.Build().RunAsync();
