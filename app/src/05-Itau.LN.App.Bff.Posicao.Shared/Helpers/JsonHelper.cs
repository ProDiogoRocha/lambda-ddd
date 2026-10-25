using System.Text.Json;

namespace Itau.LN.App.Bff.Posicao.Shared.Helpers;

public static class JsonHelper
{
    // camelCase, case-insensitive e números aceitos como string.
    public static readonly JsonSerializerOptions Options = JsonSerializerOptions.Web;

    public static T? DeserializeOrDefault<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, Options);
        }
        catch (JsonException)
        {
            return default;
        }
    }
}
