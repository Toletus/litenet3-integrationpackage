using System.Globalization;
using Toletus.LiteNet3.Handler.Biometrics.Interfaces;
using Toletus.LiteNet3.Handler.Responses.NotificationsResponses;

namespace Toletus.LiteNet3.Handler.Biometrics.Datas;

public class DataValidator : IDataValidator
{
    public byte[] ConvertHexStringToByteArray(string hexString)
    {
        var sanitized = hexString
            .Trim('"', '[', ']', ' ')
            .Replace(" ", string.Empty);

        if (sanitized.Length == 0 || sanitized.Length % 2 != 0)
            return [];

        return Enumerable
            .Range(0, sanitized.Length / 2)
            .Select(i => byte.Parse(sanitized.AsSpan(i * 2, 2), NumberStyles.HexNumber))
            .ToArray();
    }

    public bool IsValidData(BiometricsResponse biometrics, byte[]? data, IDataStorage dataStorage)
    {
        return data != null &&
               biometrics.Len == data.Length &&
               !dataStorage.Data.ContainsKey(biometrics.Id);
    }
}