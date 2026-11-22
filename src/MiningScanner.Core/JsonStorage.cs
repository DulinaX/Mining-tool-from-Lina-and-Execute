using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MiningScanner.Core;

/// <summary>Only callers select paths. This class never discovers or watches files.</summary>
internal static class JsonStorage
{
    internal const int MaximumFileBytes = 1_048_576;
    internal static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        AllowTrailingCommas = false,
        MaxDepth = 16
    };

    internal static string ReadText(string path)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length > MaximumFileBytes)
            throw new InvalidDataException("Die JSON-Datei ist größer als 1 MiB.");
        // A fixed upper bound is retained even if another process changes the file.
        byte[] bytes = new byte[checked((int)file.Length)];
        file.ReadExactly(bytes);
        if (file.ReadByte() != -1)
            throw new InvalidDataException("Die JSON-Datei hat sich beim Lesen verändert.");
        var text = new UTF8Encoding(false, true).GetString(bytes);
        return text.Length > 0 && text[0] == '\uFEFF' ? text[1..] : text;
    }

    internal static T Parse<T>(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        if (Encoding.UTF8.GetByteCount(json) > MaximumFileBytes)
            throw new InvalidDataException("Die JSON-Datei ist größer als 1 MiB.");
        RejectDuplicateProperties(json);
        return JsonSerializer.Deserialize<T>(json, Options)
               ?? throw new InvalidDataException("Die JSON-Datei enthält kein Objekt.");
    }

    // System.Text.Json otherwise accepts duplicate keys with last-value wins.
    private static void RejectDuplicateProperties(string json)
    {
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 16
        });
        Visit(document.RootElement);

        static void Visit(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in element.EnumerateObject())
                {
                    if (!names.Add(property.Name))
                        throw new InvalidDataException($"Doppeltes JSON-Feld: {property.Name}");
                    Visit(property.Value);
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var child in element.EnumerateArray())
                    Visit(child);
            }
        }
    }

    internal static void SaveAtomic<T>(string path, T value)
    {
        string json = JsonSerializer.Serialize(value, Options);
        byte[] bytes = new UTF8Encoding(false, true).GetBytes(json);
        if (bytes.Length > MaximumFileBytes)
            throw new InvalidDataException("Die JSON-Datei ist größer als 1 MiB.");
        string target = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(target)
                           ?? throw new ArgumentException("Ungültiger Speicherpfad.", nameof(path));
        Directory.CreateDirectory(directory);
        string temporary = Path.Combine(directory, "." + Path.GetFileName(target) + "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                file.Write(bytes);
                file.Flush(flushToDisk: true);
            }
            File.Move(temporary, target, overwrite: true);
        }
        finally
        {
            // Temporary data is configuration/reference JSON, never image data.
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    internal static void ValidateText(string? text, string label, int maximumLength, bool allowEmpty = false)
    {
        if (text is null || text.Length > maximumLength || (!allowEmpty && string.IsNullOrWhiteSpace(text))
            || text.Any(char.IsControl))
            throw new InvalidDataException($"Ungültiges Feld: {label}.");
    }
}
