using System.Net.Sockets;
using System.Text;

namespace Sputnik.Proxy.Client;

internal class Program
{
    static void Main(string[] args)
    {
        TcpClient client = new TcpClient();

        NetworkStream stream;
        try
        {
            client.Connect("127.0.0.1", 9999);
            stream = client.GetStream();
        }
        catch (Exception e)
        {
            Environment.Exit(1);
            return;
        }

        Console.WriteLine("To exit this conversation write \"exit\" or \"quit\".\n");

        while (true)
        {
            Console.Write("> ");

            string prompt = Console.ReadLine().Trim();

            if (prompt.ToLower() == "quit" || prompt.ToLower() == "exit")
            {
                break;
            }

            byte[] dataToSend = Encoding.ASCII.GetBytes(prompt);
            stream.Write(dataToSend, 0, dataToSend.Length);

            bool eof = false;

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
