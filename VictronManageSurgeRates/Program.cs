using BigMission.TestHelpers;
using BigMission.TestHelpers.Delay;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Diagnostics;
using NLog.Extensions.Logging;

namespace VictronManageSurgeRates;

internal class Program
{
    static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddLogging(loggingBuilder =>
        {
            loggingBuilder.ClearProviders();
            loggingBuilder.AddNLog();
        });

        builder.Services.AddSingleton<IAsyncDelay, AsyncDelay>();
        builder.Services.AddSingleton<IDateTimeHelper, DateTimeHelper>();
        builder.Services.AddSingleton<IMqttNetLogger, MqttGhettoOneOffLogger>();
        builder.Services.AddSingleton(s =>
        {
            var ghettoLogger = s.GetRequiredService<IMqttNetLogger>();
            var mqttFactory = new MqttFactory(ghettoLogger);
            return mqttFactory.CreateMqttClient();
        });
        builder.Services.AddSingleton<IFlashMqClient, FlashMqClient>();
        builder.Services.AddHostedService(s => (FlashMqClient)s.GetRequiredService<IFlashMqClient>());
        builder.Services.AddHostedService<Application>();

        using IHost host = builder.Build();
        var loggerFactory = host.Services.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger(typeof(Program).GetType().Name);

        logger.LogInformation("Starting application RunAsync...");
        await host.RunAsync();
    }
}
