using System.Net.WebSockets;
using System.Text;

namespace Toletus.LiteNet3.Server;

public class LiteNet3WebSocketBehavior
{
    public LiteNet3WebSocket LiteNet3WebSocket { get; set; } = null!;

    private readonly Guid _connectionId = Guid.NewGuid();
    private CancellationTokenSource? _connectionCts;

    public event Action<WebSocket, string, Guid>? ConnectedEvent;
    public event Action<string, Guid>? DisconnectedEvent;
    public event Action<string>? MessageEvent;

    internal void Abort() => _connectionCts?.Cancel();

    public async Task RunAsync(WebSocket ws, string serial, CancellationToken serverToken)
    {
        _connectionCts = CancellationTokenSource.CreateLinkedTokenSource(serverToken);
        var ct = _connectionCts.Token;

        if (LiteNet3WebSocket.TryGetBehavior(serial, out var oldBehavior) && oldBehavior != this)
        {
            try { oldBehavior!.Abort(); } catch { /* ignore */ }
        }

        LiteNet3WebSocket.BindSerialToBehavior(serial, this);
        LiteNet3WebSocket.RegisterConnection(serial);

        ConnectedEvent?.Invoke(ws, serial, _connectionId);

        try
        {
            var buffer = new byte[20000];
            while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                WebSocketReceiveResult result;
                try
                {
                    result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                }
                catch (OperationCanceledException) { break; }
                catch (WebSocketException) { break; }

                if (result.CloseStatus.HasValue)
                    break;

                if (result.MessageType == WebSocketMessageType.Text)
                    MessageEvent?.Invoke(Encoding.UTF8.GetString(buffer, 0, result.Count));
            }

            if (ws.State == WebSocketState.Open)
            {
                try { await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None); }
                catch { /* ignore */ }
            }
        }
        finally
        {
            LiteNet3WebSocket.UnregisterConnection(serial);
            LiteNet3WebSocket.UnbindSerial(serial);
            DisconnectedEvent?.Invoke(serial, _connectionId);
            _connectionCts.Dispose();
        }
    }
}