using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;

namespace Toletus.LiteNet3.Server;

public class LiteNet3WebSocket
{
    private const string ApiKey = "12345-abcde-67890-fghij";
    private const string WebSocketGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
    private const int HandshakeReadTimeoutMs = 10_000;
    private const int MaxHeaderBytes = 16 * 1024;

    // ESP32 requires this keepalive to stay connected — do not disable.
    private static readonly TimeSpan KeepAliveInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan KeepAliveTimeout = TimeSpan.FromSeconds(10);

    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Thread? _acceptThread;
    public string Log = string.Empty;

    private static readonly ConcurrentDictionary<string, bool> ActiveConnections = new();
    private static readonly ConcurrentDictionary<string, LiteNet3WebSocketBehavior> SerialToBehavior = new();
    private static readonly ConcurrentDictionary<string, object> SerialLocks = new();

    public Action<LiteNet3WebSocketBehavior>? OnNewBehavior;

    public void Start(string uri)
    {
        StopAndClearWebSocketServer();

        try
        {
            var uriObject = new Uri(uri);
            var address = IPAddress.TryParse(uriObject.Host, out var parsed) ? parsed : IPAddress.Any;

            _listener = new TcpListener(address, uriObject.Port);
            _listener.Start();

            _cts = new CancellationTokenSource();

            // Dedicated thread, never ThreadPool — a Task.Run-per-connection version starved a co-resident gRPC client.
            _acceptThread = new Thread(() => AcceptLoop(_cts.Token))
            {
                IsBackground = true,
                Name = "LiteNet3-Accept"
            };
            _acceptThread.Start();

            Log = $"Server started at {uri}";
        }
        catch (SocketException ex)
        {
            Console.WriteLine($"SocketException: {ex.Message}");
            Log = "Failed to start WebSocket server due to a listener issue.";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception: {ex.Message}");
            Log = "Failed to start WebSocket server due to an unexpected error.";
        }
    }

