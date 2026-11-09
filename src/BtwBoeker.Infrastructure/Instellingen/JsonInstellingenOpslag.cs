using System.Text.Json;
using System.Text.Json.Serialization;
using BtwBoeker.Application.Instellingen;

namespace BtwBoeker.Infrastructure.Instellingen;

/// <summary>Bewaart gebruikersinstellingen als JSON, standaard in <c>%APPDATA%\BTW-Boeker\instellingen.json</c>.</summary>
public sealed class JsonInstellingenOpslag(string pad) : IInstellingenOpslag
{
    private static readonly JsonSerializerOptions Opties = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public GebruikersInstellingen Laad()
    {
        try
        {
            return JsonSerializer.Deserialize<GebruikersInstellingen>(File.ReadAllBytes(pad), Opties) ?? new();
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException or JsonException)
        {
            // Ontbrekende of beschadigde instellingen zijn nooit fataal: begin met de standaardwaarden.
            return new();
        }
    }

    public void Bewaar(GebruikersInstellingen instellingen)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(pad))!);
        var tijdelijk = pad + ".tmp";
        File.WriteAllBytes(tijdelijk, JsonSerializer.SerializeToUtf8Bytes(instellingen, Opties));
        File.Move(tijdelijk, pad, overwrite: true);
    }
}
