using System.Text.Json;
using System.Text.Json.Serialization;

namespace FinancialMonitor.Api.Contracts;

/// <summary>
/// JSON settings shared by the controllers and SignalR, so GET and the live event serialize identically.
/// </summary>
public static class ApiJson
{
    public static void Configure(JsonSerializerOptions options)
    {
        // status travels as its name ("Completed"), read case-insensitively.
        // Numbers are rejected, otherwise "status": 5 would become an undefined enum value.
        options.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
    }
}
