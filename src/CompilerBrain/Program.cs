using CompilerBrain;
using Microsoft.Build.Locator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZLogger;

// ZLinq drop-in everything
[assembly: ZLinqDropInAttribute("", ZLinq.DropInGenerateTypes.Everything)]
[assembly: ZLinqDropInExternalExtension("", "System.Collections.Immutable.ImmutableArray`1", "ZLinq.Linq.FromImmutableArray`1")]

// Debugger.Launch(); // for DEBUGGING.

MSBuildLocator.RegisterDefaults();

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.SetMinimumLevel(LogLevel.Trace);
builder.Logging.AddZLoggerConsole(consoleLogOptions =>
{
    // Configure all logs to go to stderr
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services
    .AddSingleton<SessionMemory>()
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools([typeof(CSharpMcpServer)]);

await builder.Build().RunAsync();
