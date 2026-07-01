using System.Buffers;
using System.Net.WebSockets;
using System.Text;

namespace Toletus.LiteNet3.Server;

public class LiteNet3WebSocketBehavior
{
    private const int BufferSize = 40 * 1024;
    private const int MaxTextMessageBytes = 1024 * 1024;

    public LiteNet3WebSocket LiteNet3WebSocket { get; set; } = null!;

    private readonly Guid _connectionId = Guid.NewGuid();
    private CancellationTokenSource? _connectionCts;
    private string _disconnectReason = "Unknown";

    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private WebSocket? _ws;

    public Guid ConnectionId => _connectionId;

    public event Action<LiteNet3WebSocketBehavior, WebSocket, string, Guid>? ConnectedEvent;
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
        _ws = ws;

        var oldBehavior = LiteNet3WebSocket.RegisterCurrentConnection(serial, this);
        if (oldBehavior != null)
            TryAbortOldBehavior(serial, oldBehavior);

        ConnectedEvent?.Invoke(this, ws, serial, _connectionId);

        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        var receivedClose = false;
        try
        {
            while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var result = await ReceiveTextMessageAsync(ws, buffer, ct);

                if (result.CloseStatus.HasValue)
                {
                    receivedClose = true;
                    _disconnectReason = $"Remote close: {result.CloseStatus} {result.CloseStatusDescription}";
                    break;
                }

                if (result.UnsupportedMessageType.HasValue)
                {
                    _disconnectReason = $"Unsupported message type: {result.UnsupportedMessageType}";
                    break;
                }

                if (result.IsTooLarge)
                {
                    _disconnectReason = $"Message too large ({result.MessageSize} bytes)";
                    break;
                }

                if (!string.IsNullOrWhiteSpace(result.Text))
                    DispatchMessage(serial, result.Text);
            }

            await CloseHandshakeAsync(ws, receivedClose);
        }
        catch (OperationCanceledException)
        {
        }
        catch (WebSocketException ex)
        {
            _disconnectReason = "WebSocketException";
            Console.WriteLine($"[LiteNet3] Receive failed for serial {serial}: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);

            try { ws.Dispose(); } catch { /* ignore */ }
            _ws = null;

            if (LiteNet3WebSocket.TryClearCurrentConnection(serial, _connectionId))
                DisconnectedEvent?.Invoke(serial, _connectionId);

            _connectionCts?.Dispose();
        }
    }

    private const int CloseHandshakeTimeoutMs = 5_000;

    private async Task CloseHandshakeAsync(WebSocket ws, bool receivedClose)
    {
        using var closeCts = new CancellationTokenSource(CloseHandshakeTimeoutMs);
        await _sendGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (receivedClose)
            {
                if (ws.State == WebSocketState.CloseReceived)
                    await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Closing", closeCts.Token);
            }
            else if (ws.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", closeCts.Token);
            }
        }
        catch { /* ignore */ }
        finally
        {
            _sendGate.Release();
        }
    }

    public async Task CloseAsync(string reason)
    {
        var ws = _ws;
        if (ws == null)
            return;

        using var closeCts = new CancellationTokenSource(CloseHandshakeTimeoutMs);
        await _sendGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_ws is { State: WebSocketState.Open } live)
                await live.CloseAsync(WebSocketCloseStatus.NormalClosure, reason, closeCts.Token).ConfigureAwait(false);
        }
        catch { /* ignore */ }
        finally
        {
            _sendGate.Release();
        }
    }

    public async Task<bool> SendTextAsync(string json, CancellationToken ct)
    {
        var ws = _ws;
        if (ws == null || ws.State != WebSocketState.Open)
            return false;

        await _sendGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_ws is not { State: WebSocketState.Open } live)
                return false;

            var payload = Encoding.UTF8.GetBytes(json);
            await live.SendAsync(
                payload,
                WebSocketMessageType.Text,
                endOfMessage: true,
                ct).ConfigureAwait(false);

            return true;
        }
        finally
        {
            _sendGate.Release();
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
            Console.WriteLine($"[LiteNet3] Failed to abort old behavior for serial {serial}: {ex.Message}");
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
            Console.WriteLine($"[LiteNet3] Message dispatch failed for serial {serial}: {ex.GetType().Name}: {ex.Message}");
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
