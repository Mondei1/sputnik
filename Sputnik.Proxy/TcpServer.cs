using Sputnik.Proxy.Crypto;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Sputnik.Proxy;

public class TcpServer
{
    private TcpListener _listener;
    private OpenRouter _openRouter;
    private Obfuscator _obfuscator;
    private bool _isRunning;
    private readonly ConcurrentDictionary<TcpClient, Task> _clients = new ConcurrentDictionary<TcpClient, Task>();

    public TcpServer(string ipAddress, int port)
    {
        _listener = new(IPAddress.Parse(ipAddress), port);
        _openRouter = new("mistralai/mixtral-8x7b-instruct");
        _obfuscator = new("aZ0mnhg4GslstQHZc5Hz4KBqNOqZqF4H");
    }

    public void Start()
    {
        _listener.Start();
        _isRunning = true;
        Console.WriteLine($"Server started ...");

        Task.Run(() => AcceptClientsAsync());
    }

    private async Task AcceptClientsAsync()
    {
        while (_isRunning)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync();
                Console.WriteLine("Client connected...");

                // Handle each client in a separate task
                var clientTask = HandleClientAsync(client);
                _clients.TryAdd(client, clientTask);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error accepting client: {ex.Message}");
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        NetworkStream stream = client.GetStream();
        byte[] buffer = new byte[256];

        try
        {
            while (_isRunning && client.Connected)
            {
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0) break; // Client disconnected

                // Convert data received from the client
                string message = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                Console.Write("[");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("Req");
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine($"] {message}");

                DateTime startTime = DateTime.Now;
                await foreach (var str in _openRouter.Prompt(message))
                {
                    byte[] response = Encoding.ASCII.GetBytes(str);

                    // Obfuscate our response.
                    byte[] obfus = _obfuscator.Obfuscate(response);

                    await stream.WriteAsync(obfus, 0, obfus.Length);
                }
                DateTime endTime = DateTime.Now;
                TimeSpan duration = endTime - startTime;

                var cost = _openRouter.CalculatePrice();
                Console.Write("[");
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.Write("Res");
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine($"] Completed request in {duration.ToString(@"fff")}ms | {_openRouter.LastUsage.TotalTokens} tokens processed => ~{cost.Item1 + cost.Item2} €");

                // Send null-terminator to end stream
                await stream.WriteAsync(_obfuscator.Obfuscate([((byte)'E'), ((byte)'O'), ((byte)'F')]), 0, 3);

            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling client: {ex.Message}");
        }
        finally
        {
            client.Close();
            _clients.TryRemove(client, out _);
            Console.WriteLine("Client disconnected...");
        }
    }

    public void Stop()
    {
        _isRunning = false;
        _listener.Stop();

        // Wait for all clients to disconnect
        Task.WhenAll(_clients.Values).Wait();
        Console.WriteLine("Server stopped.");
    }
}
