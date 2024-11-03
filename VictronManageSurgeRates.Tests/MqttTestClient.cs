using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Diagnostics;

namespace VictronManageSurgeRates.Tests;

internal class MqttTestClient : IMqttClient
{
    public bool IsConnected { get; set; }

    public MqttClientOptions Options => throw new NotImplementedException();

    public event Func<MqttApplicationMessageReceivedEventArgs, Task>? ApplicationMessageReceivedAsync;
    public event Func<MqttClientConnectedEventArgs, Task>? ConnectedAsync;
    public event Func<MqttClientConnectingEventArgs, Task>? ConnectingAsync;
    public event Func<MqttClientDisconnectedEventArgs, Task>? DisconnectedAsync;
    public event Func<InspectMqttPacketEventArgs, Task>? InspectPacketAsync;

    public int ConnectAsyncCalls { get; private set; }
    public Task<MqttClientConnectResult> ConnectAsync(MqttClientOptions options, CancellationToken cancellationToken = default)
    {
        ConnectAsyncCalls++;
        return Task.FromResult(new MqttClientConnectResult());
    }

    public Task DisconnectAsync(MqttClientDisconnectOptions options, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public Task PingAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public int PublishAsyncCalls { get; private set; }
    public Task<MqttClientPublishResult> PublishAsync(MqttApplicationMessage applicationMessage, CancellationToken cancellationToken = default)
    {
        PublishAsyncCalls++;
        return Task.FromResult(new MqttClientPublishResult(0, MqttClientPublishReasonCode.Success, string.Empty, []));
    }

    public Task SendExtendedAuthenticationExchangeDataAsync(MqttExtendedAuthenticationExchangeData data, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public int SubscribeAsyncCalls { get; private set; }
    public Task<MqttClientSubscribeResult> SubscribeAsync(MqttClientSubscribeOptions options, CancellationToken cancellationToken = default)
    {
        SubscribeAsyncCalls++;
        return Task.FromResult(new MqttClientSubscribeResult(0, [], string.Empty, []));
    }

    public Task<MqttClientUnsubscribeResult> UnsubscribeAsync(MqttClientUnsubscribeOptions options, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
