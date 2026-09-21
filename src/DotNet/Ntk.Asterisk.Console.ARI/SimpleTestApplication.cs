using System;
using System.Net.Http;
using Ntk.AsterNet.ARI;
using Ntk.AsterNet.ARI.Models;

namespace Ntk.Asterisk.Console.ARI { internal class SimpleTestApplication
{
    public AriClient? ActionClient;

    public void Run(string host = "192.168.3.201", int port = 8088, string username = "test", string password = "test")
    {
        try
        {
            // Create a new Ari Connection
            ActionClient = new AriClient(
                new StasisEndpoint(host, port, username, password),
                "HelloWorld");

            // Hook into required events
            ActionClient.OnStasisStartEvent += c_OnStasisStartEvent;
            ActionClient.OnChannelDtmfReceivedEvent += ActionClientOnChannelDtmfReceivedEvent;
            ActionClient.OnConnectionStateChanged += ActionClientOnConnectionStateChanged;

            ActionClient.Connect();

            Program.SafeWait();
        }
        catch (HttpRequestException ex)
        {
            System.Console.WriteLine($"[SimpleTestApplication] Connection failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"[SimpleTestApplication] Error: {ex.Message}");
        }
    }

    private void ActionClientOnConnectionStateChanged(object sender)
    {
        System.Console.WriteLine("Connection state is now {0}", ActionClient?.Connected);
    }

    private void ActionClientOnChannelDtmfReceivedEvent(IAriClient sender, ChannelDtmfReceivedEvent e)
    {
        switch (e.Digit)
        {
            case "*":
                sender.Channels.Play(e.Channel.Id, "sound:asterisk-friend");
                break;
            case "#":
                sender.Channels.Play(e.Channel.Id, "sound:goodbye");
                sender.Channels.Hangup(e.Channel.Id, "normal");
                break;
            default:
                sender.Channels.Play(e.Channel.Id, string.Format("sound:digits/{0}", e.Digit));
                break;
        }
    }

    private void c_OnStasisStartEvent(IAriClient sender, StasisStartEvent e)
    {
        sender.Channels.Answer(e.Channel.Id);
        sender.Channels.Play(e.Channel.Id, "sound:hello-world");
    }
} }
