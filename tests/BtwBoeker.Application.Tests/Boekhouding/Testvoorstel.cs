using BtwBoeker.Application.Boekhouding;
using BtwBoeker.Domain.Financien;
using BtwBoeker.Domain.Financien.Boekhouding;

namespace BtwBoeker.Application.Tests.Boekhouding;

/// <summary>
/// Verzonnen inrichtvoorstellen voor tests (geen echte gegevens). Gedeeld met de App- en Infrastructure-tests via een
/// gekoppeld bestand.
/// </summary>
internal static class Testvoorstel
{
    public static Inrichtrekening Balans(int nummer, string naam, string rubriek, decimal debet = 0, decimal credit = 0, RekeningRol rol = RekeningRol.Geen) =>
        new(nummer, naam, rubriek, RekeningSoort.Balans, rol, false, Centen(debet), Centen(credit));

    public static Inrichtrekening Resultaat(int nummer, string naam, string rubriek, ResultaatKant? kant = null) =>
        new(nummer, naam, rubriek, RekeningSoort.Resultaat, RekeningRol.Geen, false, Bedrag.Nul, Bedrag.Nul) { Kant = kant };

    /// <summary>Een voorstel met beginsaldi per 1 januari van <paramref name="beginjaar"/>.</summary>
    public static Inrichtvoorstel Maak(int beginjaar, params Inrichtrekening[] rekeningen) => new(beginjaar - 1, rekeningen, []);

    /// <summary>
    /// Reserve, kas en bank met beginsaldi per 1-1-2026, een kostenrekening (4115 Energie) en een opbrengstrekening
    /// (8020 Verhuur huisje). In evenwicht zolang <paramref name="bank"/> 799,50 is.
    /// </summary>
    public static Inrichtvoorstel Standaard(decimal bank = 799.5m) => Maak(2026,
        Balans(600, "Algemene Reserve", "Vermogen", credit: 1000, rol: RekeningRol.Reserve),
        Balans(1000, "Kas", "Kas Bank", debet: 200.5m, rol: RekeningRol.Kas),
        Balans(1100, "Bank", "Kas Bank", debet: bank),
        Resultaat(4115, "Energie", "Parkkosten", ResultaatKant.Lasten),
        Resultaat(8020, "Verhuur huisje", "Verhuur", ResultaatKant.Baten));

    private static Bedrag Centen(decimal euro) => new((long)Math.Round(euro * 100m, MidpointRounding.AwayFromZero));
}
