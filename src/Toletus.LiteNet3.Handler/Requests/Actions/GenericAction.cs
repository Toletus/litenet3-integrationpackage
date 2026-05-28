using System.Text.Json;

namespace Toletus.LiteNet3.Handler.Requests.Actions;

public class GenericAction : ActionBase
{
    public GenericAction(string action, object? data = null)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            throw new ArgumentException("A ação não pode ser vazia.", nameof(action));
        }

        Action = action;
        Data = data;
    }

    public override string Serialize()
    {
        return Data is null
            ? JsonSerializer.Serialize(new { action = Action })
            : JsonSerializer.Serialize(new { action = Action, data = Data });
    }
}