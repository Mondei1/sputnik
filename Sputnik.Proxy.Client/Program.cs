using System.Net.Sockets;
using System.Text;

namespace Sputnik.Proxy.Client;

internal class Program
{
    /// <summary>
    /// This key is required to authenticate against the proxy. Not doing so would result in my credit card being
    /// billed to personal insolvency.
    /// </summary>
    private static readonly byte[] PROXY_KEY = Encoding.ASCII.GetBytes("T6pSSaSjXU6uXJqMtrYSmyptAALqGmtk");

    enum TalkingStyle
    {
        Kind = 1,
        Rude = 2
    }

    static void Main(string[] args)
    {
        TalkingStyle talkingStyle = TalkingStyle.Rude;

        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write("[!!!] ");
        Console.WriteLine("Please read the following disclaimer before you proceed:");
        Console.WriteLine("==============================================================");
        Console.ForegroundColor = ConsoleColor.Blue;
        Console.WriteLine("\n1. Your data is transmitted in cleartext.");
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.WriteLine("Venera is not capable of encryption. Therefore, your data cannot be encrypted in transit and is " +
            "vulnerable to MITM attacks. Do not use it for sensitive information.");
        Console.ForegroundColor = ConsoleColor.Blue;
        Console.WriteLine("\n2. Your data is proxied.");
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.WriteLine("Venera is not capable of running local LLMs nor to make HTTPS requests. Therefore, your data is " +
            "sent to a TCP proxy hosted by Nicolas Klier. Your Sputnik dialogue is not logged by the proxy. The context is " +
            "kept in memory as long as your TCP connection is open. I do log the amount of spent tokens to keep track of " +
            "billing. It's free for you, not for me :p");
        Console.ForegroundColor = ConsoleColor.Blue;
        Console.WriteLine("\n3. Your data is processed by OpenRouter.");
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.WriteLine("The proxy forwards your requests to OpenRouter.ai. OpenRouter anonymises your request and " +
            "forwards it to the current cheapest AI provider. OpenRouter itself does not log your requests but some " +
            "providers might. Therefore, avoid personal data. The privacy policy of OpenRouter applies: " +
            "https://openrouter.ai/privacy");
        Console.ForegroundColor = ConsoleColor.Blue;
        Console.WriteLine("\n3. AI hallucinates content.");
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.WriteLine("Take everything with a grain of salt. Depending on the preset you choose next, it may insult you, " +
            "wish you dead or instruct you on how to build a bomb. Do not take its answers seriously and have fun.");

        while (true)
        {
            Console.Write("\nType 'y' if you agree, or 'n' to exit and not use Sputnik: ");
            ConsoleKeyInfo key = Console.ReadKey();
            Console.WriteLine();

            if (key.KeyChar == 'n')
            {
                Environment.Exit(0);
            }
            else if (key.KeyChar == 'y')
            {
                Console.Clear();
                Console.WriteLine("- Disclaimer accpeted.\n");
                break;
            }
        }

        Console.WriteLine("How would you like Sputnik to talk to you?");
        Console.WriteLine("==============================================================");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("1) Helpful, kind and to the point.");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("   This is the classic experience.");
        Console.ForegroundColor = ConsoleColor.DarkRed;
        Console.WriteLine("2) Rude, never helpful and incredibly bad at insulting.");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("   Refusals are unlikely and your questions might remain unanswered.");

        while (true)
        {
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write("\nType '1' or '2' to set Sputnik's style: ");
            ConsoleKeyInfo key = Console.ReadKey();
            Console.WriteLine();

            if (int.TryParse(key.KeyChar.ToString(), out int i))
            {
                if (i >= 1 && i <= 2)
                {
                    talkingStyle = (TalkingStyle)i;
                    Console.Clear();
                    break;
                }
            }
        }

        TcpClient client = new();

        NetworkStream stream;
        try
        {
            client.Connect("127.0.0.1", 9999);
            client.ReceiveBufferSize = 256;
            client.SendBufferSize = 256;
            stream = client.GetStream();

            stream.Write(PROXY_KEY, 0, PROXY_KEY.Length);
            byte[] result = new byte[1];
            stream.ReadExactly(result, 0, 1);

            Console.WriteLine($"Auth response: {result[0]}");
        }
        catch (Exception e)
        {
            Environment.Exit(1);
            return;
        }

        Console.WriteLine("To exit this conversation write \"exit\" or \"quit\".\n");

        while (true)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("> ");

            string prompt = Console.ReadLine()!.Trim();

            if (prompt.ToLower() == "quit" || prompt.ToLower() == "exit")
            {
                break;
            }

            byte[] dataToSend = Encoding.ASCII.GetBytes(prompt);
            byte[] metadata = [((byte)talkingStyle)];

            stream.Write(metadata.Concat(dataToSend).ToArray(), 0, metadata.Length + dataToSend.Length);

            bool eof = false;

            Console.ForegroundColor = ConsoleColor.White;
            while (true)
            {
                byte[] receivedData = new byte[client.ReceiveBufferSize];
                int bytesRead = stream.Read(receivedData, 0, receivedData.Length);

                for (int i = 0; i < client.ReceiveBufferSize - 2; i++)
                {
                    byte b1 = receivedData[i];
                    byte b2 = receivedData[i + 1];
                    byte b3 = receivedData[i + 2];
                    if (b1 == 'E' && b2 == 'O' && b3 == 'F')
                    {
                        eof = true;
                        break;
                    }
                }

                if (eof)
                    break;

                string receivedMessage = Encoding.ASCII.GetString(receivedData, 0, bytesRead);

                Console.Write(receivedMessage);

            }

            Console.WriteLine();
        }

        stream.Close();
    }
}
