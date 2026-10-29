namespace Verenigd.Application.Weergaven;

/// <summary>
/// Eén begrip in twee weergaven (besluit 127). <see cref="Uitleg"/> verschijnt alleen in Eenvoudig, als tooltip. Een
/// underscore geeft de toegangstoets aan (Alt+letter).
/// </summary>
public sealed record Term(string Sleutel, string Expert, string Eenvoudig, string? Uitleg = null);

/// <summary>
/// De woordenlijst voor het tabblad Financiën (besluiten 127, 128, 130 en 133). Alleen visueel: de database, afdrukken en
/// export blijven formeel (besluit 131). De XAML gebruikt de sleutels als <c>{DynamicResource T.Sleutel}</c> en
/// <c>{DynamicResource U.Sleutel}</c>; een architectuurtest bewaakt dat elke sleutel hier staat.
/// </summary>
public static class Woordenlijst
{
    private const string UitlegDebetCredit =
        "Debet en credit zijn de twee kanten van een boeking. Bij een bezit (zoals bank of kas) is debet erbij; bij een schuld of de reserve is credit erbij.";

    public static IReadOnlyList<Term> Termen { get; } =
    [
        // Begrippen (in zinnen, zonder toegangstoets)
        new("Boekhouding", "Boekhouding", "Financiën"),
        new("Journaal", "Journaal", "Alle boekingen", "Alle boekingen van het jaar, ook de nog niet definitieve."),
        new("Bank", "Bank", "Banktransacties", "De regels van je bankafschriften; deel ze in een potje in."),
        new("Boekingsregels", "Boekingsregels", "Automatisch indelen", "Regels die banktransacties automatisch in het juiste potje zetten."),
        new("Rekeningschema", "Rekeningschema", "Potjes", "Potjes zijn categorieën waarin je inkomsten, uitgaven, bezittingen en schulden bijhoudt."),
        new("Grootboekkaart", "Grootboekkaart", "Inhoud van een potje", "Alle boekingen in één potje, met de stand na elke boeking."),
        new("Rapporten", "Rapporten", "Overzichten"),
        new("Boekjaren", "Boekjaren", "Jaren"),
        new("Saldibalans", "Saldibalans", "Stand van alle potjes", "Per potje het totaal dat erbij en eraf ging, en de stand."),
        new("Balans", "Balans", "Bezittingen en schulden", "Wat de vereniging bezit (bank, kas) en wat ze verschuldigd is, op 31 december."),
        new("WinstEnVerlies", "Staat van baten en lasten", "Inkomsten en uitgaven", "Alle inkomsten en uitgaven van het jaar en wat er overblijft."),
        new("Concept", "Concept", "Nog niet definitief", "Een voorlopige boeking: nog te wijzigen of te verwijderen, en telt nog niet mee."),
        new("Geboekt", "Geboekt", "Definitief", "Een definitieve boeking telt mee en kan alleen nog worden teruggedraaid."),
        new("Post", "post", "boeking"),
        new("Posten", "posten", "boekingen"),
        new("Rekening", "Rekening", "Potje"),
        new("Rubriek", "Rubriek", "Groep", "Een groep potjes, zoals Huisvesting of Contributie."),
        new("Debet", "Debet", "Debet", UitlegDebetCredit),
        new("Credit", "Credit", "Credit", UitlegDebetCredit),
        new("Uitgave", "Debet", "Uitgave"),
        new("Inkomst", "Credit", "Inkomst"),
        new("Erbij", "Debet", "Erbij"),
        new("Eraf", "Credit", "Eraf"),
        new("Bijlage", "Bijlage", "Bon"),
        new("Bijlagen", "Bijlagen", "Bonnen", "Bonnen en facturen bij een boeking; ze worden versleuteld bewaard en gaan mee in de back-up."),

        // Subtabbladen en knoppen (met toegangstoets)
        new("Tab.Journaal", "_Journaal", "_Alle boekingen"),
        new("Tab.Bank", "B_ank", "Ban_ktransacties"),
        new("Tab.Boekingsregels", "Boekingsregel_s", "Automati_sch indelen"),
        new("Tab.Rekeningschema", "_Rekeningschema", "P_otjes"),
        new("Tab.Grootboekkaart", "_Grootboekkaart", "_Inhoud van een potje"),
        new("Tab.Rapporten", "Ra_pporten", "Overzi_chten"),
        new("Tab.Boekjaren", "Boek_jaren", "_Jaren"),
        new("Knop.NieuwePost", "_Nieuwe post", "_Nieuwe boeking"),
        new("Knop.Boeken", "_Boeken", "_Definitief maken"),
        new("Knop.ConceptVerwijderen", "Concept _verwijderen", "Voorlopige boeking _verwijderen"),
        new("Knop.Tegenboeken", "_Tegenboeken...", "_Terugdraaien..."),
        new("Knop.ConceptenBoeken", "Concepten b_oeken", "Alles definitief m_aken"),
        new("Knop.Toewijzen", "_Toewijzen", "I_ndelen in potje"),
        new("Knop.NieuweRekening", "Nieuwe _rekening", "Nie_uw potje"),
        new("Knop.RubriekToevoegen", "R_ubriek toevoegen", "_Groep toevoegen"),
        new("Label.Dagboek", "Dag_boek", "Soort boekin_g"),
        new("Label.Rekening", "Re_kening", "Pot_je"),
        new("Tab.Postvak", "Post_vak bonnen", "Post_vak bonnen", "Zet hier bonnen neer; de app stelt voor bij welke boeking ze horen."),
        new("Knop.ZonderBon", "_Zonder bon", "_Zonder bon", "Uitgaven van bank of kas boven € 10 zonder bon."),
        new("Knop.BijlageToevoegen", "Bijlag_e toevoegen...", "Bon toevo_egen..."),
        new("Tip.Toewijzen", "Maakt een conceptpost (Enter)", "Maakt een voorlopige boeking (Enter)"),
        new("Tip.Tegenboeken", "Een geboekte post corrigeren met een post met omgekeerde bedragen", "Een definitieve boeking terugdraaien met een correctieboeking"),

        // Zinnen
        new("Tekst.SelecteerPost", "Selecteer een post of kies Nieuwe post (Ctrl+N).", "Selecteer een boeking of kies Nieuwe boeking (Ctrl+N)."),
        new("Tekst.TeVerwerken", " te verwerken · ", " nog in te delen · "),
        new("Tekst.ConceptAantal", " concept", " nog niet definitief"),
        new("Tekst.ConceptBank",
            "Een concept kunt u in het Journaal controleren, splitsen of verwijderen; de bankregel staat vast.",
            "Een voorlopige boeking kun je bij Alle boekingen controleren, splitsen of verwijderen; de bankregel staat vast."),
        new("Band.Historisch", "Historisch boekjaar, overgenomen uit Excel, niet dubbel geboekt.", "Dit jaar komt uit de oude Excel-boekhouding."),
        new("Band.HistorischUitleg",
            "Alleen-lezen. Het Journaal is leeg; de regels staan per rekening bij Grootboekkaart, de rapporten volgen het Voorblad.",
            "Je kunt het alleen bekijken. Bij Alle boekingen staat niets; de regels zie je per potje bij Inhoud van een potje, de overzichten volgen het Voorblad."),

        // Meldingen ({0} = nummer of aantal)
        new("Melding.PostGeboekt", "Post {0} is geboekt.", "Boeking {0} is definitief gemaakt."),
        new("Melding.Tegenboeking", "Tegenboeking {0} is geboekt.", "Correctieboeking {0} is gemaakt; de oorspronkelijke boeking is teruggedraaid."),
        new("Melding.Geboekt", "geboekt", "definitief gemaakt"),
        new("Melding.GeenBankconcepten", "Er zijn geen bankconcepten om te boeken.", "Er zijn geen voorlopige banktransacties om definitief te maken."),
    ];

    private static readonly Dictionary<string, Term> PerSleutel = Termen.ToDictionary(t => t.Sleutel, StringComparer.Ordinal);

    /// <exception cref="KeyNotFoundException">De sleutel staat niet in de woordenlijst.</exception>
    public static Term Zoek(string sleutel) =>
        PerSleutel.TryGetValue(sleutel, out var term) ? term : throw new KeyNotFoundException($"De term '{sleutel}' staat niet in de woordenlijst.");
}
