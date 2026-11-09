using BtwBoeker.Application.Database;
using BtwBoeker.Domain.Administraties;
using BtwBoeker.Domain.Financien.Boekhouding;

namespace BtwBoeker.Application.Boekhouding;

/// <summary>Wat er gebeurt met een T-rekening die niet in het rekeningschema staat.</summary>
public enum OnbekendeRekening
{
    Overslaan,

    /// <summary>Toevoegen als inactieve rekening, zodat de regels zichtbaar blijven.</summary>
    Toevoegen,
}

/// <summary>
/// Het historische boekjaar uit de Excel-boekhouding (besluiten 122–126): een afgesloten, alleen-lezen boekjaar met de
/// regels van de T-rekeningen en de totalen van het Voorblad. Het telt nooit mee in het grootboek.
/// </summary>
public sealed class HistorieDienst(IDatabaseSessie sessie, TimeProvider tijd)
{
    /// <summary>Geeft het aantal overgenomen regels. Vooraf wordt een back-up gemaakt.</summary>
    /// <exception cref="InvalidOperationException">Geen rekeningschema, of het boekjaar bestaat al.</exception>
    public async Task<int> ImporteerAsync(
        AdministratieSoort administratie, HistorieVoorstel voorstel, OnbekendeRekening onbekend, CancellationToken annulering = default)
    {
        ArgumentNullException.ThrowIfNull(voorstel);
        Controleer(await sessie.Boekhouding.LaadAsync(administratie, annulering).ConfigureAwait(false), voorstel.Jaar);
        await sessie.MaakBackupAsync(BoekhoudingDienst.BackupPad(sessie, tijd, $"historie-{voorstel.Jaar}"), annulering).ConfigureAwait(false);
        return await sessie.Boekhouding.PasToeAsync(administratie, stand =>
        {
            Controleer(stand, voorstel.Jaar);
            var toevoegen = new List<object> { Boekjaar.Historisch(administratie, voorstel.Jaar, tijd.GetUtcNow().UtcDateTime) };
            var aantal = 0;
            foreach (var bron in voorstel.Rekeningen)
            {
                var rekening = stand.Rekeningen.FirstOrDefault(r => r.Nummer == bron.Nummer);
                if (rekening is null)
                {
                    if (onbekend == OnbekendeRekening.Overslaan || bron.Regels.Count == 0)
                    {
                        continue;
                    }

                    rekening = new Grootboekrekening(administratie, bron.Nummer, bron.Naam, bron.Soort, null);
                    rekening.Deactiveer();
                    toevoegen.Add(rekening);
                }

                if (bron.OpVoorblad)
                {
                    toevoegen.Add(new HistorischTotaal(administratie, voorstel.Jaar, rekening, bron.TotaalDebet, bron.TotaalCredit));
                }

                var volgorde = 0;
                foreach (var regel in bron.Regels)
                {
                    toevoegen.Add(new HistorischeRegel(administratie, voorstel.Jaar, rekening, regel.Datum, regel.Omschrijving, regel.Debet, regel.Credit, regel.Soort, ++volgorde));
                    aantal++;
                }
            }

            var wijziging = new BoekhoudingWijziging<int>(aantal);
            wijziging.Toevoegen.AddRange(toevoegen);
            return wijziging;
        }, annulering).ConfigureAwait(false);
    }

    private static void Controleer(BoekhoudingStand stand, int jaar)
    {
        if (!stand.IsIngericht)
        {
            throw new InvalidOperationException("Neem eerst het rekeningschema over; daarna kan de historie worden toegevoegd.");
        }

        if (stand.Boekjaar(jaar) is not null)
        {
            throw new InvalidOperationException($"Boekjaar {jaar} bestaat al; de historie kan alleen in een nieuw jaar worden overgenomen.");
        }
    }
}
