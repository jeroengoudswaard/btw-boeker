using System.Text.Json;
using BtwBoeker.Application.Database;

namespace BtwBoeker.Infrastructure.Beveiliging;

/// <summary>Leest en schrijft het sleutelbestand (<c>*.btwdb.sleutel</c>) als JSON.</summary>
public static class Sleutelbestand
{
    private static readonly JsonSerializerOptions Opties = new() { WriteIndented = true };

    public static SleutelEnvelop Lees(string pad)
    {
        try
        {
            var envelop = JsonSerializer.Deserialize<SleutelEnvelop>(File.ReadAllBytes(pad), Opties);
            if (envelop?.Wachtwoord is null || envelop.Herstel is null)
            {
                throw new DatabaseBestandException($"Het sleutelbestand '{Path.GetFileName(pad)}' is onvolledig.");
            }

            if (envelop.Versie > SleutelKluis.HuidigeVersie)
            {
                throw new DatabaseBestandException(
                    "Het sleutelbestand is gemaakt met een nieuwere versie van BTW-Boeker. Werk de applicatie bij.");
            }

            return envelop;
        }
        catch (FileNotFoundException ex)
        {
            throw new DatabaseBestandException(
                $"Het sleutelbestand '{Path.GetFileName(pad)}' ontbreekt. Het moet in dezelfde map staan als de database.", ex);
        }
        catch (JsonException ex)
        {
            throw new DatabaseBestandException($"Het sleutelbestand '{Path.GetFileName(pad)}' is beschadigd.", ex);
        }
    }

    /// <summary>Schrijft atomair: eerst naar een tijdelijk bestand, daarna hernoemen.</summary>
    public static void Schrijf(string pad, SleutelEnvelop envelop)
    {
        var tijdelijk = pad + ".tmp";
        File.WriteAllBytes(tijdelijk, JsonSerializer.SerializeToUtf8Bytes(envelop, Opties));
        File.Move(tijdelijk, pad, overwrite: true);
    }
}