    private void AcceptLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = _listener!.AcceptTcpClient();
            }
            catch (SocketException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (InvalidOperationException) { break; }
            catch (Exception ex)
            {
                if (!ct.IsCancellationRequested)
                    Console.WriteLine($"Accept error: {ex.Message}");
                break;
            }

            var connectionThread = new Thread(() => HandleClient(client, ct))
            {
                IsBackground = true,
                Name = "LiteNet3-Conn"
            };
            connectionThread.Start();
        }
    }

    private void HandleClient(TcpClient tcpClient, CancellationToken serverToken)
    {
        WebSocket? ws = null;
        NetworkStream? stream = null;
        try
        {
            stream = tcpClient.GetStream();

            stream.ReadTimeout = HandshakeReadTimeoutMs;

            var headers = ReadHttpHeaders(stream);

            headers.TryGetValue("x-api-key", out var apiKey);
            headers.TryGetValue("Serial", out var serial);
            headers.TryGetValue("Sec-WebSocket-Key", out var wsKey);
            headers.TryGetValue("Upgrade", out var upgrade);
            headers.TryGetValue("Connection", out var connection);

            if (string.IsNullOrEmpty(serial) || string.IsNullOrEmpty(wsKey) || apiKey != ApiKey)
            {
                WriteHttpStatus(stream, "403 Forbidden");
                return;
            }

            var isUpgrade =
                string.Equals(upgrade?.Trim(), "websocket", StringComparison.OrdinalIgnoreCase) &&
                connection is not null &&
                connection.Contains("Upgrade", StringComparison.OrdinalIgnoreCase);
            if (!isUpgrade)
            {
                WriteHttpStatus(stream, "400 Bad Request");
                return;
            }

            var acceptKey = ComputeWebSocketAccept(wsKey);
            var response =
                "HTTP/1.1 101 Switching Protocols\r\n" +
                "Upgrade: websocket\r\n" +
                "Connection: Upgrade\r\n" +
                $"Sec-WebSocket-Accept: {acceptKey}\r\n\r\n";
            var responseBytes = Encoding.ASCII.GetBytes(response);
            stream.Write(responseBytes, 0, responseBytes.Length);

            stream.ReadTimeout = Timeout.Infinite;

            ws = WebSocket.CreateFromStream(stream, new WebSocketCreationOptions
            {
                IsServer = true,
                KeepAliveInterval = KeepAliveInterval,
                KeepAliveTimeout = KeepAliveTimeout,
            });

            var behavior = new LiteNet3WebSocketBehavior { LiteNet3WebSocket = this };
            OnNewBehavior?.Invoke(behavior);

            behavior.RunAsync(ws, serial, serverToken).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            if (!serverToken.IsCancellationRequested)
                Console.WriteLine($"Connection handling failed: {ex.Message}");
        }
        finally
        {
            ws?.Dispose();
            stream?.Dispose();
            tcpClient.Dispose();
        }
    }

    private static Dictionary<string, string> ReadHttpHeaders(NetworkStream stream)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var raw = new StringBuilder();
        var one = new byte[1];
        var total = 0;

        while (raw.Length < 4 ||
               raw[^1] != '\n' || raw[^2] != '\r' || raw[^3] != '\n' || raw[^4] != '\r')
        {
            var read = stream.Read(one, 0, 1);
            if (read == 0) throw new EndOfStreamException("Connection closed before handshake completed.");

            total += read;
            if (total > MaxHeaderBytes)
                throw new InvalidDataException($"HTTP headers exceeded {MaxHeaderBytes} bytes.");

            raw.Append((char)one[0]);
        }

        foreach (var line in raw.ToString().Split("\r\n").Skip(1))
        {
            var colon = line.IndexOf(':');
            if (colon > 0)
                headers[line[..colon].Trim()] = line[(colon + 1)..].Trim();
        }

        return headers;
    }

    private static void WriteHttpStatus(NetworkStream stream, string status)
    {
        try
        {
            var bytes = Encoding.ASCII.GetBytes($"HTTP/1.1 {status}\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
            stream.Write(bytes, 0, bytes.Length);
        }
        catch { /* ignore */ }
    }

    private static string ComputeWebSocketAccept(string key)
    {
        var hash = SHA1.HashData(Encoding.ASCII.GetBytes(key + WebSocketGuid));
        return Convert.ToBase64String(hash);
    }

    internal LiteNet3WebSocketBehavior? RegisterCurrentConnection(string serial, LiteNet3WebSocketBehavior behavior)
    {
        var lockObj = SerialLocks.GetOrAdd(serial, _ => new object());
        lock (lockObj)
        {
            SerialToBehavior.TryGetValue(serial, out var oldBehavior);
            SerialToBehavior[serial] = behavior;
            ActiveConnections.TryAdd(serial, true);

            return oldBehavior != behavior ? oldBehavior : null;
        }
    }

    internal bool TryClearCurrentConnection(string serial, Guid connectionId)
    {
        var lockObj = SerialLocks.GetOrAdd(serial, _ => new object());
        lock (lockObj)
        {
            if (!SerialToBehavior.TryGetValue(serial, out var currentBehavior))
                return false;

            if (currentBehavior.ConnectionId != connectionId)
                return false;

            SerialToBehavior.TryRemove(serial, out _);
            ActiveConnections.TryRemove(serial, out _);
            return true;
        }
    }

    public void StopAndClearWebSocketServer()
    {
        try
        {
            if (_listener != null)
            {
                _cts?.Cancel();
                _listener.Stop();
                _listener = null;
                _acceptThread = null;
                _cts?.Dispose();
                _cts = null;
            }

            ClearConnectionsOwnedByThisServer();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while stopping WebSocket server: {ex.Message}");
        }
    }

    private void ClearConnectionsOwnedByThisServer()
    {
        foreach (var pair in SerialToBehavior.ToArray())
        {
            if (!ReferenceEquals(pair.Value.LiteNet3WebSocket, this))
                continue;

            if (SerialToBehavior.TryRemove(pair.Key, out _))
                ActiveConnections.TryRemove(pair.Key, out _);
        }
    }
}
