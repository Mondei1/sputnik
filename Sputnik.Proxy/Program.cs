using dotenv.net;
using dotenv.net.Utilities;
using System.Text;

namespace Sputnik.Proxy
{
    internal class Program
    {
        public static string OR_API_KEY = "";
        public static bool DEBUG = false;

        static async Task Main(string[] args)
        {
            DotEnv.Load(options: new(probeForEnv: true));

            IDictionary<string, string> settings = DotEnv.Read();
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            if (EnvReader.TryGetBooleanValue("DEBUG", out bool debugLogging))
            {
                Logging.LogInfo("Debug logging enabled.");
            }
            else
            {
                Logging.LogInfo("Debug logging disabled.");
            }
            DEBUG = debugLogging;

            if (!EnvReader.TryGetStringValue("OPENROUTER_API_KEY", out string apiKey))
            {
                Console.WriteLine("OpenRouter API key is required to run this proxy. Make sure you provide the OPENROUTER_API_KEY environment variable.");
                Environment.Exit(1);
            }

            if (!EnvReader.TryGetStringValue("PROXY_PSK", out string psk))
            {
                Console.WriteLine("You must provide a pre-shared-secret. Make sure you provide the PROXY_PSK environment variable with a 32-byte random string.");
                Environment.Exit(1);
            }

            byte[] pskBytes = Encoding.ASCII.GetBytes(psk);

            if (pskBytes.Length != 32)
            {
                Console.WriteLine($"Your set PROXY_PSK is {pskBytes.Length}-bytes long but 32-bytes were expected. Make sure you provide the PROXY_PSK environment variable with a 32-byte random string.");
                Environment.Exit(1);
            }

            OR_API_KEY = apiKey;

            TcpServer server = new("0.0.0.0", 9999, pskBytes);

            server.Start();

            await Task.Delay(-1);
        }
    }
}
