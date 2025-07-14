using System.Diagnostics;
using Toletus.LiteNet3.Handler.Biometrics.Interfaces;
using Toletus.LiteNet3.Handler.Responses.NotificationsResponses;

namespace Toletus.LiteNet3.Handler.Biometrics.Datas;

public class DataAccumulator(IDataStorage dataStorage, IDataValidator dataValidator) : IDataAccumulator
{
    private int _expectedLength;
    private bool _isFinal;
    private bool _hasError;

    public DataAccumulator() : this(new DataStorage(), new DataValidator())
    {
    }

    public void AccumulateData(BiometricsResponse biometrics)
    {
        if (biometrics.Finally)
            FinalizeData();
        else
            HandleIntermediateData(biometrics);
    }

    public byte[]? GetData()
    {
        return _isFinal && !_hasError
            ? dataStorage.GetAccumulatedData(_expectedLength)
            : null;
    }

    private void FinalizeData()
    {
        if (dataStorage.Count >= 24)
            dataStorage.SortAndCombineData();

        _hasError = dataStorage.TotalDataLength < _expectedLength;
        _isFinal = true;
    }

    private void HandleIntermediateData(BiometricsResponse biometrics)
    {
        var dataBytes = dataValidator.ConvertStringToByteArray(biometrics.Package);
        _expectedLength += biometrics.Len;

        if (dataBytes == null || !dataValidator.IsValidData(biometrics, dataBytes, dataStorage))
            return;

        dataStorage.Store(biometrics.Id, dataBytes);
    }
}