using Verenigd.Application.Boekhouding;
using Verenigd.Domain.Administraties;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Financien.Bank;
using Verenigd.Domain.Financien.Begrotingen;
using Verenigd.Domain.Financien.Bijlagen;
using Verenigd.Domain.Financien.Debiteuren;
using Verenigd.Domain.Financien.Facturen;
using Verenigd.Domain.Financien.Relaties;
using Verenigd.Domain.Financien.Boekhouding;

namespace Verenigd.Application.Tests.Boekhouding;

/// <summary>Een kleine, verzonnen administratie in de opbouw van het huidige rekeningschema.</summary>
internal sealed class BoekhoudingTestdata : IBoekhoudingRepository
{
    public const AdministratieSoort Ver = AdministratieSoort.Vereniging;
    private static readonly DateTime Nu = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
    private int _id = 1;

    public BoekhoudingTestdata(bool ingericht = true)
    {
        if (!ingericht)
        {
            return;
        }

        var vermogen = Rubriek("Vermogen");
        var kasbank = Rubriek("Kas Bank");
        var kosten = Rubriek("Terreinkosten");
        var contributie = Rubriek("Contributie");
        Reserve = Rekening(600, "Algemene reserve", RekeningSoort.Balans, vermogen, RekeningRol.Reserve);
        Kas = Rekening(1000, "Kas", RekeningSoort.Balans, kasbank, RekeningRol.Kas);
        Bank = Rekening(1100, "Bank", RekeningSoort.Balans, kasbank);
        Energie = Rekening(4115, "Energie", RekeningSoort.Resultaat, kosten);
        Welpen = Rekening(8020, "Contributie Welpen", RekeningSoort.Resultaat, contributie);
        Memoriaal = Dagboek(new Dagboek(Ver, DagboekSoort.Memoriaal, "MEM", "Memoriaal", null));
        Kasboek = Dagboek(new Dagboek(Ver, DagboekSoort.Kas, "KAS", "Kas", Kas));
        Boekjaren.Add(new Boekjaar(Ver, 2026));
    }

    public List<Rubriek> Rubrieken { get; } = [];
    public List<Grootboekrekening> Rekeningen { get; } = [];
    public List<Dagboek> Dagboeken { get; } = [];
    public List<Boekjaar> Boekjaren { get; } = [];
    public List<Journaalpost> Posten { get; } = [];
    public List<Bankafschrift> Bankafschriften { get; } = [];
    public List<Bankmutatie> Bankmutaties { get; } = [];
    public List<Boekingsregel> Boekingsregels { get; } = [];
    public List<Debiteurmutatie> Debiteurmutaties { get; } = [];
    public List<Relatie> Relaties { get; } = [];
    public List<Verkoopfactuur> Verkoopfacturen { get; } = [];
    public Factuurinstellingen? Factuurinstellingen { get; set; }
    public List<HistorischeRegel> HistorischeRegels { get; } = [];
    public List<HistorischTotaal> HistorischeTotalen { get; } = [];
    public List<Bijlage> Bijlagen { get; } = [];
    public List<Begroting> Begrotingen { get; } = [];
    public Dictionary<Guid, byte[]> Bijlageinhoud { get; } = [];

    /// <summary>De runs en herincasso's van de contributiekant (in tests gedeeld met <c>IncassoTestdata</c>).</summary>
    public Func<IReadOnlyList<Incassorun>> Incassoruns { get; set; } = () => [];

    public Func<IReadOnlyList<Herincasso>> Herincassos { get; set; } = () => [];

    public Grootboekrekening Reserve { get; } = null!;
    public Grootboekrekening Kas { get; } = null!;
    public Grootboekrekening Bank { get; } = null!;
    public Grootboekrekening Energie { get; } = null!;
    public Grootboekrekening Welpen { get; } = null!;
    public Dagboek Memoriaal { get; } = null!;
    public Dagboek Kasboek { get; } = null!;

    public BoekhoudingStand Stand => new(Ver, Rubrieken.ToList(), Rekeningen.ToList(), Dagboeken.ToList(), Boekjaren.ToList(), Posten.ToList())
    {
        Bankafschriften = Bankafschriften.ToList(),
        Bankmutaties = Bankmutaties.ToList(),
        Boekingsregels = Boekingsregels.OrderBy(r => r.Volgorde).ToList(),
        Debiteurmutaties = Debiteurmutaties.ToList(),
        Relaties = Relaties.ToList(),
        Verkoopfacturen = Verkoopfacturen.ToList(),
        Factuurinstellingen = Factuurinstellingen,
        HistorischeRegels = HistorischeRegels.ToList(),
        HistorischeTotalen = HistorischeTotalen.ToList(),
        Bijlagen = Bijlagen.Where(b => b.Administratie == Ver).ToList(),
        Begrotingen = Begrotingen.ToList(),
        Incassoruns = Incassoruns(),
        Herincassos = Herincassos(),
    };

