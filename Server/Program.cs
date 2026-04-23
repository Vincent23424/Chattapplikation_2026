using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using ChattModels;

public class Program
{
    private const int Port = 5000;
    private static readonly List<ClientState> clients = new List<ClientState>();
    private static readonly object clientsLock = new object();
    private static readonly string logPath = Path.Combine(AppContext.BaseDirectory, "server_log.txt");
    // Allowed auth tokens (comma separated in ENV var AUTH_TOKEN)
    private static readonly string[] AllowedTokens = (Environment.GetEnvironmentVariable("AUTH_TOKEN") ?? string.Empty)
        .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
        .Select(s => s.Trim())
        .Where(s => !string.IsNullOrEmpty(s))
        .ToArray();

    public static async Task Main(string[] args)
    {
        var listener = new TcpListener(IPAddress.Any, Port);
        listener.Start();
        Console.WriteLine($"Server started on port {Port}. Waiting for clients...");

        var acceptTask = Task.Run(async () =>
        {
            while (true)
            {
                var tcp = await listener.AcceptTcpClientAsync();
                // create a persistent writer for broadcasting
                var writer = new StreamWriter(tcp.GetStream(), new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true) { AutoFlush = true };
                var state = new ClientState { Client = tcp, Writer = writer };
                lock (clientsLock)
                {
                    clients.Add(state);
                }
                Console.WriteLine($"Client connected: {tcp.Client.RemoteEndPoint}");
                _ = HandleClientAsync(state);
            }
        });

        Console.WriteLine("Press Ctrl+C to stop the server.");
        var done = new ManualResetEventSlim(false);
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            done.Set();
        };
        done.Wait();

        Console.WriteLine("Stopping server...");
        listener.Stop();
        lock (clientsLock)
        {
            foreach (var c in clients)
            {
                try { c.Writer.Dispose(); } catch { }
                try { c.Client.Close(); } catch { }
            }
            clients.Clear();
        }
    }

    private static async Task HandleClientAsync(ClientState state)
    {
        var tcpClient = state.Client;
        var endpoint = tcpClient.Client.RemoteEndPoint?.ToString() ?? "unknown";
        try
        {
            using var stream = tcpClient.GetStream();
            using var reader = new StreamReader(stream, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true);

            // Require authentication as first message (AuthMessage) within 10s if tokens configured
            if (AllowedTokens.Length > 0)
            {
                var authLineTask = reader.ReadLineAsync();
                var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var completed = await Task.WhenAny(authLineTask, Task.Delay(Timeout.Infinite, timeoutCts.Token));
                if (completed != authLineTask)
                {
                    // timeout
                    Console.WriteLine($"Auth timeout from {endpoint}");
                    return;
                }
                var authLine = await authLineTask;
                if (string.IsNullOrWhiteSpace(authLine))
                {
                    Console.WriteLine($"Empty auth from {endpoint}");
                    return;
                }
                var authMsg = MessageBase.FromJson(authLine) as AuthMessage;
                if (authMsg == null || string.IsNullOrEmpty(authMsg.Token) || !AllowedTokens.Contains(authMsg.Token))
                {
                    Console.WriteLine($"Authentication failed from {endpoint}");
                    // send failure system message then close
                    try { using var w = new StreamWriter(stream, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true) { AutoFlush = true }; w.WriteLine(new SystemMessage("AuthFailed"){ Sender = "Server" }.ToJson()); } catch { }
                    return;
                }
                // mark client authenticated
                state.IsAuthenticated = true;
                try { using var w = new StreamWriter(stream, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true) { AutoFlush = true }; w.WriteLine(new SystemMessage("AuthSuccess"){ Sender = "Server" }.ToJson()); } catch { }
            }

            while (true)
            {
                var line = await reader.ReadLineAsync();
                if (line == null) break; // client disconnected

                MessageBase? message = null;
                try
                {
                    message = MessageBase.FromJson(line);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to parse message from {endpoint}: {ex.Message}");
                    continue;
                }

                if (message == null)
                {
                    Console.WriteLine($"Received null/empty message from {endpoint}");
                    continue;
                }

                // Log to file as JSON line
                try
                {
                    await File.AppendAllTextAsync(logPath, message.ToJson() + Environment.NewLine);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to write log: {ex.Message}");
                }

                // Write richer console output depending on message type
                switch (message)
                {
                    case TextMessage tm:
                        Console.WriteLine($"{tm.Timestamp.ToLocalTime():yyyy-MM-dd HH:mm:ss} (Text) {tm.Sender}: {tm.Content}");
                        break;
                    case PrivateMessage pm:
                        Console.WriteLine($"{pm.Timestamp.ToLocalTime():yyyy-MM-dd HH:mm:ss} (Private) {pm.Sender} -> {pm.Recipient}: {pm.Content}");
                        break;
                    case SystemMessage sm:
                        Console.WriteLine($"{sm.Timestamp.ToLocalTime():yyyy-MM-dd HH:mm:ss} (System): {sm.Action}");
                        break;
                    default:
                        Console.WriteLine(message.ToString());
                        break;
                }

                // Broadcast to all clients
                List<ClientState> snapshot;
                lock (clientsLock)
                {
                    snapshot = clients.ToList();
                }

                var payload = message.ToJson();
                foreach (var clientState in snapshot)
                {
                    try
                    {
                        if (!clientState.Client.Connected) continue;
                        lock (clientState.WriteLock)
                        {
                            clientState.Writer.WriteLine(payload);
                        }
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            var remote = clientState.Client.Client.RemoteEndPoint;
                            Console.WriteLine($"Failed to send to client {remote}: {ex.Message}");
                        }
                        catch
                        {
                            Console.WriteLine($"Failed to send to a client: {ex.Message}");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling client {endpoint}: {ex.Message}");
        }
        finally
        {
            Console.WriteLine($"Client disconnected: {endpoint}");
            lock (clientsLock)
            {
                var toRemove = clients.FirstOrDefault(c => c.Client == tcpClient);
                if (toRemove != null)
                {
                    try { toRemove.Writer.Dispose(); } catch { }
                    clients.Remove(toRemove);
                }
            }
            try { tcpClient.Close(); } catch { }
        }
    }

    // Maintain client states with persistent writers to avoid disposing streams
    private class ClientState
    {
        // mark as required because instances are created with object initializers elsewhere
        public required TcpClient Client { get; init; }
        public required StreamWriter Writer { get; init; }
        public bool IsAuthenticated { get; set; }
        public object WriteLock { get; } = new object();
    }
}
