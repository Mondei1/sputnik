using Newtonsoft.Json;
using Sputnik.Proxy.Models;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Serialization;

namespace Sputnik.Proxy;

public class TcpServer
{
    private static readonly byte[] EOF = [((byte)'E'), ((byte)'O'), ((byte)'F')];

    private TcpListener _listener;
    private OpenRouter _openRouter;
    private bool _isRunning;
    private byte[] _psk;

    private readonly ConcurrentDictionary<TcpClient, Task> _clients = new();
    private readonly ConcurrentDictionary<TcpClient, List<GeneratedResponse>> _context = new();
    private readonly ConcurrentDictionary<TcpClient, string> _ids = new();

    public TcpServer(string ipAddress, int port, byte[] psk)
    {
        _listener = new(IPAddress.Parse(ipAddress), port);
        _openRouter = new();
        _psk = psk;
    }

    public void Start()
    {
        _listener.Start();
        _isRunning = true;
        Logging.LogInfo($"Server started on {_listener.LocalEndpoint} ...");

        Task.Run(() => AcceptClientsAsync());
    }

    private async Task AcceptClientsAsync()
    {
        while (_isRunning)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync();

                byte[] sentKey = new byte[_psk.Length];

                try
                {
                    await client.GetStream().ReadExactlyAsync(sentKey, 0, 32).AsTask().WaitAsync(TimeSpan.FromMilliseconds(342));

                    if (sentKey.SequenceEqual(_psk))
                    {
                        // Tell client to move on.
                        await client.GetStream().WriteAsync([1], 0, 1);
                    }
                    else
                    {
                        Logging.LogDebug($"Client {client.Client.RemoteEndPoint} submitted invalid key. Close ...");
                        client.Close();

                        break;
                    }
                }
                catch (TimeoutException)
                {
                    Logging.LogDebug($"Client {client.Client.RemoteEndPoint} failed to authenticate in time. Close ...");
                    client.Close();

                    break;
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
        byte[] rawBuffer = new byte[8192];

        try
        {
            while (_isRunning && client.Connected)
            {
                int bytesRead = await stream.ReadAsync(rawBuffer, 0, rawBuffer.Length);
                if (bytesRead == 0) break; // Client disconnected

                if (!_ids.TryGetValue(client, out string? clientId))
                {
                    // Unknown client, abort.
                    break;
                }

                // Extract data by reading length int.
                int dataLength = BitConverter.ToInt32(rawBuffer.Take(4).ToArray(), 0);
                byte[] buffer = new byte[dataLength];
                Array.Copy(rawBuffer, 4, buffer, 0, dataLength);

                // Parse metadata
                int talkingStyle = buffer[0];
                int jsonDataLength = BitConverter.ToInt32(buffer.Skip(1).Take(4).ToArray(), 0);
                string userInfoRaw = Encoding.ASCII.GetString(buffer, 5, jsonDataLength);
                VeneraUserInfo veneraUserInfo = JsonConvert.DeserializeObject<VeneraUserInfo>(userInfoRaw)!;

                // Convert data received from the client
                string message = string.Empty;
                message = Encoding.ASCII.GetString(buffer, 5 + jsonDataLength, dataLength - (5 + jsonDataLength)).Replace("\0", "");
                Logging.LogReqest($"Received prompt of {message.Length} characters.");

                if (!_context.TryGetValue(client, out List<GeneratedResponse> context))
                {
                    Logging.LogWarn($"Failed to retrieve context for {clientId}. Create new context ...");

                    context = new();
                    _context.TryAdd(client, context);
                }

                DateTime startTime = DateTime.Now;
                string generatedResponse = string.Empty;

                await foreach (string str in _openRouter.Prompt((TalkingStyle)talkingStyle, message, context, veneraUserInfo))
                {
                    //string str1 = str;
                    //.Replace("ä", "ae")
                    //.Replace("Ä", "Ae")
                    //.Replace("ö", "oe")
                    //.Replace("Ö", "Oe")
                    //.Replace("ü", "ue")
                    //.Replace("Ü", "Ue")
                    //.Replace("ß", "ss");

                    // If our trim removed all characters, don't waste any packets.
                    if (str.Length == 0)
                    {
                        continue;
                    }

                    generatedResponse += str;
                    byte[] response = Encoding.UTF8.GetBytes(str);

                    await stream.WriteAsync(response, 0, response.Length);
                }
                DateTime endTime = DateTime.Now;
                TimeSpan duration = endTime - startTime;

                var cost = _openRouter.CalculatePrice(_openRouter.GetModelByStyle((TalkingStyle)talkingStyle));

                Logging.LogResponse($"Completed request in {duration:s\\.fff}s | {_openRouter.LastUsage.TotalTokens} tokens processed => ~{cost.Item1 + cost.Item2} €");

                // Will only be logged during development, not during production.
                Logging.LogDebug($"Response from LLM: \"{generatedResponse}\"");

                context.Add(new() { UserPrompt = message, Response = generatedResponse });
                Logging.LogDebug($"Add {generatedResponse.Length + message.Length} characters to context.");

                // Send null-terminator to end stream
                await stream.WriteAsync(EOF, 0, 3);

            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling client: {ex.Message}");
            byte[] errorMessage = Encoding.ASCII.GetBytes($"Sputnik proxy encountered an {ex.GetType().ToString()} exception.");
            await stream.WriteAsync([
                .. errorMessage,
                .. EOF
            ], 0, errorMessage.Length + 3);
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
