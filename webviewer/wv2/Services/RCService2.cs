using System.Diagnostics;

namespace Services
{
    public class RCService2 : RCService
    {
        HubConnection connection;
        string me = "APP_" + Environment.MachineName;
        class HubMessage
        {
            public string from;
            public string to;
            public string message;
            public string title;
            public string description;
        }
        public override void Start()
        {
            connection = new HubConnectionBuilder()
            .WithUrl($"{DataClient.GetBaseConfig().BaseUrl}/chatHub")
            .WithAutomaticReconnect()
            .Build();

            connection.Closed += async (error) =>
            {
                await Task.Delay(new Random().Next(0, 5) * 1000);
                await connection.StartAsync();
            };

            connection.On("ReceiveMessage", (Action<string, string>)((user, message) =>
            {
                Debug.WriteLine($"{user} {message}");
                var obj = message.FromJson<HubMessage>();
                var cmd = obj.message;

                if (obj.from.Contains("RC_"))
                {
                    if (obj.to == me)
                    {
                        // execute cmd
                        int ret = ExecuteCmd(cmd);
                        SendMsg(obj.from, $"ExecuteCmd({cmd}) return({ret})");
                        Debug.WriteLine($"execute: {cmd}, return: {ret}");
                    }
                }
            }));

            connection.Reconnecting += error =>
            {
                Debug.Assert(connection.State == HubConnectionState.Reconnecting);

                // Notify users the connection was lost and the client is reconnecting.
                // Start queuing or dropping messages.

                return Task.CompletedTask;
            };

            try
            {
                connection.StartAsync()
                    .ContinueWith(x =>
                    {
                        Debug.WriteLine("Connection started");
                        //connection.SendAsync($"SendMessage", me, connection.ConnectionId);
                        SendMsg("", $"JoinChat {connection.ConnectionId}");
                    });
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        private void SendMsg(string to, string msg)
        {
            connection.SendAsync($"SendMessage", me, new HubMessage { from = me, to = to, message = msg }.ToJson());
        }

        public override void Stop()
        {
            connection.StopAsync();
        }
    }
}
