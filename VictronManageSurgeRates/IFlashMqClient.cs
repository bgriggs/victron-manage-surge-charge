
namespace VictronManageSurgeRates;

public interface IFlashMqClient
{
    event Action<GeneratorState>? OnGeneratorStateReceived;
    event Action<bool>? OnHeartbeatReceived;
    event Action<string>? OnKeepAlivePublishCompleted;
    event Action<double>? OnSocReceived;
    event Action<InverterMode>? OnInverterModeReceived;
    double? Soc { get; }
    GeneratorState? GeneratorState { get; }
    InverterMode? InverterMode { get; }

    Task Connect(string ip, string deviceId, CancellationToken stoppingToken);
    Task OverrideInverterMode(InverterMode mode, CancellationToken stoppingToken = default);
}