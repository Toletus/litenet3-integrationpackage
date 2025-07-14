namespace Toletus.LiteNet3.Handler.Biometrics.Interfaces;

public interface IDataStorage
{
    Dictionary<int, byte[]> Data { get; }
    int Count { get; }
    int TotalDataLength { get; }
    void Store(int id, byte[] data);
    byte[]? GetAccumulatedData(int expectedLength);
    void SortAndCombineData();
}