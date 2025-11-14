using System.Buffers;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using Microsoft.AspNetCore.Http;
using Timer = System.Timers.Timer;

#nullable enable

namespace Toletus.LiteNet3.Server;

public class LiteNet3AspNetWebSocketConnection
{
    private const int InactivityTimeout = 60000;
    private readonly LiteNet3AspNetWebSocket _server;
    private readonly string _serial;
    private readonly WebSocket _webSocket;
    private Timer? _inactivityTimer;

    private LiteNet3AspNetWebSocketConnection(LiteNet3AspNetWebSocket server, WebSocket socket, string serial)
    {
        _server = server;
        _webSocket = socket;
        _serial = serial;
    }

    public event Action<WebSocket, string>? ConnectedEvent;
    public event Action<string>? DisconnectedEvent;
    public event Action<string>? MessageEvent;

    public string Serial => _serial;
    public WebSocket Socket => _webSocket;

    public static async Task<LiteNet3AspNetWebSocketConnection?> CreateAsync(
        LiteNet3AspNetWebSocket server,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (!context.Request.Headers.TryGetValue("x-api-key", out var apiKeyHeader) ||
            !context.Request.Headers.TryGetValue("Serial", out var serialHeader))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync("Missing required headers", cancellationToken);
            return null;
        }

        if (!string.Equals(apiKeyHeader.ToString(), "12345-abcde-67890-fghij", StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("Invalid API key", cancellationToken);
            server.Log = "Rejected connection due to invalid API key.";
            Console.WriteLine(server.Log);
            return null;
        }

        var socket = await context.WebSockets.AcceptWebSocketAsync();
        var serial = serialHeader.ToString();

        return new LiteNet3AspNetWebSocketConnection(server, socket, serial);
    }

    public async Task ProcessAsync(CancellationToken cancellationToken)
    {
        try
        {
            _server.RegisterConnection(_serial);
            ConnectedEvent?.Invoke(_webSocket, _serial);

            InitializeInactivityTimer();

            await ListenAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Host shutting down.
        }
        catch (WebSocketException ex)
        {
            _server.Log = $"WebSocket error for client {_serial}: {ex.Message}";
            Console.WriteLine(_server.Log);
        }
        catch (Exception ex)
        {
            _server.Log = $"Unexpected error for client {_serial}: {ex.Message}";
            Console.WriteLine(_server.Log);
        }
        finally
        {
            await CloseAndCleanupAsync();
        }
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(8 * 1024);

        try
        {
            while (!cancellationToken.IsCancellationRequested && _webSocket.State == WebSocketState.Open)
            {
                var segment = new ArraySegment<byte>(buffer);
                var result = await _webSocket.ReceiveAsync(segment, cancellationToken);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnected", CancellationToken.None);
                    return;
                }

                if (result.MessageType != WebSocketMessageType.Text)
                    continue;

                using var messageBuffer = new MemoryStream();
                messageBuffer.Write(buffer, 0, result.Count);

                while (!result.EndOfMessage)
                {
                    result = await _webSocket.ReceiveAsync(segment, cancellationToken);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnected", CancellationToken.None);
                        return;
                    }

                    messageBuffer.Write(buffer, 0, result.Count);
                }

                ResetInactivityTimer();
                var message = Encoding.UTF8.GetString(messageBuffer.ToArray());
                MessageEvent?.Invoke(message);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private void InitializeInactivityTimer()
    {
        _inactivityTimer = new Timer(InactivityTimeout) { AutoReset = false };
        _inactivityTimer.Elapsed += async (_, _) =>
        {
            try
            {
                if (_webSocket.State == WebSocketState.Open)
                {
                    await _webSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Inactivity timeout",
                        CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error closing WebSocket for inactivity: {ex.Message}");
            }
        };

        _inactivityTimer.Start();
    }

    private void ResetInactivityTimer()
    {
        if (_inactivityTimer == null) return;
        _inactivityTimer.Stop();
        _inactivityTimer.Start();
    }

    private async Task CloseAndCleanupAsync()
    {
        _inactivityTimer?.Stop();
        _inactivityTimer?.Dispose();

        if (_webSocket.State != WebSocketState.Closed && _webSocket.State != WebSocketState.Aborted)
        {
            try
            {
                await _webSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Server shutdown",
                    CancellationToken.None);
            }
            catch
            {
                // Ignore; we are tearing down the socket.
            }
        }

        _webSocket.Dispose();
        _server.UnregisterConnection(_serial);
        _server.Log = $"Client {_serial} has been disconnected";
        Console.WriteLine(_server.Log);
        DisconnectedEvent?.Invoke(_serial);
    }
}
