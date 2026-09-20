using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using Ntk.AsterNet.ARI;

namespace Asterisk.Console.ARI
{
    internal class RecordingSample
    {
        public static AriClient? ActionClient;

        public static void Run(string host = "127.0.0.1", int port = 8088, string username = "username", string password = "test")
        {
            try
            {
                // Create a new Ari Connection
                ActionClient = new AriClient(
                    new StasisEndpoint(host, port, username, password),
                    "HelloWorld");

                ActionClient.Connect();

                // List Recordings
                var recordings = ActionClient.Recordings.ListStored();
                recordings?.ForEach(x => System.Console.WriteLine($"Recording Name: {x.Name}, {x.Format}"));

                // Download the first Recording
                var recording = recordings?.FirstOrDefault();
                if (recording != null)
                {
                    System.Console.WriteLine($"Downloading recording {recording.Name}");
                    using (var file = File.Create(Path.GetTempFileName()))
                    {
                        var buffer = ActionClient.Recordings.GetStoredFile(recording.Name);
                        file.Write(buffer, 0, buffer.Length);
                        file.Flush();
                    }
                }

                Program.SafeWait();
            }
            catch (HttpRequestException ex)
            {
                System.Console.WriteLine($"[RecordingSample] Connection failed: {ex.Message}");
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[RecordingSample] Error: {ex.Message}");
            }
        }
    }
}
