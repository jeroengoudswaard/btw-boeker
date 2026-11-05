using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Verenigd.App.ViewModels.Boekhouding;
using Verenigd.App.ViewModels.Contributie;
using Verenigd.Application.Inrichting;
using Verenigd.Application.Modules;
using Verenigd.Domain.Administraties;

namespace Verenigd.App.ViewModels;

/// <summary>Soort melding in de statusregel (besluit 168).</summary>
public enum Statussoort
{
    Info,
    Gelukt,
    Waarschuwing,
    Fout,
}

/// <summary>
/// Navigatiebalk, administratiewisselaar, statusregel, menubalk en startkaarten (v0.8.0, besluiten 168 en 177–183).
/// </summary>
public sealed partial class ShellViewModel
{
    private readonly HashSet<ModuleSoort> _meerOpen = [];
    private bool _synchroniseren;
    private bool _menubalkTijdelijk;
    private bool _webappGekoppeld;

    /// <summary>De regels van de navigatiebalk; onderdelen alleen van de module die open is.</summary>
    public ObservableCollection<NavigatieItem> Navigatie { get; } = [];

    [ObservableProperty]
    public partial NavigatieItem? GeselecteerdeNavigatie { get; set; }

    /// <summary>Besluit 177: één wisselaar bovenaan voor de hele app.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActieveAdministratie), nameof(ActieveAdministratieNaam), nameof(Venstertitel))]
    public partial AdministratieViewModel? GekozenAdministratie { get; set; }

    /// <summary>Besluit 179: in Expert altijd; in Eenvoudig alleen tijdelijk na Alt.</summary>
    public bool IsMenubalkZichtbaar => Weergave == Application.Instellingen.Weergavemodus.Expert || _menubalkTijdelijk;

    public void ToonMenubalkTijdelijk()
    {
        _menubalkTijdelijk = true;
        OnPropertyChanged(nameof(IsMenubalkZichtbaar));
    }

    public void VerbergTijdelijkeMenubalk()
    {
        _menubalkTijdelijk = false;
        OnPropertyChanged(nameof(IsMenubalkZichtbaar));
    }

    private void ZetStatus(Statussoort soort, string tekst)
    {
        Statussoort = soort;
        Statusmelding = tekst;
    }

    partial void OnDatabaseNaamChanged(string? value) => BouwNavigatie();

    partial void OnGeselecteerdeNavigatieChanged(NavigatieItem? value)
    {
        if (!_synchroniseren && value is not null)
        {
            Navigeer(value);
        }
    }

    /// <summary>Besluit 182: dezelfde pagina voor de andere administratie; bestaat die daar niet, dan naar Start.</summary>
    partial void OnGekozenAdministratieChanged(AdministratieViewModel? value)
    {
        if (_synchroniseren || value is null)
        {
            return;
        }

        using var meting = Meetpunt.Start(_logger, "Wisselen van administratie");

        BouwModuleMenu();
        if (GeselecteerdTabblad is { Administratie: { } huidig } pagina && huidig.Soort != value.Soort)
        {
            var verzoek = new ModuleVerzoek(pagina.Module.Soort, value.Soort);
            if (KanModuleOpenen(verzoek))
            {
                var onderdeel = pagina.Onderdeel;
                OpenModule(verzoek);
                if (GeselecteerdTabblad is { } nieuw && onderdeel is { } o && !(nieuw is BoekhoudingViewModel && IsAlleenStichting(o)))
                {
                    nieuw.Onderdeel = o;
                }
            }
            else
            {
                OpenModule(new ModuleVerzoek(ModuleSoort.Overzicht, null));
            }
        }

        BouwNavigatie();
        _ = VernieuwStartkaartenAsync(); // kerncijfers en tellers horen bij de gekozen administratie
    }

    /// <summary>Zet de wisselaar zonder van pagina te wisselen (bij openen van een database of van een pagina).</summary>
    private void KiesAdministratieZonderWisselen(AdministratieSoort? soort)
    {
        _synchroniseren = true;
        try
        {
            GekozenAdministratie = soort is null ? null : Administraties.FirstOrDefault(a => a.Soort == soort);
        }
        finally
        {
            _synchroniseren = false;
        }

        BouwModuleMenu();
    }

