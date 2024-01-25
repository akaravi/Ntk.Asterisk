using Ntk.AsterNet.ARI.Models;
using Ntk.AsterNet.ARI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asterisk.Console.ARI
{
    internal class SimpleBridge2
    {
        public  AriClient ActionClient;
        public  Bridge simpleBridge;

        private const string AppName = "bridge_test";
        void Main()
        {
            try
            {
                // Create a message actionClient to receive events on
                ActionClient = new AriClient(new StasisEndpoint("127.0.0.1", 8088, "dev", "test"), AppName);

                ActionClient.OnStasisStartEvent += c_OnStasisStartEvent;
                ActionClient.OnStasisEndEvent += c_OnStasisEndEvent;

                ActionClient.Connect();

                // Create simple bridge
                simpleBridge = ActionClient.Bridges.Create("mixing", Guid.NewGuid().ToString(), AppName);

                // subscribe to bridge events
                ActionClient.Applications.Subscribe(AppName, "bridge:" + simpleBridge.Id);

                // start MOH on bridge
                ActionClient.Bridges.StartMoh(simpleBridge.Id, "default");

                var done = false;
                while (!done)
                {
                    var lastKey =System. Console.ReadKey();
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
                            // Mute all channels on bridge
                            var bridgeMute = ActionClient.Bridges.Get(simpleBridge.Id);
                            foreach (var chan in bridgeMute.Channels)
                                ActionClient.Channels.Mute(chan, "in");
                            break;
                        case "4":
                            // Unmute all channels on bridge
                            var bridgeUnmute = ActionClient.Bridges.Get(simpleBridge.Id);
                            foreach (var chan in bridgeUnmute.Channels)
                                ActionClient.Channels.Unmute(chan, "in");
                            break;
                    }
                }

                ActionClient.Bridges.Destroy(simpleBridge.Id);
                ActionClient.Disconnect();
            }
            catch (Exception ex)
            {
                System.Console.WriteLine(ex.ToString());
                System.Console.ReadKey();
            }
        }

         void c_OnStasisEndEvent(object sender, StasisEndEvent e)
        {
            // remove from bridge
            ActionClient.Bridges.RemoveChannel(simpleBridge.Id, e.Channel.Id);

            // hangup
            ActionClient.Channels.Hangup(e.Channel.Id, "normal");
        }

         void c_OnStasisStartEvent(object sender, StasisStartEvent e)
        {
            // answer channel
            ActionClient.Channels.Answer(e.Channel.Id);

            // add to bridge
            ActionClient.Bridges.AddChannel(simpleBridge.Id, e.Channel.Id, "member");
        }
    }


}