    /// <summary>Een bankdagboek op <see cref="Bank"/> (of een andere rekening) voor een eigen IBAN.</summary>
    public Dagboek Bankdagboek(string code, string iban, Grootboekrekening? rekening = null) =>
        Dagboek(new Dagboek(Ver, DagboekSoort.Bank, code, code, rekening ?? Bank, Iban.Maak(iban)));

    public Rubriek Rubriek(string naam)
    {
        var rubriek = MetId(new Rubriek(Ver, naam, Rubrieken.Count + 1));
        Rubrieken.Add(rubriek);
        return rubriek;
    }

    public Grootboekrekening Rekening(int nummer, string naam, RekeningSoort soort, Rubriek? rubriek, RekeningRol rol = RekeningRol.Geen)
    {
        var rekening = MetId(new Grootboekrekening(Ver, nummer, naam, soort, rubriek, rol));
        Rekeningen.Add(rekening);
        return rekening;
    }

    /// <summary>Een geboekte post met regels (rekening, debet, credit) in centen.</summary>
    public Journaalpost Geboekt(DateOnly datum, string omschrijving, params (Grootboekrekening Rekening, long Debet, long Credit)[] regels) =>
        Geboekt(datum, omschrijving, PostSoort.Gewoon, regels);

    public Journaalpost Geboekt(DateOnly datum, string omschrijving, PostSoort soort, params (Grootboekrekening Rekening, long Debet, long Credit)[] regels)
    {
        var post = new Journaalpost(Ver, Memoriaal, datum, omschrijving, soort);
        post.Wijzig(Memoriaal, datum, omschrijving, regels.Select(r => new RegelInvoer(r.Rekening, null, new Bedrag(r.Debet), new Bedrag(r.Credit))));
        post.Boek(Posten.Count(p => p.IsGeboekt) + 1, Nu);
        Posten.Add(post);
        return post;
    }

    public Task<BoekhoudingStand> LaadAsync(AdministratieSoort administratie, CancellationToken annulering = default) => Task.FromResult(Stand);

    public Task<byte[]?> LaadBijlageInhoudAsync(Guid bijlageId, CancellationToken annulering = default) =>
        Task.FromResult(Bijlageinhoud.GetValueOrDefault(bijlageId));

    public Task<T> PasToeAsync<T>(AdministratieSoort administratie, Func<BoekhoudingStand, BoekhoudingWijziging<T>> wijziging, CancellationToken annulering = default)
    {
        var resultaat = wijziging(Stand);
        foreach (var item in resultaat.Verwijderen)
        {
            switch (item)
            {
                case Journaalpost post: Posten.Remove(post); break;
                case Boekingsregel regel: Boekingsregels.Remove(regel); break;
                case Debiteurmutatie mutatie: Debiteurmutaties.Remove(mutatie); break;
                case Verkoopfactuur factuur: Verkoopfacturen.Remove(factuur); break;
                case Bijlage bijlage: Bijlagen.Remove(bijlage); Bijlageinhoud.Remove(bijlage.Id); break;
                default: throw new NotSupportedException(item.GetType().Name);
            }
        }

        foreach (var item in resultaat.Toevoegen)
        {
            switch (item)
            {
                case Rubriek r: Rubrieken.Add(MetId(r)); break;
                case Grootboekrekening g: Rekeningen.Add(MetId(g)); break;
                case Dagboek d: Dagboeken.Add(MetId(d)); break;
                case Boekjaar b: Boekjaren.Add(b); break;
                case Journaalpost p: Posten.Add(p); break;
                case Bankafschrift a: Bankafschriften.Add(MetId(a)); break;
                case Bankmutatie m: Bankmutaties.Add(m); break;
                case Boekingsregel r: Boekingsregels.Add(MetId(r)); break;
                case Debiteurmutatie d: Debiteurmutaties.Add(d); break;
                case Relatie relatie: Relaties.Add(relatie); break;
                case Verkoopfactuur factuur: Verkoopfacturen.Add(factuur); break;
                case Factuurinstellingen instellingen: Factuurinstellingen = MetId(instellingen); break;
                case HistorischeRegel regel: HistorischeRegels.Add(MetId(regel)); break;
                case HistorischTotaal totaal: HistorischeTotalen.Add(MetId(totaal)); break;
                case Bijlage bijlage: Bijlagen.Add(bijlage); break;
                case Begroting begroting: Begrotingen.Add(MetId(begroting)); break;
                case BijlageInhoud inhoud: Bijlageinhoud[inhoud.BijlageId] = inhoud.Gegevens; break;
                default: throw new NotSupportedException(item.GetType().Name);
            }
        }

        return Task.FromResult(resultaat.Uitkomst());
    }

    private Dagboek Dagboek(Dagboek dagboek)
    {
        Dagboeken.Add(MetId(dagboek));
        return dagboek;
    }

    private T MetId<T>(T entiteit)
    {
        var id = typeof(T).GetProperty("Id")!;
        if (id.PropertyType == typeof(int) && (int)id.GetValue(entiteit)! == 0)
        {
            id.SetValue(entiteit, _id++);
        }

        return entiteit;
    }
}
