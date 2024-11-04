using BigMission.TestHelpers;
using BigMission.TestHelpers.Delay;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Logging.EventLog;
using MQTTnet;
using MQTTnet.Diagnostics;
using NLog.Extensions.Logging;

namespace VictronManageSurgeRates;

internal class Program
{
    static async Task Main(string[] args)
    {
        try
        {
            var builder = Host.CreateApplicationBuilder(args);

            builder.Services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.ClearProviders();
                loggingBuilder.AddNLog();
            });
            //LoggerProviderOptions.RegisterProviderOptions<EventLogSettings, EventLogLoggerProvider>(builder.Services);

            builder.Services.AddWindowsService(options =>
            {
                options.ServiceName = "Manage Victron Surge Charges";
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
        catch (Exception ex)
        {
            Console.WriteLine($"Unhandled exception: {ex}");
        }
    }
}
