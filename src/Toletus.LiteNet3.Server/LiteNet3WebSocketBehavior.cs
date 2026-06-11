using System.Net.WebSockets;
using System.Text;

namespace Toletus.LiteNet3.Server;

public class LiteNet3WebSocketBehavior
{
    private const int BufferSize = 40000;
    private const int MaxTextMessageBytes = 1024 * 1024;

    public LiteNet3WebSocket LiteNet3WebSocket { get; set; } = null!;

    private readonly Guid _connectionId = Guid.NewGuid();
    private CancellationTokenSource? _connectionCts;
    private string _disconnectReason = "Unknown";

    internal Guid ConnectionId => _connectionId;

    public event Action<WebSocket, string, Guid>? ConnectedEvent;
    public event Action<string, Guid>? DisconnectedEvent;
    public event Action<string>? MessageEvent;

    internal void Abort() => Abort("Connection aborted");

    internal void Abort(string reason)
    {
        _disconnectReason = reason;
        _connectionCts?.Cancel();
    }

    public async Task RunAsync(WebSocket ws, string serial, CancellationToken serverToken)
    {
        _connectionCts = CancellationTokenSource.CreateLinkedTokenSource(serverToken);
        var ct = _connectionCts.Token;

        var oldBehavior = LiteNet3WebSocket.RegisterCurrentConnection(serial, this);
        if (oldBehavior != null)
            TryAbortOldBehavior(serial, oldBehavior);

        ConnectedEvent?.Invoke(ws, serial, _connectionId);

        try
        {
            var buffer = new byte[BufferSize];
            while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var result = await ReceiveTextMessageAsync(ws, buffer, ct);

                if (result.CloseStatus.HasValue)
                {
                    _disconnectReason = $"Remote close: {result.CloseStatus} {result.CloseStatusDescription}";
                    Console.WriteLine(
                        $"[LiteNet3] Receive close for serial {serial}, connection {_connectionId}: {result.CloseStatus} {result.CloseStatusDescription}");
                    break;
                }

                if (result.UnsupportedMessageType.HasValue)
                {
                    _disconnectReason = $"Unsupported message type: {result.UnsupportedMessageType}";
                    Console.WriteLine(
                        $"[LiteNet3] Unsupported WebSocket message type {result.UnsupportedMessageType} for serial {serial}, connection {_connectionId}.");
                    break;
                }

                if (result.IsTooLarge)
                {
                    _disconnectReason = $"Message too large ({result.MessageSize} bytes)";
                    Console.WriteLine(
                        $"[LiteNet3] WebSocket message too large for serial {serial}, connection {_connectionId}: {result.MessageSize} bytes.");
                    break;
                }

                if (result.Text != null)
                    DispatchMessage(serial, result.Text);
            }

            if (ws.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                try { await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None); }
                catch { /* ignore */ }
            }
        }
        catch (OperationCanceledException ex)
        {
            Console.WriteLine(
                $"[LiteNet3] Receive canceled for serial {serial}, connection {_connectionId}, state {ws.State}, reason {_disconnectReason}: {ex.Message}");
        }
        catch (WebSocketException ex)
        {
            _disconnectReason = "WebSocketException";
            Console.WriteLine(
                $"[LiteNet3] Receive failed for serial {serial}, connection {_connectionId}, state {ws.State}: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            var cleared = LiteNet3WebSocket.TryClearCurrentConnection(serial, _connectionId);
            if (cleared)
            {
                DisconnectedEvent?.Invoke(serial, _connectionId);
            }
            else
            {
                Console.WriteLine(
                    $"[LiteNet3] DisconnectedEvent skipped for stale connection {_connectionId} and serial {serial}.");
            }

            _connectionCts?.Dispose();
        }
    }

    private static void TryAbortOldBehavior(string serial, LiteNet3WebSocketBehavior oldBehavior)
    {
        try
        {
            oldBehavior.Abort("Replaced by new connection");
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"[LiteNet3] Failed to abort old WebSocket behavior for serial {serial}, connection {oldBehavior.ConnectionId}: {ex.Message}");
        }
    }

    private void DispatchMessage(string serial, string message)
    {
        try
        {
            MessageEvent?.Invoke(message);
        }
        catch (Exception ex)
        {
            _disconnectReason = "DispatcherException";
            Console.WriteLine(
                $"[LiteNet3] Message dispatch failed for serial {serial}, connection {_connectionId}: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static async Task<TextReceiveResult> ReceiveTextMessageAsync(
        WebSocket ws,
        byte[] buffer,
        CancellationToken ct)
    {
        using var message = new MemoryStream();

        while (true)
        {
            var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);

            if (result.CloseStatus.HasValue)
                return TextReceiveResult.Close(result.CloseStatus, result.CloseStatusDescription);

            if (result.MessageType != WebSocketMessageType.Text)
                return TextReceiveResult.Unsupported(result.MessageType);

            if (message.Length + result.Count > MaxTextMessageBytes)
                return TextReceiveResult.TooLarge(message.Length + result.Count);

            message.Write(buffer, 0, result.Count);

            if (!result.EndOfMessage) continue;

            return TextReceiveResult.TextMessage(Encoding.UTF8.GetString(message.ToArray()));
        }
    }

    private sealed record TextReceiveResult(
        string? Text,
        WebSocketCloseStatus? CloseStatus,
        string? CloseStatusDescription,
        WebSocketMessageType? UnsupportedMessageType,
        bool IsTooLarge,
        long MessageSize)
    {
        public static TextReceiveResult TextMessage(string text) =>
            new(text, null, null, null, false, text.Length);

        public static TextReceiveResult Close(WebSocketCloseStatus? status, string? description) =>
            new(null, status, description, null, false, 0);

        public static TextReceiveResult Unsupported(WebSocketMessageType messageType) =>
            new(null, null, null, messageType, false, 0);

        public static TextReceiveResult TooLarge(long size) =>
            new(null, null, null, null, true, size);
    }
}