using System.Text.Encodings.Web;
using System.Text.Json;

namespace AvaloniaApplication1.Models;

public static class ConfigJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,                                   // файл зручно читати очима
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,      // "ipAddress", а не "IpAddress"
        PropertyNameCaseInsensitive = true,                     // при читанні регістр не важливий
        AllowOutOfOrderMetadataProperties = true,               // "type" може стояти не першим, якщо файл правили вручну

        // Без цього українські літери в файлі перетворюються на "\u041A\u043E...".
        // Для локальних файлів конфігів це безпечно (вони не вставляються в HTML).
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
}