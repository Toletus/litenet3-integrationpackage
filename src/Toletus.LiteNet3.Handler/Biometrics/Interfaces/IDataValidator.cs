using Toletus.LiteNet3.Handler.Responses.NotificationsResponses;

namespace Toletus.LiteNet3.Handler.Biometrics.Interfaces;

public interface IDataValidator
{
    byte[]? ConvertHexStringToByteArray(string byteString);
    bool IsValidData(BiometricsResponse biometrics, byte[] data, IDataStorage dataStorage);
}