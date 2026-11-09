using BtwBoeker.Application.Boekhouding;

namespace BtwBoeker.Application.Tests.Boekhouding;

/// <summary>Een verzonnen Excel-boekhouding 2025 met T-rekeningen, voor de tests van het historische boekjaar.</summary>
public static class HistorieVoorbeeld
{
    /// <summary>Voorblad 2025: 600 reserve, 1100 bank (balans), 4115 energie en 8020 contributie (W&amp;V), 4200 zonder T-rekening.</summary>
    public static Werkblad Voorblad()
    {
        var blad = new Werkblad();
        blad.Zet(1, 1, 2025.0);
        blad.Zet(3, 2, "Vermogen");
        blad.Zet(4, 1, 600.0); blad.Zet(4, 2, "Algemene Reserve"); blad.Zet(4, 10, 100.0);
        blad.Zet(5, 2, "Kas Bank");
        blad.Zet(6, 1, 1100.0); blad.Zet(6, 2, "Bank"); blad.Zet(6, 9, 150.0);
        blad.Zet(7, 2, "Kosten");
        blad.Zet(8, 1, 4115.0); blad.Zet(8, 2, "Energie"); blad.Zet(8, 7, 30.0);
        blad.Zet(9, 1, 4200.0); blad.Zet(9, 2, "Huur"); blad.Zet(9, 7, 20.0);
        blad.Zet(10, 2, "Contributie");
        blad.Zet(11, 1, 8020.0); blad.Zet(11, 2, "Contributie Welpen"); blad.Zet(11, 8, 80.0);
        blad.Zet(12, 3, 0.0, "=SUM(C3:C11)");
        return blad;
    }

    /// <summary>Twee tabbladen met T-rekeningen naast elkaar: per rekening datum, omschrijving, debet, credit.</summary>
    public static IReadOnlyList<GenoemdWerkblad> Bladen()
    {
        var kasbank = new Werkblad();
        // 1100 Bank in kolommen A-D: beginsaldo zonder datum, twee maandtotalen, een SUM-regel.
        kasbank.Zet(1, 2, "Kas Bank");
        kasbank.Zet(2, 1, 1100.0); kasbank.Zet(2, 2, "Bank"); kasbank.Zet(2, 3, "Debet"); kasbank.Zet(2, 4, "Credit");
        kasbank.Zet(3, 2, "Beginsaldo"); kasbank.Zet(3, 3, 100.0);
        kasbank.Zet(4, 1, new DateTime(2025, 1, 31)); kasbank.Zet(4, 2, "Januari"); kasbank.Zet(4, 3, 80.0);
        kasbank.Zet(5, 1, new DateTime(2025, 2, 28)); kasbank.Zet(5, 2, "Februari"); kasbank.Zet(5, 4, 30.0);
        kasbank.Zet(6, 3, 180.0, "=SUM(C3:C5)"); kasbank.Zet(6, 4, 30.0, "=SUM(D3:D5)");
        // 600 Reserve in kolommen G-J: een regel met een datum vóór 2025.
        kasbank.Zet(2, 7, 600.0); kasbank.Zet(2, 8, "Reserve"); kasbank.Zet(2, 9, "Debet"); kasbank.Zet(2, 10, "Credit");
        kasbank.Zet(3, 7, new DateTime(2019, 5, 1)); kasbank.Zet(3, 8, "Inbreng"); kasbank.Zet(3, 10, 100.0);

        var kosten = new Werkblad();
        kosten.Zet(2, 1, 4115.0); kosten.Zet(2, 2, "Energie"); kosten.Zet(2, 3, "Debet"); kosten.Zet(2, 4, "Credit");
        kosten.Zet(3, 1, new DateTime(2025, 3, 3)); kosten.Zet(3, 2, "Stroom"); kosten.Zet(3, 3, 25.0); // Voorblad zegt 30: afwijking
        kosten.Zet(2, 7, 8020.0); kosten.Zet(2, 8, "Contributie"); kosten.Zet(2, 9, "Debet"); kosten.Zet(2, 10, "Credit");
        kosten.Zet(3, 7, new DateTime(2025, 4, 1)); kosten.Zet(3, 8, "Incasso"); kosten.Zet(3, 10, 80.0);
        kosten.Zet(4, 7, new DateTime(2025, 4, 2)); kosten.Zet(4, 8, "Leeg"); // geen bedrag: overslaan
        kosten.Zet(2, 13, 9999.0); kosten.Zet(2, 14, "Diversen"); kosten.Zet(2, 15, "Debet"); kosten.Zet(2, 16, "Credit");
        kosten.Zet(3, 13, new DateTime(2025, 6, 1)); kosten.Zet(3, 14, "Iets"); kosten.Zet(3, 15, 5.0);

        return [new("Voorblad", Voorblad()), new("KasBank", kasbank), new("Kosten", kosten)];
    }
}
