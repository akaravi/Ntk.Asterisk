using Ntk.AsterNet.ARI;
using Ntk.AsterNet.ARI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asterisk.Console.ARI
{
    internal class SimpleTestApplication
    {
        public AriClient ActionClient;
        private void Main(string[] args)
        {
            try
            {
                // Create a new Ari Connection
                ActionClient = new AriClient(
                    new StasisEndpoint("192.168.3.201", 8088, "test", "test"),
                    "HelloWorld");

                // Hook into required events
                ActionClient.OnStasisStartEvent += c_OnStasisStartEvent;
                ActionClient.OnChannelDtmfReceivedEvent += ActionClientOnChannelDtmfReceivedEvent;
                ActionClient.OnConnectionStateChanged += ActionClientOnConnectionStateChanged;

                ActionClient.Connect();

                System.Console.ReadKey();
            }
            catch (Exception ex)
            {
                System.Console.WriteLine(ex.ToString());
                System.Console.ReadKey();
            }
        }

        private  void ActionClientOnConnectionStateChanged(object sender)
        {
            System.Console.WriteLine("Connection state is now {0}", ActionClient.Connected);
        }

        private void ActionClientOnChannelDtmfReceivedEvent(IAriClient sender, ChannelDtmfReceivedEvent e)
        {
            // When DTMF received
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
            // Answer the channel
            sender.Channels.Answer(e.Channel.Id);

            // Play an announcement
            sender.Channels.Play(e.Channel.Id, "sound:hello-world");
        }
    }
}