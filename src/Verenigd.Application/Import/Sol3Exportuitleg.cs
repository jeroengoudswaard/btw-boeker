namespace Verenigd.Application.Import;

/// <param name="IsVerplicht">De import heeft de kolom nodig (een leeg veld mag); anders levert SOL3 hem en negeert Verenigd hem.</param>
public sealed record Sol3Kolom(string Naam, bool IsVerplicht)
{
    public string Weergave => IsVerplicht ? Naam : $"{Naam} (optioneel)";
}

public sealed record Sol3Exportbeschrijving(Sol3Export Export, string Titel, string Doel, IReadOnlyList<Sol3Kolom> Kolommen)
{
    /// <summary>Voor <i>Kolomnamen kopiëren</i>: één naam per regel, in de volgorde van SOL3.</summary>
    public string Kopieertekst => string.Join(Environment.NewLine, Kolommen.Select(k => k.Naam));

    /// <summary>De naam waaronder het rapport in SOL3 wordt opgeslagen (klikroute van de gebruiker, besluit 187).</summary>
    public string Rapportnaam => $"invoer voor Verenigd, output #{(int)Export}";

    public string Aantaltekst => $"{Kolommen.Count} kolommen, waarvan {Kolommen.Count(k => k.IsVerplicht)} verplicht";
}

/// <summary>
/// Uitleg over de vier SOL3-exports (besluiten 20, 185 en 186) voor de startwizard en de importwizard. Welke kolommen
/// verplicht zijn, komt uit <see cref="ExportHerkenning"/>; de rest is wat SOL3 daarnaast levert.
/// </summary>
public static class Sol3Exportuitleg
{
    public const string Formaat =
        "Een CSV-bestand zoals SOL3 het exporteert met \"Alleen details\" (tekenset Windows-1252 of UTF-8). " +
        "Datums als dag-maand-jaar, bijvoorbeeld 19-09-2025. De volgorde van de kolommen maakt niet uit: Verenigd zoekt ze op naam. " +
        "Elke export bevat het lidnummer.";

    /// <summary>Hoe u in SOL3 een export maakt, zoals aangeleverd door de gebruiker (besluit 187); gelijk voor alle vier.</summary>
    public static IReadOnlyList<string> Klikroute { get; } =
    [
        "1. Log in op SOL3.",
        "2. Kies rechtsboven Meer › Rapporten.",
        "3. Kies Nieuw rapport en begin met een standaardrapport, bijvoorbeeld \"Import voor de Grote Clubactie\".",
        "4. Kies Bewerken en zet \"Voorbeeld automatisch bijwerken\" aan.",
        "5. Verwijder alle kolommen die u niet nodig hebt en kies de kolommen van de export (zie hieronder).",
        "6. Kies Uitvoeren.",
        "7. Kies in het driehoekje naast Bewerken eerst Opslaan, met de rapportnaam van de export (zie hieronder).",
        "8. Kies daarna in hetzelfde driehoekje Exporteren, met \"Alleen details\" en de indeling \"CSV, door komma's gescheiden\".",
    ];

    public static IReadOnlyList<Sol3Exportbeschrijving> Alle { get; } =
    [
        Maak(Sol3Export.LedenBasis, "Leden-basis", "Per lid de naam, geboortedatum, e-mail, telefoon en status.",
            "Lidnummer", "Voornaam", "Tussenvoegsel", "Achternaam", "Geboortedatum", "Gender", "E-mail", "Telefoon", "Startdatum functie",
            "Status", "Contactpersoon-ID", "Organisatie naam", "Organisatie plaats", "Naam organisatie eenheid"),
        Maak(Sol3Export.Huishouden, "Huishouden", "Per lid het adres en, als SOL3 dat heeft, het IBAN en de rekeninghouder.",
            "Lidnummer", "Contactpersoon-ID", "Lid straat", "Lid huisnummer", "Lid huisnr.toev.", "Postcode", "Lid plaats", "Land",
            "Betaalprofiel IBAN", "Betaalprofiel rekeningshoudersnaam", "Organisatie naam", "Organisatie plaats", "Naam organisatie eenheid"),
        Maak(Sol3Export.Voogden, "Voogden", "Per lid hooguit twee voogden met naam, e-mail, telefoon en mobiel.",
            "Lidnummer", "Voogd 1 naam", "Voogd 1 e-mail", "Voogd 1 telefoon", "Voogd 2 volledige naam", "Voogd 2 e-mail", "Voogd 2: Telefoon",
            "Voogd 1 mobiel", "Voogd 2 mobiel", "Organisatie naam", "Organisatie plaats", "Naam organisatie eenheid"),
        Maak(Sol3Export.Functies, "Functies", "Per functie de rol, de groep of speltak, de start- en einddatum en het lidtype.",
            "Rol", "Naam organisatie eenheid", "Startdatum functie", "Einddatum functie", "Role Membership Type", "Status", "Organisatie naam",
            "Organisatie plaats", "Lidnummer"),
    ];

    public static Sol3Exportbeschrijving Van(Sol3Export export) => Alle.Single(e => e.Export == export);

    private static Sol3Exportbeschrijving Maak(Sol3Export export, string naam, string doel, params string[] kolommen)
    {
        var verplicht = ExportHerkenning.VerplichteKolommen(export);
        return new Sol3Exportbeschrijving(
            export, $"Export {(int)export} · {naam}", doel,
            [.. kolommen.Select(k => new Sol3Kolom(k, verplicht.Contains(k, StringComparer.Ordinal)))]);
    }
}
