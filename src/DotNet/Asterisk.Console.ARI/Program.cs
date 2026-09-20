using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using Ntk.AsterNet.ARI;

namespace Asterisk.Console.ARI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var host = Environment.GetEnvironmentVariable("ASTERISK_ARI_HOST") ?? "127.0.0.1";
            var portStr = Environment.GetEnvironmentVariable("ASTERISK_ARI_PORT");
            var port = int.TryParse(portStr, out var p) ? p : 8088;
            var user = Environment.GetEnvironmentVariable("ASTERISK_ARI_USER") ?? "admin";
            var password = Environment.GetEnvironmentVariable("ASTERISK_ARI_PASSWORD") ?? "test";
            var app = Environment.GetEnvironmentVariable("ASTERISK_ARI_APP") ?? "HelloWorld";

            System.Console.WriteLine($"[Asterisk.Console.ARI] Starting with endpoint {host}:{port}, app '{app}'...");

            try
            {
                var endpoint = new StasisEndpoint(host, port, user, password);
                var client = new AriClient(endpoint, app);

                System.Console.WriteLine($"[Asterisk.Console.ARI] Attempting connection to http://{host}:{port}/ari...");
                client.Connect();

                System.Console.WriteLine("[Asterisk.Console.ARI] Connected to ARI. Listing stored recordings...");
                var recordings = client.Recordings.ListStored();
                if (recordings != null && recordings.Count > 0)
                {
                    foreach (var rec in recordings)
                    {
                        System.Console.WriteLine($"  Recording: {rec.Name}, format: {rec.Format}");
                    }
                }
                else
                {
                    System.Console.WriteLine("  No stored recordings found.");
                }
            }
            catch (HttpRequestException ex) when (ex.InnerException is SocketException || ex.Message.Contains("actively refused"))
            {
                System.Console.WriteLine($"[Asterisk.Console.ARI] Warning: Could not connect to Asterisk ARI at {host}:{port}. (Connection refused / server offline)");
                System.Console.WriteLine("[Asterisk.Console.ARI] Verify Asterisk ARI is running on port " + port + " or set ASTERISK_ARI_HOST/ASTERISK_ARI_PORT.");
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[Asterisk.Console.ARI] Error: {ex.Message}");
            }
            finally
            {
                SafeWait();
            }
        }

        public static void SafeWait()
        {
            if (System.Console.IsInputRedirected || !Environment.UserInteractive)
            {
                return;
            }

            try
            {
                System.Console.WriteLine("Press any key to exit...");
                System.Console.ReadKey();
            }
            catch (InvalidOperationException)
            {
                // Console input is redirected or running in headless/daemon mode
            }
        }
    }
}
