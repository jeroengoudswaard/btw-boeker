using Verenigd.Application.Inrichting;
using Verenigd.Application.Modules;
using Verenigd.Domain.Administraties;

namespace Verenigd.App.ViewModels;

/// <summary>
/// Besluit 239: elk onderdeel een eigen kleur, alleen in het pictogram (op een zachte tint) en in de dunne band boven de
/// pagina. Eén palet voor licht en donker: elke kleur haalt minstens 3 : 1 op de tint en op de grond van beide thema's.
/// De accentkleur van de administratie blijft voor knoppen, selectie en tellers.
/// </summary>
public static class Onderdeelkleuren
{
    /// <summary>Dekking van de tint achter een pictogram.</summary>
    public const double Tintdekking = 0.16;

    public static readonly Kleur Koraal = new(0xCF, 0x5A, 0x33);
    public static readonly Kleur Amber = new(0xAE, 0x72, 0x09);
    public static readonly Kleur Indigo = new(0x6A, 0x77, 0xD7);
    public static readonly Kleur Violet = new(0x9E, 0x63, 0xD2);
    public static readonly Kleur Blauw = new(0x27, 0x86, 0xB9);
    public static readonly Kleur Groenblauw = new(0x14, 0x8D, 0x7F);
    public static readonly Kleur Grijs = new(0x7F, 0x7F, 0x78);

    public static IReadOnlyList<Kleur> Palet { get; } = [Koraal, Amber, Indigo, Violet, Blauw, Groenblauw, Grijs];

    public static Kleur Voor(ModuleSoort module) => module switch
    {
        ModuleSoort.Leden => Koraal,
        ModuleSoort.Contributie => Amber,
        ModuleSoort.Boekhouding => Indigo,
        ModuleSoort.Begroting => Violet,
        ModuleSoort.Email => Blauw,
        ModuleSoort.Webapp => Groenblauw,
        _ => Grijs,
    };

    /// <summary>Het onderdeel waar een startkaart naartoe leidt, voor de kleur van het getal op de kaart.</summary>
    public static ModuleSoort OnderdeelVan(Startkaartsoort soort) => soort switch
    {
        Startkaartsoort.Incassorun or Startkaartsoort.Storneringen => ModuleSoort.Contributie,
        Startkaartsoort.Inrichting => ModuleSoort.Overzicht,
        _ => ModuleSoort.Boekhouding,
    };

    /// <summary>De kleur met <see cref="Tintdekking"/> over de grond gelegd.</summary>
    public static Kleur Tint(Kleur kleur, Kleur grond)
    {
        static byte Meng(byte voor, byte achter) => (byte)Math.Round((voor * Tintdekking) + (achter * (1 - Tintdekking)), MidpointRounding.AwayFromZero);
        return new Kleur(Meng(kleur.R, grond.R), Meng(kleur.G, grond.G), Meng(kleur.B, grond.B));
    }

    /// <summary>Een vaste kleur per groep (voor de initialen in Leden), zonder grijs; geen groep is grijs.</summary>
    public static Kleur VoorGroep(string? groep)
    {
        if (string.IsNullOrWhiteSpace(groep))
        {
            return Grijs;
        }

        var kleuren = Palet.Where(k => k != Grijs).ToList();
        var som = groep.Aggregate(0, (totaal, teken) => unchecked((totaal * 31) + teken));
        return kleuren[(int)((uint)som % (uint)kleuren.Count)];
    }

    public static string Initialen(string voornaam, string achternaam) =>
        string.Concat(new[] { voornaam, achternaam }.Where(d => !string.IsNullOrWhiteSpace(d)).Select(d => char.ToUpperInvariant(d.Trim()[0])));
}
