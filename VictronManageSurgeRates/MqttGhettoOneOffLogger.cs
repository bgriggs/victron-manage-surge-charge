using Microsoft.Extensions.Logging;
using MQTTnet.Diagnostics;

namespace VictronManageSurgeRates;

internal class MqttGhettoOneOffLogger : IMqttNetLogger
{
    private ILogger Logger { get; }

    public bool IsEnabled => true;

    public MqttGhettoOneOffLogger(ILoggerFactory loggerFactory)
    {
        Logger = loggerFactory.CreateLogger(GetType().Name);
    }

    public void Publish(MqttNetLogLevel logLevel, string source, string message, object[] parameters, Exception exception)
    {
        switch (logLevel)
        {
            case MqttNetLogLevel.Error:
                Logger.LogError(exception, message, parameters);
                break;
            case MqttNetLogLevel.Warning:
                Logger.LogWarning(exception, message, parameters);
                break;
            case MqttNetLogLevel.Info:
                Logger.LogInformation(exception, message, parameters);
                break;
            case MqttNetLogLevel.Verbose:
                Logger.LogTrace(exception, message, parameters);
                break;
            default:
                Logger.LogDebug(exception, message, parameters);
                break;
        }
    }
}
