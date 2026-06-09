namespace Toletus.LiteNet3.Handler.Requests.Updates;

public class ServerUpdate : UpdateBase
{
    public ServerUpdate(string serial,string uri)
    {
        Update = "server";
        Data = new { serial, uri };
    }
}