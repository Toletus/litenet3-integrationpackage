using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;

namespace Toletus.LiteNet3.Server;

public class LiteNet3WebSocket
{
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    public string Log = string.Empty;

    private static readonly ConcurrentDictionary<string, bool> ActiveConnections = new();
    private static readonly ConcurrentDictionary<string, LiteNet3WebSocketBehavior> SerialToBehavior = new();

    public Action<LiteNet3WebSocketBehavior>? OnNewBehavior;

    public void Start(string uri)
    {
        StopAndClearWebSocketServer();

        try
        {
            var uriObject = new Uri(uri);

            _listener = new TcpListener(IPAddress.Any, uriObject.Port);
            _listener.Start();

            _cts = new CancellationTokenSource();
            _ = AcceptLoopAsync(_cts.Token);

            Log = $"Server started at {uri}";
            Console.WriteLine(Log);
        }
        catch (SocketException ex)
        {
            Console.WriteLine($"SocketException: {ex.Message}");
            Log = "Failed to start WebSocket server due to socket issue.";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception: {ex.Message}");
            Log = "Failed to start WebSocket server due to an unexpected error.";
        }
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener!.AcceptTcpClientAsync(ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                if (!ct.IsCancellationRequested)
                    Console.WriteLine($"Accept error: {ex.Message}");
                break;
            }

            _ = Task.Run(() => HandleClientAsync(client, ct), ct);
        }
    }

    private async Task HandleClientAsync(TcpClient tcpClient, CancellationToken serverCt)
    {
        using var client = tcpClient;
        var stream = client.GetStream();

        Dictionary<string, string> headers;
        try { headers = await ReadHttpHeadersAsync(stream); }
        catch { return; }

        headers.TryGetValue("x-api-key", out var apiKey);
        headers.TryGetValue("Serial", out var serial);
        headers.TryGetValue("Sec-WebSocket-Key", out var wsKey);

        if (string.IsNullOrEmpty(serial) || string.IsNullOrEmpty(wsKey) ||
            apiKey != "12345-abcde-67890-fghij")
        {
            await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 401 Unauthorized\r\nContent-Length: 0\r\n\r\n"));
            return;
        }

        var acceptKey = ComputeWebSocketAccept(wsKey);
        var response =
            $"HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: {acceptKey}\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(response));

        var ws = WebSocket.CreateFromStream(stream, new WebSocketCreationOptions
        {
            IsServer = true,
            KeepAliveInterval = TimeSpan.FromSeconds(30)
        });

        var behavior = new LiteNet3WebSocketBehavior { LiteNet3WebSocket = this };
        OnNewBehavior?.Invoke(behavior);
        await behavior.RunAsync(ws, serial, serverCt);
    }

    private static async Task<Dictionary<string, string>> ReadHttpHeadersAsync(NetworkStream stream)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var buffer = new byte[4096];
        var raw = new StringBuilder();

        while (!raw.ToString().Contains("\r\n\r\n"))
        {
            var read = await stream.ReadAsync(buffer);
            if (read == 0) throw new EndOfStreamException();
            raw.Append(Encoding.ASCII.GetString(buffer, 0, read));
        }

        foreach (var line in raw.ToString().Split("\r\n").Skip(1))
        {
            var colon = line.IndexOf(':');
            if (colon > 0)
                headers[line[..colon].Trim()] = line[(colon + 1)..].Trim();
        }

        return headers;
    }

    private static string ComputeWebSocketAccept(string key)
    {
        var hash = SHA1.HashData(Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"));
        return Convert.ToBase64String(hash);
    }

    public static void RegisterConnection(string serial)
    {
        Console.WriteLine(ActiveConnections.TryAdd(serial, true)
            ? $"Client {serial} registered. Total connections: {ActiveConnections.Count}"
            : $"Client {serial} reconnected. Total connections: {ActiveConnections.Count}");
    }

    public void UnregisterConnection(string serial)
    {
        ActiveConnections.TryRemove(serial, out _);
        Console.WriteLine($"Client {serial} unregistered. Remaining connections: {ActiveConnections.Count}");
    }

    internal static bool TryGetBehavior(string serial, out LiteNet3WebSocketBehavior? behavior) =>
        SerialToBehavior.TryGetValue(serial, out behavior);

    internal static void BindSerialToBehavior(string serial, LiteNet3WebSocketBehavior behavior) =>
        SerialToBehavior[serial] = behavior;

    internal static void UnbindSerial(string serial) =>
        SerialToBehavior.TryRemove(serial, out _);

    public void StopAndClearWebSocketServer()
    {
        try
        {
            if (_listener == null) return;

            Console.WriteLine("Stopping current WebSocket server...");
            _cts?.Cancel();
            _listener.Stop();
            _listener = null;
            _cts?.Dispose();
            _cts = null;

            ActiveConnections.Clear();
            SerialToBehavior.Clear();

            Console.WriteLine("WebSocket server stopped and resources cleared.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while stopping WebSocket server: {ex.Message}");
        }
    }
}