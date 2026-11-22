using System.Text.Json;
using System.Text.Json.Serialization;

namespace MiningScanner.Core;

public sealed record SignatureEntry
{
    public string Id { get; init; } = "";
    public string NameEn { get; init; } = "";
    public string NameDe { get; init; } = "";
    public int BaseSignature { get; init; }
    public string Source { get; init; } = "";
    public string VerifiedPatch { get; init; } = "";
    public string Verification { get; init; } = "";
}

public sealed record SignatureMatch(SignatureEntry Entry, int Count);

public sealed class SignatureTable
{
    [JsonRequired]
    public int SchemaVersion { get; init; } = 1;
    [JsonRequired]
    public IReadOnlyList<SignatureEntry> Entries { get; init; } = [];

    public static SignatureTable Load(string path) => Parse(JsonStorage.ReadText(path));

    public static SignatureTable Parse(string json)
    {
        var table = JsonStorage.Parse<SignatureTable>(json);
        table.Validate();
        return table;
    }

    public string ToJson()
    {
        Validate();
        return JsonSerializer.Serialize(this, JsonStorage.Options);
    }

    public void Save(string path)
    {
        Validate();
        JsonStorage.SaveAtomic(path, this);
    }

    public void Validate()
    {
        if (SchemaVersion != 1)
            throw new InvalidDataException("Nicht unterstützte Version der Signaturtabelle.");
        if (Entries is null || Entries.Count > 500)
            throw new InvalidDataException("Die Signaturtabelle darf höchstens 500 Einträge enthalten.");
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in Entries)
        {
            if (entry is null)
                throw new InvalidDataException("Die Signaturtabelle enthält einen leeren Eintrag.");
            JsonStorage.ValidateText(entry.Id, "ID", 80);
            if (!entry.Id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
                throw new InvalidDataException("IDs dürfen nur ASCII-Buchstaben, Ziffern, - und _ enthalten.");
            if (!ids.Add(entry.Id))
                throw new InvalidDataException($"Doppelte Signatur-ID: {entry.Id}");
            JsonStorage.ValidateText(entry.NameEn, "Englischer Name", 120);
            JsonStorage.ValidateText(entry.NameDe, "Deutscher Name", 120);
            JsonStorage.ValidateText(entry.Source, "Quelle", 2048);
            JsonStorage.ValidateText(entry.VerifiedPatch, "Überprüfter Patchstand", 160);
            JsonStorage.ValidateText(entry.Verification, "Verifikation", 500);
            if (entry.BaseSignature is < 1 or > NumberNormalizer.MaximumValue)
                throw new InvalidDataException($"Ungültige Basissignatur: {entry.Id}");
        }
    }

    /// <summary>Every exact integer multiple is returned. There is no fuzzy match.</summary>
    public IReadOnlyList<SignatureMatch> Match(int value, int maxCount)
    {
        if (maxCount is < 1 or > 1000)
            throw new ArgumentOutOfRangeException(nameof(maxCount), "Die maximale Anzahl muss zwischen 1 und 1000 liegen.");
        if (value is < 1 or > NumberNormalizer.MaximumValue)
            return [];
        Validate();
        var matches = new List<SignatureMatch>();
        foreach (var entry in Entries)
        {
            if (value % entry.BaseSignature != 0)
                continue;
            int count = value / entry.BaseSignature;
            if (count <= maxCount)
                matches.Add(new SignatureMatch(entry, count));
        }
        return matches;
    }
}
