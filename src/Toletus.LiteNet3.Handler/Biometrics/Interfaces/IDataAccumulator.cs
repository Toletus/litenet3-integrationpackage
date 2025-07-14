using Toletus.LiteNet3.Handler.Responses.NotificationsResponses;

namespace Toletus.LiteNet3.Handler.Biometrics.Interfaces;

public interface IDataAccumulator
{
    void AccumulateData(BiometricsResponse biometrics);
    byte[]? GetData();
}