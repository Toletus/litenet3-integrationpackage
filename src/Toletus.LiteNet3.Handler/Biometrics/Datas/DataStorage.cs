using System.Diagnostics;
using Toletus.LiteNet3.Handler.Biometrics.Interfaces;

namespace Toletus.LiteNet3.Handler.Biometrics.Datas;

public class DataStorage : IDataStorage
{
    public Dictionary<int, byte[]> Data { get; } = new();
    private readonly List<byte> _accumulatedData = [];
    public int Count => Data.Count;
    public int TotalDataLength => _accumulatedData.Count;

    public void Store(int id, byte[] data)
    {
        Data.TryAdd(id, data);
    }

    public byte[]? GetAccumulatedData(int expectedLength)
        => _accumulatedData.Take(expectedLength).ToArray();

    public void SortAndCombineData()
    {
        _accumulatedData.Clear();
        _accumulatedData.AddRange(
            Data.OrderBy(entry => entry.Key)
                .SelectMany(entry => entry.Value)
        );
    }
}