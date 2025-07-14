using Toletus.LiteNet3.Handler.Biometrics.Interfaces;
using Toletus.LiteNet3.Handler.Responses.NotificationsResponses;

namespace Toletus.LiteNet3.Handler.Biometrics.Datas;

public class DataValidator : IDataValidator
{
    public byte[] ConvertStringToByteArray(string byteString)
    {
        return byteString
            .Trim('"', '[', ']')
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(byte.Parse)
            .ToArray();
    }
    
    public bool IsValidData(BiometricsResponse biometrics, byte[]? data, IDataStorage dataStorage)
    {
        return data != null &&
               biometrics.Len == data.Length &&
               !dataStorage.Data.ContainsKey(biometrics.Id);
    }
}