using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Toletus.LiteNet3.Handler;
using Toletus.LiteNet3.Handler.Requests;
using Toletus.LiteNet3.Handler.Requests.Fetches;
using Toletus.LiteNet3.Handler.Responses.Base;
using Toletus.LiteNet3.Handler.Responses.FetchesResponse;
using Toletus.LiteNet3.Handler.Responses.NotificationsResponses;
using Toletus.LiteNet3.Handler.Responses.NotificationsResponses.Base;
using Toletus.LiteNet3.SerialPort;
using Toletus.LiteNet3.Server;
using Toletus.Pack.Core.Network;
using System.Net.WebSockets;

namespace Toletus.LiteNet3;

public class LiteNet3BoardBase
{
    private readonly ManualResetEventSlim _connectedEvent = new(initialState: false);

    private readonly ResponseDispatcher _dispatcher = new();
    private readonly Lock _subscriptionLock = new();
    private bool _dispatcherSubscribed;
    private LiteNet3WebSocketBehavior? _currentBehavior;
    private readonly int _portServer;
    private readonly ConcurrentDictionary<string, Guid> _activeConnectionIds = new();
    private static readonly ConcurrentDictionary<string, object> ConnectLocks = new();

    private static readonly ConcurrentDictionary<string, LiteNet3WebSocket> LiteNet3WebSockets = new();
    private static readonly ConcurrentDictionary<string, BoardWebSocketConnection> WebSockets = new();
    private static SerialService? _serialService;

    private string ConnectionInfo { get; set; }

    public IPAddress Ip { get; set; }
    public IPAddress? NetworkIp { get; set; }
    public const int Port = 7878;
    public int Id { get; set; }
    public string? Serial { get; set; }
    public string? Alias { get; set; }
    public string? ServerUri { get; set; }
    public string? Firmware { get; set; }
    public string? Hardware { get; set; }

    public bool InUse => Connected
                         || ConnectionInfo.Length > 0 && ConnectionInfo != "Disconnected";

    public bool HasFingerprintReader { get; set; }
    public bool Connected { get; private set; }
    public object? Tag { get; set; }
    public string? Mac { get; set; }
    public string? Description { get; set; }
    public IpConfig? IpConfig { get; set; }
    public string? MenuPassword { get; set; }
    public int? ReleaseDuration { get; set; }
    public bool? ShowCounters { get; set; }
    public bool? EntryClockwise { get; set; }
    public bool? BuzzerMute { get; set; }

    public Action<LiteNet3BoardBase, CodeBase>? OnIdentification;
    public Action<LiteNet3BoardBase, object>? OnResponse;
    public Action<LiteNet3BoardBase, ResultBase>? OnResult;
    public Action<LiteNet3BoardBase, byte[]>? OnBiometricsResponse;
    public Action<LiteNet3BoardBase, ReleaseBase>? OnReleaseResponse;

    public LiteNet3BoardBase(
        IPAddress ip,
        bool connected,
        int? id = null,
        string connectionInfo = "",
        string? serial = null,
        string? alias = null)
    {
        Connected = connected;
        Ip = ip;
        if (id.HasValue) Id = id.Value;
        ConnectionInfo = connectionInfo == "None" ? "Disconnected" : connectionInfo;
        _portServer = GetAvailablePort();
        Serial = serial;
        Alias = alias;
    }

    protected LiteNet3BoardBase(string connectionInfo = "")
    {
        ConnectionInfo = connectionInfo == "None" ? "Disconnected" : connectionInfo;
    }

    public override string ToString() => $"LiteNet3 #{Id} {Ip}:{Port} {ConnectionInfo}";

    public void Connect(string network)
    {
        ArgumentException.ThrowIfNullOrEmpty(Serial);

        if (StartServerAndCheckAlreadyConnected(network))
            return;

        if (_connectedEvent.Wait(TimeSpan.FromSeconds(30))) return;

        Close();
        throw new TimeoutException("LiteNet3 failed to connect within 30 seconds.");
    }