    private void Navigeer(NavigatieItem item)
    {
        if (item.IsMeer)
        {
            // Besluit 269: Meer… klapt alleen in of uit; de pagina blijft staan.
            if (!_meerOpen.Remove(item.Module))
            {
                _meerOpen.Add(item.Module);
            }

            BouwNavigatie();
            return;
        }

        using var meting = Meetpunt.Start(_logger, "Navigeren naar " + item.Sleutel);
        var administratie = GekozenAdministratie?.Soort;
        var definitie = ModuleCatalogus.Geef(item.Module);
        var verzoek = new ModuleVerzoek(item.Module, definitie.PerAdministratie ? definitie.AlleenVoor ?? administratie : null);
        if (!KanModuleOpenen(verzoek))
        {
            SynchroniseerNavigatie();
            return;
        }

        OpenModule(verzoek);
        if (item.Onderdeel is { } onderdeel && GeselecteerdTabblad is { } pagina)
        {
            pagina.Onderdeel = onderdeel;
        }

        BouwNavigatie();
    }

    /// <summary>Bouwt de navigatiebalk opnieuw op: andere administratie, weergave of geopende module.</summary>
    private void BouwNavigatie()
    {
        var items = new List<NavigatieItem> { Module(ModuleSoort.Overzicht, "Start", "Start") };
        var tellers = IsDatabaseOpen ? Navigatietellers.Bepaal(_kaarten, GekozenAdministratie?.Soort) : new Dictionary<string, int>();
        if (IsDatabaseOpen)
        {
            var open = GeselecteerdTabblad?.Module.Soort;
            var stichting = GekozenAdministratie?.Soort == AdministratieSoort.Stichting;
            items.Add(Module(ModuleSoort.Leden, "Leden", "Leden"));
            if (!stichting)
            {
                items.Add(Module(ModuleSoort.Contributie, "Contributie", "Contributie", heeftOnderdelen: true, uitgeklapt: open == ModuleSoort.Contributie));
                if (open == ModuleSoort.Contributie)
                {
                    items.AddRange(Contributieonderdelen(tellers));
                }
            }

            items.Add(Module(ModuleSoort.Boekhouding, "Boekhouding", _boekhouding.Woorden["Boekhouding"], heeftOnderdelen: true,
                uitgeklapt: open == ModuleSoort.Boekhouding));
            if (open == ModuleSoort.Boekhouding)
            {
                items.AddRange(Boekhoudingonderdelen());
            }

            items.Add(Module(ModuleSoort.Begroting, "Begroting", Zonder_(_boekhouding.Woorden[stichting ? "Tab.Meerjarenplan" : "Tab.Begroting"])));
            items.Add(Module(ModuleSoort.Email, "Email", "E-mail", heeftOnderdelen: true, uitgeklapt: open == ModuleSoort.Email));
            if (open == ModuleSoort.Email)
            {
                items.AddRange(Emailonderdelen());
            }

            items.Add(Module(ModuleSoort.Webapp, "Webapp", "Webapp", heeftOnderdelen: true, uitgeklapt: open == ModuleSoort.Webapp));
            if (open == ModuleSoort.Webapp)
            {
                items.AddRange(Webapponderdelen());
            }

            if (open != ModuleSoort.Boekhouding && open != ModuleSoort.Contributie && open != ModuleSoort.Webapp)
            {
                _meerOpen.Clear(); // een andere module: Meer… begint weer ingeklapt
            }

            items.Add(Module(ModuleSoort.Instellingen, "Instellingen", "Instellingen"));
        }

        WerkNavigatieBij(items);
        SynchroniseerNavigatie();
        PasTellersToe();
    }

