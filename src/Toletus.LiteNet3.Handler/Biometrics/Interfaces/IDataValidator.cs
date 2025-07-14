using Toletus.LiteNet3.Handler.Responses.NotificationsResponses;

namespace Toletus.LiteNet3.Handler.Biometrics.Interfaces;

public interface IDataValidator
{
    byte[]? ConvertStringToByteArray(string byteString);
    bool IsValidData(BiometricsResponse biometrics, byte[] data, IDataStorage dataStorage);
}