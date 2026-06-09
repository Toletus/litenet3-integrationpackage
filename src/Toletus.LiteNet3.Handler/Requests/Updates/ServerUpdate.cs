namespace Toletus.LiteNet3.Handler.Requests.Updates;

public class ServerUpdate : UpdateBase
{
    public ServerUpdate(string uri)
    {
        Update = "server";
        Data = new { uri };
    }
}