    /// <summary>
    /// Werkt de navigatiebalk bij in plaats van hem opnieuw op te bouwen: bestaande regels blijven dezelfde objecten, zodat
    /// de gekozen regel, de toetsenbordfocus en UI Automation (schermlezers) hun element houden. Een regel met een andere
    /// titel (andere weergave) wordt vervangen.
    /// </summary>
    private void WerkNavigatieBij(List<NavigatieItem> items)
    {
        _synchroniseren = true;
        try
        {
            for (var i = Navigatie.Count - 1; i >= 0; i--)
            {
                var regel = Navigatie[i];
                if (!items.Exists(n => n.Sleutel == regel.Sleutel && n.Titel == regel.Titel))
                {
                    Navigatie.RemoveAt(i);
                }
            }

            for (var i = 0; i < items.Count; i++)
            {
                var gewenst = items[i];
                var bestaand = Navigatie.FirstOrDefault(n => n.Sleutel == gewenst.Sleutel);
                if (bestaand is null)
                {
                    Navigatie.Insert(i, gewenst);
                    continue;
                }

                bestaand.IsUitgeklapt = gewenst.IsUitgeklapt;
                var index = Navigatie.IndexOf(bestaand);
                if (index != i)
                {
                    Navigatie.Move(index, i);
                }
            }
        }
        finally
        {
            _synchroniseren = false;
        }
    }

    /// <summary>Markeert de regel van de pagina die nu open is, zonder opnieuw te navigeren.</summary>
    private void SynchroniseerNavigatie()
    {
        var pagina = GeselecteerdTabblad;
        NavigatieItem? gekozen = pagina switch
        {
            null or OverzichtViewModel => Navigatie.FirstOrDefault(),
            BoekhoudingViewModel { Subtab: BoekhoudingSubtab.Begroting or BoekhoudingSubtab.Meerjarenplan } =>
                Navigatie.FirstOrDefault(n => n.Module == ModuleSoort.Begroting),
            _ => Navigatie.FirstOrDefault(n => n.Module == pagina.Module.Soort && !n.IsMeer && n.Onderdeel == pagina.Onderdeel)
                ?? Navigatie.FirstOrDefault(n => n.Module == pagina.Module.Soort && !n.IsSubitem),
        };

        if (!ReferenceEquals(gekozen, GeselecteerdeNavigatie))
        {
            _synchroniseren = true;
            try
            {
                GeselecteerdeNavigatie = gekozen;
            }
            finally
            {
                _synchroniseren = false;
            }
        }
    }

    private List<NavigatieItem> Boekhoudingonderdelen()
    {
        var woorden = _boekhouding.Woorden;
        return Onderdelen(ModuleSoort.Boekhouding, "Boekhouding",
        [
            ((int)BoekhoudingSubtab.Journaal, woorden["Tab.Journaal"]),
            ((int)BoekhoudingSubtab.Bank, woorden["Tab.Bank"]),
            ((int)BoekhoudingSubtab.Postvak, woorden["Tab.Postvak"]),
            ((int)BoekhoudingSubtab.Facturen, "Facturen"),
            ((int)BoekhoudingSubtab.Rapporten, woorden["Tab.Rapporten"]),
        ],
        [
            ((int)BoekhoudingSubtab.Boekingsregels, woorden["Tab.Boekingsregels"]),
            ((int)BoekhoudingSubtab.Relaties, "Relaties"),
            ((int)BoekhoudingSubtab.Rekeningschema, woorden["Tab.Rekeningschema"]),
            ((int)BoekhoudingSubtab.Grootboekkaart, woorden["Tab.Grootboekkaart"]),
            ((int)BoekhoudingSubtab.Boekjaren, woorden["Tab.Boekjaren"]),
        ], BoekhoudingSubtabNaam);
    }

