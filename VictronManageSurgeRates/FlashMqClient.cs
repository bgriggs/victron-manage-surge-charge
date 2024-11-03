using BigMission.TestHelpers.Delay;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.TopicTemplate;
using Newtonsoft.Json;
using System.Text;

namespace VictronManageSurgeRates;

public class FlashMqClient : BackgroundService, IFlashMqClient
{
    private ILogger Logger { get; }

    private readonly IMqttClient mqtt;
    private readonly IAsyncDelay asyncDelay;
    private static readonly MqttTopicTemplate socTemplate = new("N/{id}/vebus/276/Soc");
    private static readonly MqttTopicTemplate hbTemplate = new("N/{id}/heartbeat");
    private static readonly MqttTopicTemplate generatorTemplate = new("N/{id}/generator/0/State");
    private static readonly MqttTopicTemplate inverterModeTemplate = new("N/{id}/vebus/276/Mode");
    private static readonly MqttTopicTemplate publishCompletedTemplate = new("N/{id}/full_publish_completed");
    private string? ip;
    private string? deviceId;
    private CancellationToken stoppingToken;
    private readonly string instanceId = Guid.NewGuid().ToString();
    protected bool publishCompleted = false;

    public event Action<string>? OnKeepAlivePublishCompleted;
    public event Action<double>? OnSocReceived;
    public event Action<bool>? OnHeartbeatReceived;
    public event Action<GeneratorState>? OnGeneratorStateReceived;
    public event Action<InverterMode>? OnInverterModeReceived;

    public double? Soc { get; protected set; }
    public GeneratorState? GeneratorState { get; protected set; }
    public InverterMode? InverterMode { get; protected set; }


    public FlashMqClient(IMqttClient mqtt, ILoggerFactory loggerFactory, IAsyncDelay asyncDelay)
    {
        Logger = loggerFactory.CreateLogger(GetType().Name);
        this.mqtt = mqtt;
        this.asyncDelay = asyncDelay;
        mqtt.ApplicationMessageReceivedAsync += Mqtt_ApplicationMessageReceivedAsync;
        OnKeepAlivePublishCompleted += (string inst) => { publishCompleted = true; };
        OnSocReceived += (double soc) => { Soc = soc; };
        OnGeneratorStateReceived += (GeneratorState gen) => { GeneratorState = gen; };
        OnInverterModeReceived += (InverterMode mode) => { InverterMode = mode; };
    }

    public async Task Connect(string ip, string deviceId, CancellationToken stoppingToken)
    {
        this.ip = ip;
        this.deviceId = deviceId;
        this.stoppingToken = stoppingToken;

        Logger.LogInformation($"Connecting to {ip} device id {deviceId}...");
        var mqttClientOptions = new MqttClientOptionsBuilder().WithTcpServer(ip).Build();
        await mqtt.ConnectAsync(mqttClientOptions, stoppingToken);

        // Setup subscriptions
        var mqttFactory = new MqttFactory();
        var mqttSubscribeOptions = mqttFactory.CreateSubscribeOptionsBuilder().WithTopicTemplate(socTemplate.WithParameter("id", deviceId)).Build();
        await mqtt.SubscribeAsync(mqttSubscribeOptions, stoppingToken);

        var subHb = mqttFactory.CreateSubscribeOptionsBuilder().WithTopicTemplate(hbTemplate.WithParameter("id", deviceId)).Build();
        await mqtt.SubscribeAsync(subHb, stoppingToken);

        var subGen = mqttFactory.CreateSubscribeOptionsBuilder().WithTopicTemplate(generatorTemplate.WithParameter("id", deviceId)).Build();
        await mqtt.SubscribeAsync(subGen, stoppingToken);

        var subInverter = mqttFactory.CreateSubscribeOptionsBuilder().WithTopicTemplate(inverterModeTemplate.WithParameter("id", deviceId)).Build();
        await mqtt.SubscribeAsync(subInverter, stoppingToken);

        var subPublishCompleted = mqttFactory.CreateSubscribeOptionsBuilder().WithTopicTemplate(publishCompletedTemplate.WithParameter("id", deviceId)).Build();
        await mqtt.SubscribeAsync(subPublishCompleted, stoppingToken);

        // Keep alive - do full publish on initial connection to get all values
        // https://github.com/victronenergy/dbus-flashmq/blob/master/README.md#keep-alive
        var optStr = "{ \"keepalive-options\" : [ {\"full-publish-completed-echo\": \"" + instanceId + "\" } ] }";
        var keepAlivePublish = new MqttApplicationMessageBuilder()
                .WithTopic($"R/{deviceId}/keepalive")
                .WithPayload(optStr)
                .Build();
        await mqtt.PublishAsync(keepAlivePublish, stoppingToken);
    }

