using Ntk.AsterNet.ARI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Asterisk.Console.ARI
{
    internal class RecordingSample
    {
        public static AriClient ActionClient;
        private static void Main(string[] args)
        {
            try
            {
                // Create a new Ari Connection
                ActionClient = new AriClient(
                    new StasisEndpoint("127.0.0.1", 8088, "username", "test"),
                    "HelloWorld");

                ActionClient.Connect();

                // List Recordings
                var recordings = ActionClient.Recordings.ListStored();
                recordings.ForEach(x =>System. Console.WriteLine($"Recording Name: {x.Name}, {x.Format}"));

                // Download the first Recording
                var recording = recordings.First();
                System.Console.WriteLine($"Downloading recording {recording.Name}");
                using (var file = File.Create(Path.GetTempFileName()))
                {
                    var buffer = ActionClient.Recordings.GetStoredFile(recording.Name);
                    file.Write(buffer, 0, buffer.Length);

                    file.Flush();
                }

                System.Console.WriteLine("Press any key to close...");
                System.Console.ReadKey();
            }
            catch (Exception ex)
            {
                System.Console.WriteLine(ex.ToString());
                System.Console.ReadKey();
            }
        }
    }
}