    /// <summary>Openstaand alleen als er iets openstaat of als het open is (besluit 269).</summary>
    private List<NavigatieItem> Contributieonderdelen(IReadOnlyDictionary<string, int> tellers)
    {
        var openstaandZichtbaar = tellers.ContainsKey("Contributie.Openstaand")
            || GeselecteerdTabblad is ContributieViewModel { Subtab: ContributieSubtab.Openstaand };
        List<(int, string)> dagelijks = [((int)ContributieSubtab.Leden, "Leden & categorie"), ((int)ContributieSubtab.Runs, "Incassoruns")];
        if (openstaandZichtbaar)
        {
            dagelijks.Add(((int)ContributieSubtab.Openstaand, "Openstaand"));
        }

        return Onderdelen(ModuleSoort.Contributie, "Contributie", dagelijks,
            [((int)ContributieSubtab.Betalers, "Betalers & machtigingen"), ((int)ContributieSubtab.Tarieven, "Tarieven")],
            o => ((ContributieSubtab)o).ToString());
    }

    private static IEnumerable<NavigatieItem> Emailonderdelen()
    {
        (Email.EmailSubtab Subtab, string Titel)[] onderdelen =
        [
            (Email.EmailSubtab.Opstellen, "Opstellen"),
            (Email.EmailSubtab.Verzonden, "Verzonden"),
            (Email.EmailSubtab.Sjablonen, "Sjablonen"),
        ];
        return onderdelen.Select(o => new NavigatieItem($"Email.{o.Subtab}", o.Titel, string.Empty, ModuleSoort.Email, (int)o.Subtab));
    }

    /// <summary>Zonder koppeling alleen <i>Koppelen</i>; daarna Voorstellen, met Leiding en Synchroniseren onder Meer… (besluit 269).</summary>
    private List<NavigatieItem> Webapponderdelen()
    {
        static string Naam(int o) => ((Webapp.WebappSubtab)o).ToString();
        if (!_webappGekoppeld)
        {
            return [new NavigatieItem($"Webapp.{Naam((int)Webapp.WebappSubtab.Synchroniseren)}", "Koppelen", string.Empty, ModuleSoort.Webapp, (int)Webapp.WebappSubtab.Synchroniseren)];
        }

        return Onderdelen(ModuleSoort.Webapp, "Webapp", [((int)Webapp.WebappSubtab.Voorstellen, "Voorstellen")],
            [((int)Webapp.WebappSubtab.Leiding, "Leiding"), ((int)Webapp.WebappSubtab.Synchroniseren, "Synchroniseren")], Naam);
    }

    /// <summary>
    /// De dagelijkse onderdelen, dan <i>Meer…</i> en, als die open is of de pagina daar staat, de onderdelen voor inrichting
    /// (besluit 269).
    /// </summary>
    private List<NavigatieItem> Onderdelen(
        ModuleSoort module, string sleutel, IEnumerable<(int Onderdeel, string Titel)> dagelijks, IEnumerable<(int Onderdeel, string Titel)> meer,
        Func<int, string> naam)
    {
        var meerLijst = meer.ToList();
        if (GeselecteerdTabblad is { } pagina && pagina.Module.Soort == module && pagina.Onderdeel is { } huidig && meerLijst.Exists(m => m.Onderdeel == huidig))
        {
            _meerOpen.Add(module);
        }

        var open = _meerOpen.Contains(module);
        var items = dagelijks.Select(o => new NavigatieItem($"{sleutel}.{naam(o.Onderdeel)}", Zonder_(o.Titel), string.Empty, module, o.Onderdeel)).ToList();
        items.Add(new NavigatieItem($"{sleutel}.Meer", "Meer…", string.Empty, module, null) { IsMeer = true, IsUitgeklapt = open, HeeftOnderdelen = true });
        if (open)
        {
            items.AddRange(meerLijst.Select(o => new NavigatieItem($"{sleutel}.{naam(o.Onderdeel)}", Zonder_(o.Titel), string.Empty, module, o.Onderdeel) { IsDiep = true }));
        }

        return items;
    }

    private static string BoekhoudingSubtabNaam(int onderdeel) => ((BoekhoudingSubtab)onderdeel).ToString();

    private static NavigatieItem Module(ModuleSoort soort, string sleutel, string titel, bool heeftOnderdelen = false, bool uitgeklapt = false) =>
        new(sleutel, titel, ModuleCatalogus.Geef(soort).Pictogram, soort, null) { HeeftOnderdelen = heeftOnderdelen, IsUitgeklapt = uitgeklapt };

