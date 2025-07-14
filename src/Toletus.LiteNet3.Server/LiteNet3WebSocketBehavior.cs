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

    public event Action<WebSocket, string>? ConnectedEvent;
    public event Action<string>? DisconnectedEvent;
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

            LiteNet3WebSocket.RegisterConnection(serial);
            ConnectedEvent?.Invoke(Context.WebSocket, serial);

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

        _inactivityTimer?.Dispose();

        if (!string.IsNullOrEmpty(serial))
            LiteNet3WebSocket.UnregisterConnection(serial);

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
        _inactivityTimer?.Dispose();

        var serial = Context.Headers["Serial"];

        LiteNet3WebSocket.Log = $"Client {serial} has been disconnected";
        Console.WriteLine(LiteNet3WebSocket.Log);

        LiteNet3WebSocket.UnregisterConnection(serial);
        DisconnectedEvent?.Invoke(serial);
    }

    private void InitializeInactivityTimer()
    {
        _inactivityTimer = new Timer(InactivityTimeout);
        _inactivityTimer.Elapsed += (_, _) =>
        {
            Context.WebSocket.Close(CloseStatusCode.Normal, "Inactivity timeout");
        };
        _inactivityTimer.Start();
    }
}