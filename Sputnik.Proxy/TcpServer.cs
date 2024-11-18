using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Sputnik.Proxy;

public class TcpServer
{
    /// <summary>
    /// This key is required to authenticate against the proxy. Not doing so would result in my credit card being
    /// billed to personal insolvency.
    /// </summary>
    private static readonly byte[] PROXY_KEY = Encoding.ASCII.GetBytes("T6pSSaSjXU6uXJqMtrYSmyptAALqGmtk");

    private TcpListener _listener;
    private OpenRouter _openRouter;
    private bool _isRunning;

    private readonly ConcurrentDictionary<TcpClient, Task> _clients = new();
    private readonly ConcurrentDictionary<TcpClient, List<GeneratedResponse>> _context = new();
    private readonly ConcurrentDictionary<TcpClient, string> _ids = new();

    public TcpServer(string ipAddress, int port)
    {
        _listener = new(IPAddress.Parse(ipAddress), port);
        _openRouter = new("x-ai/grok-beta");
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

                byte[] sentKey = new byte[PROXY_KEY.Length];

                try
                {
                    await client.GetStream().ReadExactlyAsync(sentKey, 0, 32).AsTask().WaitAsync(TimeSpan.FromSeconds(1));

                    if (sentKey.SequenceEqual(PROXY_KEY))
                    {
                        // Tell client to move on.
                        await client.GetStream().WriteAsync([1], 0, 1);
                    }
                    else
                    {
                        Logging.LogDebug($"Client {client.Client.RemoteEndPoint} submitted invalid key. Close ...");
                        client.Close();

                        continue;
                    }
                }
                catch (TimeoutException)
                {
                    Logging.LogDebug($"Client {client.Client.RemoteEndPoint} failed to authenticate in time. Close ...");
                    client.Close();

                    continue;
                }

                string newGuid = Guid.NewGuid().ToString();
                _ids.AddOrUpdate(client, newGuid, (key, oldValue) => newGuid);

                EndPoint? endpoint = client.Client.RemoteEndPoint as IPEndPoint;
                Logging.LogConnection($"Client {endpoint} (id: {newGuid}) connected.");

                if (_context.TryAdd(client, new()))
                {
                    Logging.LogDebug($"Created new context for {newGuid}");
                }
                else
                {
                    Logging.LogWarn($"Context already found for {newGuid}");
                }

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

                if (!_ids.TryGetValue(client, out string? clientId))
                {
                    // Unknown client, abort.
                    break;
                }

                // Parse metadata
                int talkingStyle = buffer[0];

                // Convert data received from the client
                string message = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                Logging.LogReqest($"Received user prompt of {message.Length} characters.");

                if (!_context.TryGetValue(client, out List<GeneratedResponse> context))
                {
                    Logging.LogWarn($"Failed to retrieve context for {clientId}. Create new context ...");

                    context = new();
                    _context.TryAdd(client, context);
                }

                DateTime startTime = DateTime.Now;
                string generatedResponse = "";

                await foreach (string str in _openRouter.Prompt((TalkingStyle)talkingStyle, message, context))
                {
                    generatedResponse += str;
                    byte[] response = Encoding.ASCII.GetBytes(str);

                    await stream.WriteAsync(response, 0, response.Length);
                }
                DateTime endTime = DateTime.Now;
                TimeSpan duration = endTime - startTime;

                var cost = _openRouter.CalculatePrice();

                Logging.LogResponse(($"Completed request in {duration.ToString(@"s\.fff")}s | {_openRouter.LastUsage.TotalTokens} tokens processed => ~{cost.Item1 + cost.Item2} €"));

                context.Add(new() { UserPrompt = message, Response = generatedResponse });
                Logging.LogDebug($"Add {generatedResponse.Length + message.Length} characters to context.");

                // Send null-terminator to end stream
                await stream.WriteAsync([((byte)'E'), ((byte)'O'), ((byte)'F')], 0, 3);

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
            _context.TryRemove(client, out _);

            _ids.TryGetValue(client, out string id);
            Logging.LogConnection($"Client {id ?? "unknown"} disconnected.");

            _ids.TryRemove(client, out _);
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
