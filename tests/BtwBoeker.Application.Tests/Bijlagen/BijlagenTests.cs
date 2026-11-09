using System.Text;
using BtwBoeker.Application.Bijlagen;
using BtwBoeker.Application.Boekhouding;
using BtwBoeker.Application.Tests.Boekhouding;
using BtwBoeker.Domain.Financien;
using BtwBoeker.Domain.Financien.Bijlagen;
using BtwBoeker.Domain.Financien.Boekhouding;

namespace BtwBoeker.Application.Tests.Bijlagen;

/// <summary>Koppelvoorstel, filter "Zonder bon" en de bijlagendienst (besluiten 83–88, 96, 97 en 135–138).</summary>
public sealed class BijlagenTests
{
    private static readonly DateTime Nu = new(2026, 10, 29, 9, 0, 0, DateTimeKind.Utc);
    private readonly BoekhoudingTestdata _data = new();
    private readonly NepPdfLezer _lezer = new();

    internal static byte[] Pdf(string tekst = "bon") => Encoding.ASCII.GetBytes("%PDF-1.7\n" + tekst);

    private BijlageDienst Dienst() => new(new BoekhoudingDienstTests.FakeSessie(_data), _lezer, new BoekhoudingDienstTests.VasteTijd(Nu));

    private Journaalpost Kasuitgave(DateOnly datum, long centen, bool boeken = true) =>
        Post(_data.Kasboek, datum, "Boodschappen", (_data.Energie, centen, 0), (_data.Kas, 0, centen), boeken);

    private Journaalpost Post(Dagboek dagboek, DateOnly datum, string omschrijving, (Grootboekrekening R, long D, long C) a, (Grootboekrekening R, long D, long C) b, bool boeken = true)
    {
        var post = new Journaalpost(BoekhoudingTestdata.Ver, dagboek, datum, omschrijving);
        post.Wijzig(dagboek, datum, omschrijving,
            [new RegelInvoer(a.R, null, new Bedrag(a.D), new Bedrag(a.C)), new RegelInvoer(b.R, null, new Bedrag(b.D), new Bedrag(b.C))]);
        if (boeken)
        {
            post.Boek(_data.Posten.Count + 1, Nu);
        }

        _data.Posten.Add(post);
        return post;
    }

    // ---- Koppelvoorstel ---------------------------------------------------------------------

    [Fact]
    public void Voorstel_is_een_post_met_precies_hetzelfde_bedrag_binnen_7_dagen_dichtstbij_eerst()
    {
        var ver = Kasuitgave(new DateOnly(2026, 10, 5), 2345);
        var dichtbij = Kasuitgave(new DateOnly(2026, 10, 13), 2345);
        Kasuitgave(new DateOnly(2026, 10, 12), 2346);
        Kasuitgave(new DateOnly(2026, 10, 20), 2345);
        var bijlage = new Bijlage(BoekhoudingTestdata.Ver, "bon.pdf", Pdf(), Nu);
        bijlage.ZetGegevens(new DateOnly(2026, 10, 12), new Bedrag(2345));

        var voorstel = KoppelVoorsteller.Voorstellen(_data.Stand, bijlage);

        Assert.Equal([dichtbij, ver], voorstel);
    }

    [Fact]
    public void Een_creditnota_wordt_voorgesteld_bij_een_ontvangst_met_hetzelfde_bedrag()
    {
        var bankboek = _data.Bankdagboek("BNK", "NL91ABNA0417164300");
        Post(bankboek, new DateOnly(2026, 10, 10), "Gamma", (_data.Energie, 1500, 0), (_data.Bank, 0, 1500)); // uitgave van 15,00
        var retour = Post(bankboek, new DateOnly(2026, 10, 11), "Gamma retour", (_data.Bank, 1500, 0), (_data.Energie, 0, 1500));
        Post(_data.Memoriaal, new DateOnly(2026, 10, 11), "Memoriaal", (_data.Energie, 1500, 0), (_data.Reserve, 0, 1500));
        var creditnota = new Bijlage(BoekhoudingTestdata.Ver, "creditnota.pdf", Pdf(), Nu);
        creditnota.ZetGegevens(new DateOnly(2026, 10, 12), new Bedrag(-1500));

        Assert.Equal([retour], KoppelVoorsteller.Voorstellen(_data.Stand, creditnota));
    }

