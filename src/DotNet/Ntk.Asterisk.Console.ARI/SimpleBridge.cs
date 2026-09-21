using System;
using System.Net.Http;
using Ntk.AsterNet.ARI;
using Ntk.AsterNet.ARI.Models;

namespace Ntk.Asterisk.Console.ARI { internal class SimpleBridge2
{
    public AriClient? ActionClient;
    public Bridge? simpleBridge;

    private const string AppName = "bridge_test";

    public void Run(string host = "127.0.0.1", int port = 8088, string username = "dev", string password = "test")
    {
        try
        {
            ActionClient = new AriClient(new StasisEndpoint(host, port, username, password), AppName);

            ActionClient.OnStasisStartEvent += c_OnStasisStartEvent;
            ActionClient.OnStasisEndEvent += c_OnStasisEndEvent;

            ActionClient.Connect();

            simpleBridge = ActionClient.Bridges.Create("mixing", Guid.NewGuid().ToString(), AppName);
            ActionClient.Applications.Subscribe(AppName, "bridge:" + simpleBridge.Id);
            ActionClient.Bridges.StartMoh(simpleBridge.Id, "default");

            if (!System.Console.IsInputRedirected && Environment.UserInteractive)
            {
                var done = false;
                while (!done)
                {
                    var lastKey = System.Console.ReadKey();
                    switch (lastKey.KeyChar.ToString())
                    {
                        case "*":
                            done = true;
                            break;
                        case "1":
                            ActionClient.Bridges.StopMoh(simpleBridge.Id);
                            break;
                        case "2":
                            ActionClient.Bridges.StartMoh(simpleBridge.Id, "default");
                            break;
                        case "3":
                            var bridgeMute = ActionClient.Bridges.Get(simpleBridge.Id);
                            foreach (var chan in bridgeMute.Channels)
                                ActionClient.Channels.Mute(chan, "in");
                            break;
                        case "4":
                            var bridgeUnmute = ActionClient.Bridges.Get(simpleBridge.Id);
                            foreach (var chan in bridgeUnmute.Channels)
                                ActionClient.Channels.Unmute(chan, "in");
                            break;
                    }
                }
            }

            ActionClient.Bridges.Destroy(simpleBridge.Id);
            ActionClient.Disconnect();
        }
        catch (HttpRequestException ex)
        {
            System.Console.WriteLine($"[SimpleBridge] Connection failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"[SimpleBridge] Error: {ex.Message}");
        }
        finally
        {
            Program.SafeWait();
        }
    }

    private void c_OnStasisEndEvent(object sender, StasisEndEvent e)
    {
        if (simpleBridge != null && ActionClient != null)
        {
            ActionClient.Bridges.RemoveChannel(simpleBridge.Id, e.Channel.Id);
            ActionClient.Channels.Hangup(e.Channel.Id, "normal");
        }
    }

    private void c_OnStasisStartEvent(IAriClient sender, StasisStartEvent e)
    {
        sender.Channels.Answer(e.Channel.Id);
        if (simpleBridge != null)
        {
            sender.Bridges.AddChannel(simpleBridge.Id, e.Channel.Id, "member");
        }
    }
} }
