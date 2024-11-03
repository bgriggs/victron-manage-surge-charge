using MQTTnet.Client;
using MQTTnet.Packets;
using MQTTnet.Protocol;
using MQTTnet;
using MQTTnet.Server;
using MQTTnet.Extensions.TopicTemplate;
using System.Text.Json;
using System.Text;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace VictronManageSurgeRates;

internal class Program
{
    static readonly MqttTopicTemplate socTemplate = new("N/{id}/vebus/276/Soc");
    static readonly MqttTopicTemplate hbTemplate = new("N/{id}/heartbeat");
    static readonly MqttTopicTemplate publishCompletedTemplate = new("N/{id}/full_publish_completed");
    static readonly string deviceId = "102c6b644fe6";

    static async Task Main(string[] args)
    {
        Console.WriteLine("Hello, World!");

        var mqttFactory = new MqttFactory();

        using var mqttClient = mqttFactory.CreateMqttClient();
        var mqttClientOptions = new MqttClientOptionsBuilder().WithTcpServer("10.0.0.100").Build();

        mqttClient.ApplicationMessageReceivedAsync += e =>
        {
            //Console.WriteLine("Received application message.");

            var buff = e.ApplicationMessage.PayloadSegment.Array;
            if (buff != null)
            {
                var response = Encoding.UTF8.GetString(buff);
                dynamic? r = JsonConvert.DeserializeObject(response);
                if (r != null)
                {
                    if (e.ApplicationMessage.Topic.EndsWith("full_publish_completed"))
                    {
                        Console.WriteLine($"full_publish_completed: {r["full-publish-completed-echo"]}");
                    }
                    else
                    {
                        Console.WriteLine($"{e.ApplicationMessage.Topic}: value = {r.value}"); //19.0
                    }
                }
                else
                {
                    Console.WriteLine($"RX: {response}");
                }
            }
            return Task.CompletedTask;
        };

        await mqttClient.ConnectAsync(mqttClientOptions, CancellationToken.None);
        var mqttSubscribeOptions = mqttFactory.CreateSubscribeOptionsBuilder().WithTopicTemplate(socTemplate.WithParameter("id", deviceId)).Build();
        await mqttClient.SubscribeAsync(mqttSubscribeOptions, CancellationToken.None);

        var subHb = mqttFactory.CreateSubscribeOptionsBuilder().WithTopicTemplate(hbTemplate.WithParameter("id", deviceId)).Build();
        await mqttClient.SubscribeAsync(subHb, CancellationToken.None);

        var subPublishCompleted = mqttFactory.CreateSubscribeOptionsBuilder().WithTopicTemplate(publishCompletedTemplate.WithParameter("id", deviceId)).Build();
        await mqttClient.SubscribeAsync(subPublishCompleted, CancellationToken.None);

        // Keep alive
        // https://github.com/victronenergy/dbus-flashmq/blob/master/README.md#keep-alive
        var instanceId = Guid.NewGuid().ToString();
        var optStr = "{ \"keepalive-options\" : [ {\"full-publish-completed-echo\": \"" + instanceId + "\" } ] }";
        var keepAlivePublish = new MqttApplicationMessageBuilder()
                .WithTopic($"R/{deviceId}/keepalive")
                .WithPayload(optStr)
                .Build();
        await mqttClient.PublishAsync(keepAlivePublish, CancellationToken.None);

        await Task.Delay(2000);

        int i = 0;
        while (i < 100)
        {
            optStr = "{ \"keepalive-options\" : [\"suppress-republish\"] }";
            var keepaliveSupress = new MqttApplicationMessageBuilder()
                    .WithTopic($"R/{deviceId}/keepalive")
                    .WithPayload(optStr)
                    .Build();
            await mqttClient.PublishAsync(keepaliveSupress, CancellationToken.None);
            i++;
            await Task.Delay(5000);
        }

        //var applicationMessage = new MqttApplicationMessageBuilder()
        //        .WithTopic($"R/{deviceId}/vebus/276/Soc")
        //        .WithPayload("")
        //        .Build();
        //await mqttClient.PublishAsync(applicationMessage, CancellationToken.None);


        Console.WriteLine("MQTT client subscribed to topic.");

        Console.WriteLine("Press enter to exit.");
        Console.ReadLine();

        // This will send the DISCONNECT packet. Calling _Dispose_ without DisconnectAsync the
        // connection is closed in a "not clean" way. See MQTT specification for more details.
        await mqttClient.DisconnectAsync(new MqttClientDisconnectOptionsBuilder().WithReason(MqttClientDisconnectOptionsReason.NormalDisconnection).Build());
    }
}