    [Fact]
    public void Zonder_datum_of_bedrag_is_er_geen_voorstel()
    {
        Kasuitgave(new DateOnly(2026, 10, 12), 2345);
        var bijlage = new Bijlage(BoekhoudingTestdata.Ver, "bon.pdf", Pdf(), Nu);
        bijlage.ZetGegevens(null, new Bedrag(2345));

        Assert.Empty(KoppelVoorsteller.Voorstellen(_data.Stand, bijlage));
    }

    [Fact]
    public void Een_post_die_al_een_bijlage_heeft_komt_achteraan()
    {
        var metBon = Kasuitgave(new DateOnly(2026, 10, 12), 2345);
        var zonderBon = Kasuitgave(new DateOnly(2026, 10, 14), 2345);
        var bestaand = new Bijlage(BoekhoudingTestdata.Ver, "oud.pdf", Pdf("oud"), Nu);
        bestaand.KoppelAan(metBon.Id);
        _data.Bijlagen.Add(bestaand);
        var bijlage = new Bijlage(BoekhoudingTestdata.Ver, "bon.pdf", Pdf(), Nu);
        bijlage.ZetGegevens(new DateOnly(2026, 10, 12), new Bedrag(2345));

        Assert.Equal([zonderBon, metBon], KoppelVoorsteller.Voorstellen(_data.Stand, bijlage));
    }

    // ---- Zonder bon ---------------------------------------------------------------------------

    [Fact]
    public void Zonder_bon_zijn_uitgaven_en_ontvangsten_van_bank_of_kas_boven_10_euro_zonder_bijlage()
    {
        var bankboek = _data.Bankdagboek("BNK", "NL91ABNA0417164300");
        var groteKas = Kasuitgave(new DateOnly(2026, 10, 1), 2500);
        Kasuitgave(new DateOnly(2026, 10, 2), 1000); // precies de drempel: niet erboven
        var bankUit = Post(bankboek, new DateOnly(2026, 10, 3), "Gamma", (_data.Energie, 5000, 0), (_data.Bank, 0, 5000));
        var ontvangst = Post(bankboek, new DateOnly(2026, 10, 4), "Ontvangst", (_data.Bank, 9000, 0), (_data.Welpen, 0, 9000)); // geld erbij (besluit 145)
        Post(_data.Memoriaal, new DateOnly(2026, 10, 5), "Memoriaal", (_data.Energie, 7000, 0), (_data.Reserve, 0, 7000)); // geen bank of kas
        var metBon = Kasuitgave(new DateOnly(2026, 10, 6), 3000);
        var bon = new Bijlage(BoekhoudingTestdata.Ver, "bon.pdf", Pdf(), Nu);
        bon.KoppelAan(metBon.Id);
        _data.Bijlagen.Add(bon);
        var concept = Kasuitgave(new DateOnly(2026, 10, 7), 4000, boeken: false);

        Assert.Equal([groteKas, bankUit, ontvangst, concept], ZonderBon.Posten(_data.Stand, ZonderBon.StandaardDrempel));
        Assert.Equal(new Bedrag(1000), ZonderBon.StandaardDrempel);
        Assert.Equal(new Bedrag(5000), ZonderBon.Geldbeweging(bankUit));
        Assert.Equal(new Bedrag(-9000), ZonderBon.Geldbeweging(ontvangst));
    }

    // ---- Dienst -------------------------------------------------------------------------------

    [Fact]
    public async Task Toevoegen_aan_het_postvak_leest_datum_en_bedrag_uit_de_pdf()
    {
        _lezer.Tekst = "Datum 12-10-2026\nTotaal 23,45";

        var resultaat = await Dienst().VoegToeAsync(BoekhoudingTestdata.Ver, [new BijlageBestand("bon.pdf", Pdf())], postId: null);

        var bijlage = Assert.Single(resultaat.Toegevoegd);
        Assert.Empty(resultaat.Overgeslagen);
        Assert.True(bijlage.IsInPostvak);
        Assert.Equal(new DateOnly(2026, 10, 12), bijlage.Datum);
        Assert.Equal(new Bedrag(2345), bijlage.Bedrag);
        Assert.Equal(Pdf(), _data.Bijlageinhoud[bijlage.Id]);
        Assert.Equal(Pdf(), await Dienst().LaadInhoudAsync(BoekhoudingTestdata.Ver, bijlage.Id));
    }

