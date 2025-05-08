using MhpdCommon.Models.MessageBodyModels;
using System.Reflection;
using System.Text.Json;

namespace PensionsRetrievalFunctionTests.Data;

internal static class DataProvider
{
    internal static Stream GetStream(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var stream = assembly.GetManifestResourceStream($"{typeof(DataProvider).Namespace}.{resourceName}");
        return stream ?? throw new InvalidOperationException($"Resource '{resourceName}' not found.");
    }

    internal static string GetString(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using Stream? stream = assembly.GetManifestResourceStream($"{typeof(DataProvider).Namespace}.{resourceName}") ??
            throw new InvalidOperationException($"Resource '{resourceName}' not found.");
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }

    internal static PensionRetrievalPayload? GetPayload(string payloadFile)
    {
        return JsonSerializer.Deserialize<PensionRetrievalPayload>(GetStream(payloadFile));
    }
}