    private static bool IsAlleenStichting(int onderdeel) => onderdeel == (int)BoekhoudingSubtab.Meerjarenplan;

    /// <summary>Titels uit de woordenlijst bevatten een toegangstoets ("B_ank"); de navigatiebalk toont ze zonder.</summary>
    private static string Zonder_(string titel) => titel.Replace("_", string.Empty, StringComparison.Ordinal);

    // ---- Startkaarten (besluit 183) ----------------------------------------------------------

    private IReadOnlyList<Startkaart> _kaarten = [];

    private async Task VernieuwStartkaartenAsync()
    {
        using var meting = Meetpunt.Start(_logger, "Startpagina (kaarten, kerncijfers, tellers)");
        var vandaag = DateOnly.FromDateTime(_tijd.GetLocalNow().DateTime);
        var gegevens = _sessie is null ? null : await Startkaarten.LaadAsync(_sessie, vandaag);
        _kaarten = gegevens?.Kaarten ?? [];
        _overzicht.Kaarten.Vervang(_kaarten);

        _overzicht.Kerncijfers.Clear();
        if (gegevens is not null && GekozenAdministratie is { } administratie)
        {
            // Besluit 234: dezelfde geladen boekhouding als de kaarten, niet opnieuw laden.
            foreach (var regel in OverzichtViewModel.Kerncijferregels(Kerncijfers.Bepaal(gegevens.Boekhouding(administratie.Soort), vandaag), vandaag.Year))
            {
                _overzicht.Kerncijfers.Add(regel);
            }
        }

        BouwNavigatie(); // tellers, en Openstaand verschijnt of verdwijnt (besluit 269)
    }

    /// <summary>Besluit 220: de aantallen bij de regels van de navigatiebalk.</summary>
    private void PasTellersToe()
    {
        var tellers = IsDatabaseOpen ? Navigatietellers.Bepaal(_kaarten, GekozenAdministratie?.Soort) : new Dictionary<string, int>();
        foreach (var regel in Navigatie)
        {
            regel.Teller = tellers.TryGetValue(regel.Sleutel, out var aantal) ? aantal : null;
        }
    }

    /// <summary>De knop op een startkaart: naar de pagina waar het werk ligt.</summary>
    [RelayCommand]
    private async Task KaartOpenenAsync(Startkaart? kaart)
    {
        if (kaart is null)
        {
            return;
        }

        if (kaart.Administratie is { } soort && GekozenAdministratie?.Soort != soort)
        {
            KiesAdministratieZonderWisselen(soort);
        }

        switch (kaart.Soort)
        {
            case Startkaartsoort.Inrichting:
                await StartwizardCommand.ExecuteAsync(null);
                await VernieuwStartkaartenAsync();
                return;
            case Startkaartsoort.Incassorun:
                await NieuweIncassorunCommand.ExecuteAsync(null);
                return;
            case Startkaartsoort.Storneringen:
                Navigeer(new NavigatieItem("Contributie.Openstaand", string.Empty, string.Empty, ModuleSoort.Contributie, (int)ContributieSubtab.Openstaand));
                return;
        }

        var onderdeel = kaart.Soort switch
        {
            Startkaartsoort.Bank => BoekhoudingSubtab.Bank,
            Startkaartsoort.Postvak => BoekhoudingSubtab.Postvak,
            Startkaartsoort.VervallenFacturen => BoekhoudingSubtab.Facturen,
            _ => BoekhoudingSubtab.Journaal,
        };
        Navigeer(new NavigatieItem($"Boekhouding.{onderdeel}", string.Empty, string.Empty, ModuleSoort.Boekhouding, (int)onderdeel));
        if (kaart.Soort == Startkaartsoort.ZonderBon && GeselecteerdTabblad is BoekhoudingViewModel boekhouding)
        {
            boekhouding.AlleenZonderBon = true;
        }
    }
}
