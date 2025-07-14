using System.Collections.Concurrent;
using System.Net.Sockets;
using WebSocketSharp.Server;

namespace Toletus.LiteNet3.Server;

public class LiteNet3WebSocket
{
    private WebSocketServer? _server;
    public string Log = string.Empty;
    private static readonly ConcurrentDictionary<string, bool> ActiveConnections = new();

    public Action<LiteNet3WebSocketBehavior>? OnNewBehavior;

    public void StartAsync(string uri)
    {
        StopAndClearWebSocketServer();
        RestartWebSocketServerWithChatService(uri);
    }

    public static void RegisterConnection(string serial)
    {
        ActiveConnections.TryAdd(serial, true);
        Console.WriteLine($"Client {serial} registered. Total connections: {ActiveConnections.Count}");
    }

    public void UnregisterConnection(string serial)
    {
        ActiveConnections.TryRemove(serial, out _);
        Console.WriteLine($"Client {serial} unregistered. Remaining connections: {ActiveConnections.Count}");

        if (!ActiveConnections.IsEmpty) return;
        Console.WriteLine("No more active connections. Stopping server...");
        StopAndClearWebSocketServer();
    }

    private void RestartWebSocketServerWithChatService(string uri)
    {
        try
        {
            var uriObject = new Uri(uri);

            _server = new WebSocketServer(uriObject.Port)
            {
                KeepClean = false,
                WaitTime = TimeSpan.FromSeconds(30)
            };

            _server.AddWebSocketService<LiteNet3WebSocketBehavior>("/", behavior =>
            {
                behavior.LiteNet3WebSocket = this;
                OnNewBehavior?.Invoke(behavior);
            });

            _server.Start();

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

    public void StopAndClearWebSocketServer()
    {
        try
        {
            if (_server == null) return;

            if (_server.IsListening)
            {
                Console.WriteLine("Stopping current WebSocket server...");
                _server.Stop();
            }

            _server?.RemoveWebSocketService("/");
            _server = null;
            Console.WriteLine("WebSocket server stopped and resources cleared.");

            ActiveConnections.Clear();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while stopping WebSocket server: {ex.Message}");
        }
    }
}