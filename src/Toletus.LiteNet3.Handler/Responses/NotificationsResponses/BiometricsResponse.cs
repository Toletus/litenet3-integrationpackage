using Toletus.LiteNet3.Handler.Biometrics.Datas;
using Toletus.LiteNet3.Handler.Biometrics.Images;
using Toletus.LiteNet3.Handler.Responses.NotificationsResponses.Base;

namespace Toletus.LiteNet3.Handler.Responses.NotificationsResponses;

public class BiometricsResponse : SerialBase
{
    private static DataAccumulator? _acumulate;
    private ImageFinder? _imageFinder;

    public int Id { get; set; }
    public int Len { get; set; }
    public bool Init { get; set; }
    public string Package { get; set; }
    public bool Finally { get; set; }
    public int LenTotal { get; set; }

    public bool Process()
    {
        try
        {
            if (Init)
                _acumulate = new DataAccumulator();

            _acumulate?.AccumulateData(this);

            var result = _acumulate?.GetData();

            if (result is { Length: > 0 })
            {
                _imageFinder = new ImageFinder(result);
                _acumulate = new DataAccumulator();
                return true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            _acumulate = new DataAccumulator();
        }
        return false;
    }

    public ImageFinder GetImage() => _imageFinder!;
}