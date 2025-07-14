namespace Toletus.LiteNet3.Handler.Requests.Updates;

public class SensorUpdate : UpdateBase
{
    public SensorUpdate(bool resetIn, bool resetOut)
    {
        Update = "sensor";
        Data = new { resetIn, resetOut };
    }
}