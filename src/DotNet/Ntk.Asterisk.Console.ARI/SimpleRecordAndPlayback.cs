using System;
using System.IO;
using System.Net.Http;
using Ntk.AsterNet.ARI;
using Ntk.AsterNet.ARI.Models;

namespace Ntk.Asterisk.Console.ARI { internal class SimpleRecordAndPlayback
{
    public AriClient? actionClient;
    public StasisEndpoint? endPoint;
    public RecordingToChannel? recording;

    public class RecordingToChannel
    {
        public LiveRecording? Recording { get; set; }
        public Channel? Channel { get; set; }
    }

    public void Run(string host = "127.0.0.1", int port = 8088, string username = "username", string password = "password")
    {
        try
        {
            endPoint = new StasisEndpoint(host, port, username, password);
            actionClient = new AriClient(endPoint, "playrec_test");

            actionClient.OnStasisStartEvent += c_OnStasisStartEvent;
            actionClient.OnStasisEndEvent += c_OnStasisEndEvent;
            actionClient.OnRecordingFinishedEvent += ActionClientOnRecordingFinishedEvent;

            actionClient.Connect();

            if (!System.Console.IsInputRedirected && Environment.UserInteractive)
            {
                bool done = false;
                while (!done)
                {
                    var lastKey = System.Console.ReadKey();
                    if (lastKey.KeyChar == '*')
                        done = true;
                }
            }

            actionClient.Disconnect();
        }
        catch (HttpRequestException ex)
        {
            System.Console.WriteLine($"[SimpleRecordAndPlayback] Connection failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"[SimpleRecordAndPlayback] Error: {ex.Message}");
        }
        finally
        {
            Program.SafeWait();
        }
    }

    private void GetRecording(Channel c)
    {
        if (actionClient == null) return;
        var playback = actionClient.Channels.Play(c.Id, "sound:vm-rec-name", "en", 0, 0, Guid.NewGuid().ToString()).Id;
        recording = new RecordingToChannel()
        {
            Recording = actionClient.Channels.Record(c.Id, "temp-recording", "wav", 6, 1, "overwrite", true, "#"),
            Channel = c
        };
    }

    private void PlaybackRecording(Channel c)
    {
        actionClient?.Channels.Play(c.Id, "recording:temp-recording", "en", 0, 0, Guid.NewGuid().ToString());
    }

    private void ActionClientOnRecordingFinishedEvent(IAriClient sender, RecordingFinishedEvent e)
    {
        if (recording != null && recording.Channel != null)
        {
            PlaybackRecording(recording.Channel);
        }
    }

    private void c_OnStasisEndEvent(IAriClient sender, StasisEndEvent e)
    {
        if (recording != null && recording.Channel != null && recording.Channel.Id == e.Channel.Id)
        {
            recording = null;
        }
    }

    private void c_OnStasisStartEvent(IAriClient sender, StasisStartEvent e)
    {
        sender.Channels.Answer(e.Channel.Id);
        GetRecording(e.Channel);
    }
} }
