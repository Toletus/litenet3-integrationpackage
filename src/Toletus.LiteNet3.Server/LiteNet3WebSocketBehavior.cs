using System.Net.Sockets;
using WebSocketSharp;
using WebSocketSharp.Server;
using ErrorEventArgs = WebSocketSharp.ErrorEventArgs;
using Timer = System.Timers.Timer;

namespace Toletus.LiteNet3.Server;

public class LiteNet3WebSocketBehavior : WebSocketBehavior
{
    public LiteNet3WebSocket LiteNet3WebSocket { get; set; }

    private const int InactivityTimeout = 60000; // 1 minuto em milissegundos
    private Timer? _inactivityTimer;
    private readonly Guid _connectionId = Guid.NewGuid();
    private int _closed; // 0 = aberto, 1 = fechando/fechado

    public event Action<WebSocket, string, Guid>? ConnectedEvent;
    public event Action<string, Guid>? DisconnectedEvent;
    public event Action<string>? MessageEvent;

    protected override void OnOpen()
    {
        try
        {
            if (!Context.Headers.Contains("x-api-key") || !Context.Headers.Contains("Serial"))
            {
                Context.WebSocket.Close(CloseStatusCode.PolicyViolation, "Missing required headers");
                return;
            }

            var apiKey = Context.Headers["x-api-key"];

            if (apiKey != "12345-abcde-67890-fghij")
            {
                Context.WebSocket.Close(CloseStatusCode.PolicyViolation, "Invalid API key");
                return;
            }

            var serial = Context.Headers["Serial"];

            var sessionId = ID;
            if (LiteNet3WebSocket.TryGetSession(serial, out var oldSessionId) && oldSessionId != sessionId)
            {
                try
                {
                    Sessions.CloseSession(oldSessionId, (ushort)CloseStatusCode.Normal, "Replaced by new connection");
                }
                catch { /* ignore */ }
            }
            
            LiteNet3WebSocket.BindSerialToSession(serial, sessionId);
            LiteNet3WebSocket.RegisterConnection(serial);
            
            ConnectedEvent?.Invoke(Context.WebSocket, serial, _connectionId);

            InitializeInactivityTimer();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during connection open: {ex.Message}");
            _inactivityTimer?.Dispose();
            throw;
        }
    }

    protected override void OnError(ErrorEventArgs e)
    {
        var serial = Context.Headers["Serial"];

        if (e.Exception is IOException { InnerException: SocketException sockEx })
        {
            LiteNet3WebSocket.Log =
                $"Socket error for client {serial}: Code {sockEx.ErrorCode}, Message: {sockEx.Message}";
            Console.WriteLine($"Socket disconnection detected for {serial}: {sockEx.ErrorCode}");
        }
        else
        {
            LiteNet3WebSocket.Log = $"Error for client {serial}: {e.Message}";
            Console.WriteLine(LiteNet3WebSocket.Log);
        }

        try
        {
            _inactivityTimer?.Stop();
            _inactivityTimer?.Dispose();
            _inactivityTimer = null;
        }
        catch { /* ignore */ }

        var state = Context.WebSocket.ReadyState;
        if (state != WebSocketState.Closing && state != WebSocketState.Closed)
        {
            SafeClose(CloseStatusCode.Abnormal, "Socket error");
        }

        base.OnError(e);
    }

    protected override void OnMessage(MessageEventArgs e)
    {
        _inactivityTimer?.Stop();
        _inactivityTimer?.Start();

        if (e.IsText)
            MessageEvent?.Invoke(e.Data);
    }

    protected override void OnClose(CloseEventArgs e)
    {
        if (Interlocked.Exchange(ref _closed, 1) == 1)
            return;

        try
        {
            _inactivityTimer?.Stop();
            _inactivityTimer?.Dispose();
            _inactivityTimer = null;
        }
        catch { /* ignore */ }

        var serial = Context.Headers["Serial"];

        LiteNet3WebSocket.Log = $"Client {serial} has been disconnected";
        Console.WriteLine(LiteNet3WebSocket.Log);

        LiteNet3WebSocket.UnregisterConnection(serial);
        LiteNet3WebSocket.UnbindSerial(serial);
        DisconnectedEvent?.Invoke(serial, _connectionId);
    }

    private void InitializeInactivityTimer()
    {
        _inactivityTimer = new Timer(InactivityTimeout) { AutoReset = false };
        _inactivityTimer.Elapsed += (_, _) =>
        {
            SafeClose(CloseStatusCode.Normal, "Inactivity timeout");
        };
        _inactivityTimer.Start();
    }

    private void SafeClose(CloseStatusCode code, string reason)
    {
        if (Interlocked.Exchange(ref _closed, 1) == 1)
            return;

        try
        {
            if (Context?.WebSocket?.ReadyState is WebSocketState.Open or WebSocketState.Connecting)
                Context.WebSocket.Close(code, reason);
        }
        catch { /* ignore */ }
    }
}