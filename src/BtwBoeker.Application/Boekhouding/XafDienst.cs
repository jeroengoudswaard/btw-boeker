using System.Globalization;
using System.Text;
using BtwBoeker.Application.Database;
using BtwBoeker.Domain.Administraties;

namespace BtwBoeker.Application.Boekhouding;

/// <summary>
/// Haalt de gegevens voor een auditfile (XAF 3.2) uit de database: de boekhouding van één administratie en de naam.
/// </summary>
public sealed class XafDienst(IDatabaseSessie sessie, TimeProvider tijd)
{
    public const string Extensie = ".xaf";

    public async Task<XafResultaat> MaakAsync(AdministratieSoort administratie, int jaar, string softwareversie, CancellationToken annulering = default)
    {
        var stand = await sessie.Boekhouding.LaadAsync(administratie, annulering).ConfigureAwait(false);
        var naam = (await sessie.Administraties.HaalAlleAsync(annulering).ConfigureAwait(false))
            .FirstOrDefault(a => a.Soort == administratie)?.Naam ?? administratie.ToString();
        var vandaag = DateOnly.FromDateTime(tijd.GetLocalNow().DateTime);
        return XafSchrijver.Schrijf(new XafInvoer(stand, jaar, naam, vandaag, softwareversie));
    }

    /// <summary>Bijvoorbeeld <c>auditfile-vakantiehuis-de-voorbeeld-2026.xaf</c>.</summary>
    public static string Bestandsnaam(string administratienaam, int jaar)
    {
        var naam = new StringBuilder();
        foreach (var teken in (administratienaam ?? string.Empty).Trim().ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(teken))
            {
                naam.Append(teken);
            }
            else if (naam.Length > 0 && naam[^1] != '-')
            {
                naam.Append('-');
            }
        }

        var deel = naam.ToString().Trim('-');
        return $"auditfile-{(deel.Length == 0 ? "administratie" : deel)}-{jaar.ToString(CultureInfo.InvariantCulture)}{Extensie}";
    }
}