    private Task Mqtt_ApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs e)
    {
        try
        {
            var buff = e.ApplicationMessage.PayloadSegment.Array;
            if (buff != null)
            {
                var response = Encoding.UTF8.GetString(buff);
                dynamic? r = JsonConvert.DeserializeObject(response);
                if (r != null)
                {
                    if (e.ApplicationMessage.Topic.EndsWith("full_publish_completed"))
                    {
                        string echoInstance = r["full-publish-completed-echo"].ToString();
                        if (echoInstance == instanceId)
                        {
                            Logger.LogInformation($"full_publish_completed: {r["full-publish-completed-echo"]}. Starting supressed keep alive.");
                            OnKeepAlivePublishCompleted?.Invoke(echoInstance);
                        }
                        else
                        {
                            Logger.LogWarning($"full_publish_completed: {r["full-publish-completed-echo"]}. Ignoring as this is not this instance: {instanceId}.");
                        }
                    }
                    // State of charge
                    else if (e.ApplicationMessage.Topic.EndsWith("Soc"))
                    {
                        double soc = r.value;
                        Logger.LogDebug($"SOC: {soc}");
                        OnSocReceived?.Invoke(soc);
                    }
                    // Heartbeat
                    else if (e.ApplicationMessage.Topic.EndsWith("heartbeat"))
                    {
                        bool hb = r.value;
                        Logger.LogDebug($"Heartbeat: {hb}");
                        OnHeartbeatReceived?.Invoke(hb);
                    }
                    // Generator state
                    else if (e.ApplicationMessage.Topic.EndsWith("generator/0/State"))
                    {
                        int gen = r.value;
                        var genState = (GeneratorState)gen;
                        Logger.LogDebug($"Generator: {genState}");
                        OnGeneratorStateReceived?.Invoke(genState);
                    }
                    // Inverter mode
                    else if (e.ApplicationMessage.Topic.EndsWith("vebus/276/Mode"))
                    {
                        int mode = r.value;
                        var inverterMode = (InverterMode)mode;
                        Logger.LogDebug($"Inverter Mode: {inverterMode}");
                        OnInverterModeReceived?.Invoke(inverterMode);
                    }
                    else
                    {
                        Logger.LogDebug($"{e.ApplicationMessage.Topic}: value = {r.value}"); //19.0
                    }
                }
                else
                {
                    Logger.LogTrace($"RX: {response}");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in FlashMqClient::Mqtt_ApplicationMessageReceivedAsync");
        }
        return Task.CompletedTask;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await asyncDelay.Delay(TimeSpan.FromSeconds(3), stoppingToken); // Wait for the host to be fully started
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!mqtt.IsConnected)
                {
                    // Reset on a disconnect so we send full publish again
                    publishCompleted = false;

                    if (ip != null && deviceId != null)
                    {
                        Logger.LogWarning("Disconnected from MQTT server. Reconnecting...");
                        await Connect(ip!, deviceId!, stoppingToken);
                    }
                    await asyncDelay.Delay(TimeSpan.FromSeconds(3), stoppingToken);
                    continue;
                }

                if (!publishCompleted)
                {
                    await asyncDelay.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                    continue;
                }

                // Send keep alive at least every 60 seconds
                Logger.LogDebug("Sending keep-alive message...");
                var optStr = "{ \"keepalive-options\" : [\"suppress-republish\"] }";
                var keepaliveSupress = new MqttApplicationMessageBuilder()
                        .WithTopic($"R/{deviceId}/keepalive")
                        .WithPayload(optStr)
                        .Build();
                await mqtt.PublishAsync(keepaliveSupress, stoppingToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error in FlashMqClient::ExecuteAsync");
            }
            await asyncDelay.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    /// <summary>
    /// Publishes the inverter mode sending it to the inverter.
    /// </summary>
    public async Task OverrideInverterMode(InverterMode mode, CancellationToken stoppingToken = default)
    {
        var optStr = "{ \"value\" :" + (int)mode + " }";
        var inverterModePublish = new MqttApplicationMessageBuilder()
                .WithTopic($"W/{deviceId}/vebus/276/Mode")
                .WithPayload(optStr)
                .Build();
        await mqtt.PublishAsync(inverterModePublish, stoppingToken);
    }
}
