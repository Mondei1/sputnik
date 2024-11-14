using dotenv.net;
using dotenv.net.Utilities;

namespace Sputnik.Proxy
{
    internal class Program
    {
        public static string OR_API_KEY = "";

        static void Main(string[] args)
        {
            DotEnv.Load(options: new(probeForEnv: true));

            IDictionary<string, string> settings = DotEnv.Read();
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            if (!EnvReader.TryGetStringValue("OPENROUTER_API_KEY", out string apiKey))
            {
                Console.WriteLine("OpenRouter API key is required to run this proxy. Make sure you provide the OPENROUTER_API_KEY environment variable.");
                Environment.Exit(1);
            }

            OR_API_KEY = apiKey;

            TcpServer server = new("0.0.0.0", 9999);

            server.Start();

            Console.WriteLine("Press ENTER to stop the server...");
            Console.ReadLine();

            server.Stop();
        }
    }
}