    [Fact]
    public async Task Een_foto_of_onleesbare_pdf_wordt_toegevoegd_zonder_gegevens()
    {
        _lezer.Fout = true;

        var resultaat = await Dienst().VoegToeAsync(BoekhoudingTestdata.Ver,
            [new BijlageBestand("scan.pdf", Pdf()), new BijlageBestand("foto.jpg", [0xFF, 0xD8, 0xFF, 1])], postId: null);

        Assert.Equal(2, resultaat.Toegevoegd.Count);
        Assert.All(resultaat.Toegevoegd, b => Assert.Null(b.Datum));
        Assert.Equal(1, _lezer.Aantal); // een foto wordt niet gelezen
    }

    [Fact]
    public async Task Ongeldige_en_dubbele_bestanden_worden_overgeslagen_met_een_reden()
    {
        var dienst = Dienst();
        await dienst.VoegToeAsync(BoekhoudingTestdata.Ver, [new BijlageBestand("bon.pdf", Pdf())], postId: null);

        var resultaat = await dienst.VoegToeAsync(BoekhoudingTestdata.Ver,
            [new BijlageBestand("kopie.pdf", Pdf()), new BijlageBestand("notitie.txt", Encoding.ASCII.GetBytes("tekst"))], postId: null);

        Assert.Empty(resultaat.Toegevoegd);
        Assert.Collection(resultaat.Overgeslagen,
            o => { Assert.Equal("kopie.pdf", o.Bestandsnaam); Assert.Contains("al toegevoegd", o.Reden, StringComparison.Ordinal); },
            o => { Assert.Equal("notitie.txt", o.Bestandsnaam); Assert.Contains("PDF, JPG of PNG", o.Reden, StringComparison.Ordinal); });
        Assert.Single(_data.Bijlagen);
    }

    [Fact]
    public async Task Toevoegen_aan_een_geboekte_post_koppelen_ontkoppelen_en_verwijderen()
    {
        var post = Kasuitgave(new DateOnly(2026, 10, 12), 2345);
        var dienst = Dienst();

        var bijlage = Assert.Single((await dienst.VoegToeAsync(BoekhoudingTestdata.Ver, [new BijlageBestand("bon.pdf", Pdf())], post.Id)).Toegevoegd);
        Assert.Equal(post.Id, bijlage.PostId);
        Assert.True(post.IsGeboekt);

        await dienst.OntkoppelAsync(BoekhoudingTestdata.Ver, bijlage.Id);
        Assert.True(_data.Bijlagen.Single().IsInPostvak);

        await dienst.KoppelAsync(BoekhoudingTestdata.Ver, bijlage.Id, post.Id);
        Assert.Equal(post.Id, _data.Bijlagen.Single().PostId);

        await dienst.VerwijderAsync(BoekhoudingTestdata.Ver, bijlage.Id);
        Assert.Empty(_data.Bijlagen);
        Assert.Empty(_data.Bijlageinhoud);
    }

    [Fact]
    public async Task Koppelen_aan_een_onbekende_post_kan_niet()
    {
        var dienst = Dienst();
        var bijlage = Assert.Single((await dienst.VoegToeAsync(BoekhoudingTestdata.Ver, [new BijlageBestand("bon.pdf", Pdf())], null)).Toegevoegd);

        await Assert.ThrowsAsync<InvalidOperationException>(() => dienst.KoppelAsync(BoekhoudingTestdata.Ver, bijlage.Id, Guid.NewGuid()));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            dienst.VoegToeAsync(BoekhoudingTestdata.Ver, [new BijlageBestand("x.pdf", Pdf("x"))], Guid.NewGuid()));
    }

    [Fact]
    public async Task Datum_en_bedrag_kunnen_worden_ingevuld()
    {
        var dienst = Dienst();
        var bijlage = Assert.Single((await dienst.VoegToeAsync(BoekhoudingTestdata.Ver, [new BijlageBestand("foto.png", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A])], null)).Toegevoegd);

        await dienst.WijzigGegevensAsync(BoekhoudingTestdata.Ver, bijlage.Id, new DateOnly(2026, 10, 1), new Bedrag(999));

        Assert.Equal(new DateOnly(2026, 10, 1), _data.Bijlagen.Single().Datum);
        Assert.Equal(new Bedrag(999), _data.Bijlagen.Single().Bedrag);
    }

    private sealed class NepPdfLezer : IPdfTekstLezer
    {
        public string Tekst { get; set; } = string.Empty;
        public bool Fout { get; set; }
        public int Aantal { get; private set; }

        public string? LeesTekst(byte[] pdf)
        {
            Aantal++;
            return Fout ? null : Tekst;
        }
    }
}