    public async Task ConnectAsync(string network, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(Serial);

        if (StartServerAndCheckAlreadyConnected(network))
            return;

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));

        try
        {
            await _connectedEvent.WaitHandle.WaitOneAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Close();
            throw new TimeoutException("LiteNet3 failed to connect within 30 seconds.");
        }
    }

    private bool StartServerAndCheckAlreadyConnected(string network)
    {
        _connectedEvent.Reset();

        WebSockets.TryGetValue(Serial, out var webSocket);
        if (webSocket?.Socket.State == WebSocketState.Open)
            return true;

        NetworkIp = NetworkHelper.GetLocalNetworkAddress(network);
        var serverUri = $"ws://{NetworkIp}:{_portServer}";

        if (!serverUri.Equals(ServerUri, StringComparison.OrdinalIgnoreCase))
            LiteNetUtil.SetServer(Ip, serverUri, Serial);

        var uri = $"http://{NetworkIp}:{_portServer}/";

        var lockObj = ConnectLocks.GetOrAdd(Serial, _ => new object());
        lock (lockObj)
        {
            if (!LiteNet3WebSockets.TryGetValue(Serial, out var existingServer))
            {
                existingServer = new LiteNet3WebSocket();
                LiteNet3WebSockets[Serial] = existingServer;
                existingServer.OnNewBehavior = SwapCurrentBehavior;
                existingServer.Start(uri);
            }
            else
            {
                existingServer.OnNewBehavior = SwapCurrentBehavior;
            }
        }

        return false;
    }

    private void SwapCurrentBehavior(LiteNet3WebSocketBehavior behavior)
    {
        lock (_subscriptionLock)
        {
            if (_currentBehavior != null)
            {
                _currentBehavior.ConnectedEvent -= OnConnected;
                _currentBehavior.DisconnectedEvent -= OnDisconnected;
                _currentBehavior.MessageEvent -= OnMessage;
            }

            _currentBehavior = behavior;
            behavior.ConnectedEvent += OnConnected;
            behavior.DisconnectedEvent += OnDisconnected;
            behavior.MessageEvent += OnMessage;
        }
    }

    public void ConnectSerialPort(string? serialPortName = null)
    {
        if (_serialService?.IsOpen == true)
            Close();

        _serialService = new SerialService();
        _serialService.Start(serialPortName);
        _serialService.MessageEvent += OnMessage;
        Connected = true;

        var liteNet3Response = _serialService.SendAndWaitResponse<LiteNet3Response>(new LiteNet3Fetch().Serialize());

        Id = liteNet3Response.Id;
        Alias = liteNet3Response.Alias;
        Serial = liteNet3Response.Serial;
        Ip = IPAddress.Loopback;

        ToggleWebSocketEventSubscriptions();
    }

    public void Close()
    {
        if (_serialService != null)
            CloseSerialPort();
        else
            DisconnectWebSocketClients(Serial);

        Connected = false;
    }

    protected void Send(RequestBase request)
    {
        var json = request.Serialize();

        if (_serialService?.IsOpen == true)
        {
            _serialService.Send(json);
            return;
        }

        if (!WebSockets.TryGetValue(Serial, out var value))
            return;

        if (value.Socket.State != WebSocketState.Open)
            return;

        _ = SendWebSocketAsync(Serial, json, value);
    }

    private void CloseSerialPort()
    {
        _serialService?.Stop();
        _serialService = null;

        ToggleWebSocketEventSubscriptions(isRegister: false);
    }

    private static void DisconnectWebSocketClients(string serial, bool isOnDisconnect = false)
    {
        if (!LiteNet3WebSockets.TryGetValue(serial, out var liteNet3WebSocket)) return;

        if (!isOnDisconnect)
        {
            liteNet3WebSocket.StopAndClearWebSocketServer();
            LiteNet3WebSockets.TryRemove(serial, out _);
        }

        WebSockets.TryRemove(serial, out _);
    }


    private void ToggleWebSocketEventSubscriptions(bool isRegister = true)
    {
        lock (_subscriptionLock)
        {
            if (isRegister)
            {
                if (_dispatcherSubscribed)
                    return;

                _dispatcher.OnNotificationResponse += OnNotificationResponse;
                _dispatcher.OnNotificationBiometricsResponse += OnNotificationBiometricsResponse;
                _dispatcher.OnFetchResponse += OnOnFetchResponseHandler;
                _dispatcher.OnUpdateResponse += OnChangeResponseHandler;
                _dispatcher.OnActionResponse += OnChangeResponseHandler;

                _dispatcherSubscribed = true;
            }
            else
            {
                if (!_dispatcherSubscribed)
                    return;

                _dispatcher.OnNotificationResponse -= OnNotificationResponse;
                _dispatcher.OnNotificationBiometricsResponse -= OnNotificationBiometricsResponse;
                _dispatcher.OnFetchResponse -= OnOnFetchResponseHandler;
                _dispatcher.OnUpdateResponse -= OnChangeResponseHandler;
                _dispatcher.OnActionResponse -= OnChangeResponseHandler;

                _dispatcherSubscribed = false;
            }
        }
    }

    private void OnChangeResponseHandler(ResultBase? obj)
    {
        if (obj == null) return;

        OnResult?.Invoke(this, obj);
    }

    private void OnOnFetchResponseHandler(object? obj)
    {
        if (obj == null) return;

        OnResponse?.Invoke(this, obj);
    }

    private void OnNotificationResponse(object? obj)
    {
        switch (obj)
        {
            case null:
                return;
            case PingResponse:
                Connected = true;
                OnResponse?.Invoke(this, obj);
                return;
            case CodeBase code:
                OnIdentification?.Invoke(this, code);
                return;
            case PassageResponse response:
                OnReleaseResponse?.Invoke(this, response);
                return;
            case TimeoutResponse response:
                OnReleaseResponse?.Invoke(this, response);
                return;
            case ButtonResponse:
                OnResponse?.Invoke(this, obj);
                return;
            case ErrorResponse:
                OnResponse?.Invoke(this, obj);
                return;
            default:
                break;
        }
    }

    private void OnNotificationBiometricsResponse(byte[] obj)
    {
        OnBiometricsResponse?.Invoke(this, obj);
    }

    private void OnConnected(LiteNet3WebSocketBehavior behavior, WebSocket obj, string serial, Guid connectionId)
    {
        if (behavior.ConnectionId != connectionId)
            return;

        Connected = true;
        _activeConnectionIds[serial] = connectionId;
        if (WebSockets.TryGetValue(serial, out var oldWs) && oldWs.ConnectionId != connectionId)
        {
            _ = oldWs.Behavior.CloseAsync("Replaced by new connection");
        }

        WebSockets[serial] = new BoardWebSocketConnection(behavior, obj, connectionId);
        ToggleWebSocketEventSubscriptions();
        _connectedEvent.Set();
    }

    private void OnDisconnected(string serial, Guid connectionId)
    {
        if (_activeConnectionIds.TryGetValue(serial, out var current) && current != connectionId)
            return;

        DisconnectWebSocketClients(serial, true);
        ToggleWebSocketEventSubscriptions(isRegister: false);
        Connected = false;
    }

    private void OnMessage(string obj)
    {
        _dispatcher.Dispatch(obj);
    }

    private static int GetAvailablePort()
    {
        const int minPort = 1024;
        const int maxPort = 65535;
        var random = new Random();

        while (true)
        {
            var port = random.Next(minPort, maxPort);

            if (IsPortAvailable(port))
                return port;
        }
    }

    private static bool IsPortAvailable(int port)
    {
        try
        {
            var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            listener.Stop();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    private static async Task SendWebSocketAsync(string serial, string json, BoardWebSocketConnection connection)
    {
        try
        {
            if (!WebSockets.TryGetValue(serial, out var current) ||
                !ReferenceEquals(current, connection) ||
                current.ConnectionId != connection.ConnectionId)
                return;

            await connection.Behavior.SendTextAsync(json, CancellationToken.None);
        }
        catch (IOException ioEx)
        {
            Console.WriteLine($"[LiteNet3] Send failed (IO) for serial {serial}: {ioEx.Message}");
        }
        catch (WebSocketException wsEx)
        {
            Console.WriteLine($"[LiteNet3] Send failed (WebSocket) for serial {serial}: {wsEx.Message}");
        }
        catch (SocketException sockEx)
        {
            Console.WriteLine($"[LiteNet3] Send failed (Socket) for serial {serial}: {sockEx.Message}");
        }
        catch (OperationCanceledException cancelEx)
        {
            Console.WriteLine($"[LiteNet3] Send canceled for serial {serial}: {cancelEx.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LiteNet3] Send failed (Unexpected) for serial {serial}: {ex.Message}");
        }
    }

    private sealed class BoardWebSocketConnection(LiteNet3WebSocketBehavior behavior, WebSocket socket, Guid connectionId)
    {
        public LiteNet3WebSocketBehavior Behavior { get; } = behavior;
        public WebSocket Socket { get; } = socket;
        public Guid ConnectionId { get; } = connectionId;
    }
}

internal static class WaitHandleExtensions
{
    public static Task WaitOneAsync(this WaitHandle handle, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var registration = ThreadPool.RegisterWaitForSingleObject(
            handle,
            (_, timedOut) => { if (!timedOut) tcs.TrySetResult(); },
            null,
            Timeout.Infinite,
            executeOnlyOnce: true);

        var ctr = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));

        return tcs.Task.ContinueWith(t =>
        {
            registration.Unregister(null);
            ctr.Dispose();
            return t;
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default).Unwrap();
    }
}