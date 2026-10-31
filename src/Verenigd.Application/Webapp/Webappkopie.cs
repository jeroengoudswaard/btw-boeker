using Verenigd.Domain.Leden;

namespace Verenigd.Application.Webapp;

/// <summary>
/// Maakt de kopie van ledengegevens voor de server (besluit 38). Alleen leden met een actieve functie in een
/// speltak gaan mee (de leiding ziet alleen de eigen speltak, besluit 33); geen adres en niets financieels (besluiten 35 en 229).
/// </summary>
public static class Webappkopie
{
    public static Ledenkopie Maak(IEnumerable<Persoon> personen, IEnumerable<Groep> groepen, DateOnly vandaag)
    {
        ArgumentNullException.ThrowIfNull(personen);
        ArgumentNullException.ThrowIfNull(groepen);

        var speltakken = groepen.Where(g => g.Soort == GroepSoort.Speltak).Select(g => g.Naam).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        var leden = new List<Kopielid>();
        foreach (var persoon in personen.Where(p => !p.IsUitgeschreven))
        {
            var functies = persoon.Functies
                .Where(f => f.Groep.Soort == GroepSoort.Speltak && f.IsActief(vandaag))
                .Select(f => new Kopiefunctie(f.Groep.Naam, f.Rol, f.Lidtype == Lidtype.Kaderlid))
                .Distinct()
                .OrderBy(f => f.Speltak, StringComparer.Ordinal).ThenBy(f => f.Rol, StringComparer.Ordinal)
                .ToList();
            if (functies.Count == 0)
            {
                continue;
            }

            leden.Add(new Kopielid(
                persoon.Id, persoon.Lidnummer.ToString(), persoon.Voornaam, persoon.Tussenvoegsel, persoon.Achternaam, persoon.Geboortedatum,
                persoon.Email, persoon.Telefoon, persoon.LaatstGewijzigdUtc,
                [.. persoon.Voogden.OrderBy(v => v.Volgnummer).Select(v => new Kopievoogd(v.Volgnummer, v.NaamOnbekend ? null : v.Naam, v.Email, v.Telefoon, v.Mobiel))],
                functies));
        }

        return new Ledenkopie(speltakken, leden);
    }
}
