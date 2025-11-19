using System.IO.Ports;
using System.Text;
using Toletus.LiteNet3.Handler;
using Toletus.LiteNet3.Handler.Responses.FetchesResponse;

namespace Toletus.LiteNet3.SerialPort;

public class SerialService
{
    private const string SerialPortName = "/dev/ttyS4";
    private const int BaudRate = 460800;
    private static System.IO.Ports.SerialPort? _serialPort;

    private readonly Lock _receivedLock = new();
    private readonly StringBuilder _serialBuffer = new();
    private readonly Lock _sendLock = new();

    public event Action<string>? MessageEvent;
    public bool IsOpen => _serialPort?.IsOpen ?? false;

    public void Start(string? serialPortName = SerialPortName, int baudRate = BaudRate)
    {
        _serialPort = new System.IO.Ports.SerialPort(serialPortName, baudRate)
        {
            ReadTimeout = 20,
            ReceivedBytesThreshold = 1, // dispara assim que chegar algo
            ReadBufferSize = 10000
        };

        _serialPort.DataReceived += OnDataReceived;
        _serialPort.Open();
    }

    public void Stop()
    {
        _serialPort?.Close();
        _serialPort?.Dispose();
    }

    public void Send(string message) => _serialPort?.Write(message);

    public TResponse SendAndWaitResponse<TResponse>(string message, int timeoutMs = 2000) where TResponse : class
    {
        if (_serialPort is not { IsOpen: true })
            throw new InvalidOperationException("Serial port is not open.");

        lock (_sendLock)
        {
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

            void Handler(string json)
            {
                tcs.TrySetResult(json);
            }

            try
            {
                MessageEvent += Handler;

                _serialPort.Write(message);

                var completed = tcs.Task.Wait(timeoutMs);
                if (!completed)
                    throw new TimeoutException(
                        $"Timed out while waiting for a response from the serial port ({timeoutMs}ms).");

                var json = tcs.Task.Result;
                var jsonObject = JObjectUtil.Parse(json);
                var response = jsonObject?.ToObject<FetchResponse>();

                return response == null
                    ? throw new Exception()
                    : response.GetData<TResponse>()!;
            }
            finally
            {
                MessageEvent -= Handler;
            }
        }
    }


    private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        var serialPort = (System.IO.Ports.SerialPort)sender;
        var data = serialPort.ReadExisting();

        if (string.IsNullOrEmpty(data))
            return;

        lock (_receivedLock)
        {
            _serialBuffer.Append(data);

            var jsons = ExtractJsons(_serialBuffer);

            foreach (var json in jsons)
                MessageEvent?.Invoke(json);
        }
    }

    private static List<string> ExtractJsons(StringBuilder buffer)
    {
        var results = new List<string>();
        var depth = 0;
        var insideJson = false;
        var current = new StringBuilder();

        for (var i = 0; i < buffer.Length; i++)
        {
            var c = buffer[i];

            if (c == '{')
            {
                depth++;
                insideJson = true;
            }

            if (insideJson)
                current.Append(c);

            if (c != '}') continue;

            if (depth > 0)
                depth--;

            if (depth != 0 || !insideJson) continue;

            var json = current.ToString();
            results.Add(json);
            current.Clear();
            insideJson = false;
        }

        buffer.Clear();
        if (insideJson && current.Length > 0)
            buffer.Append(current);

        return results;
    }
}