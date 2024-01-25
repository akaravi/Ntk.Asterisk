using Ntk.AsterNet.ARI.Models;
using Ntk.AsterNet.ARI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asterisk.Console.ARI
{
    /*
     * This simple example shows how to queue a playback, initiate a recording
     * and then play back that recording when it's been completed.
     *  
     * This example doesn't allow multiple calls, and is pretty much useless for
     * anything else than showing how to use Play and Record.
     * 
     * Once the recording has been played back, the process starts again.
     * 
     * The recording will wait for a '#' digit, 1 second of silence or cut off 
     * after 6 seconds of recording.
     */

    internal class SimpleRecordAndPlayback
    {
        public AriClient actionClient;
        public StasisEndpoint endPoint;

        public RecordingToChannel recording;

        public class RecordingToChannel
        {
            public LiveRecording Recording { get; set; }
            public Channel Channel { get; set; }
        }

        void Main(string[] args)
        {
            try
            {
                endPoint = new StasisEndpoint("ipaddress", 8088, "username", "password");

                // Create a message actionClient to receive events on
                actionClient = new AriClient(endPoint, "playrec_test");

                actionClient.OnStasisStartEvent += c_OnStasisStartEvent;
                actionClient.OnStasisEndEvent += c_OnStasisEndEvent;
                actionClient.OnRecordingFinishedEvent += ActionClientOnRecordingFinishedEvent;

                actionClient.Connect();

                bool done = false;
                while (!done)
                {
                    var lastKey = System.Console.ReadKey();
                    switch (lastKey.KeyChar.ToString())
                    {
                        case "*":
                            done = true;
                            break;
                    }
                }

                actionClient.Disconnect();
            }
            catch (Exception ex)
            {
                System.Console.WriteLine(ex.ToString());
                System.Console.ReadKey();
            }
        }

        void GetRecording(Channel c)
        {
            var playback = actionClient.Channels.Play(c.Id, "sound:vm-rec-name", "en", 0, 0, Guid.NewGuid().ToString()).Id;
            recording = new RecordingToChannel()
            {
                Recording = actionClient.Channels.Record(c.Id, "temp-recording", "wav", 6, 1, "overwrite", true, "#"),
                Channel = c
            };
        }

        void PlaybackRecording(Channel c)
        {
            var repeat = actionClient.Channels.Play(c.Id, "recording:temp-recording", "en", 0, 0, Guid.NewGuid().ToString()).Id;
        }

        void ActionClientOnRecordingFinishedEvent(object sender, RecordingFinishedEvent e)
        {
            if (e.Recording.Name != recording.Recording.Name) return;

            PlaybackRecording(recording.Channel);

            GetRecording(recording.Channel);
        }

        void c_OnStasisEndEvent(object sender, StasisEndEvent e)
        {
            // Delete recording
            actionClient.Recordings.DeleteStored("temp-recording");

            // hangup
            actionClient.Channels.Hangup(e.Channel.Id, "normal");
        }

        void c_OnStasisStartEvent(object sender, StasisStartEvent e)
        {
            // answer channel
            actionClient.Channels.Answer(e.Channel.Id);

            GetRecording(e.Channel);
        }
    }
}