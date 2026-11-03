using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Verenigd.App.Diensten;
using Verenigd.Application.Database;
using Verenigd.Application.Modules;
using Verenigd.Application.Webapp;
using Verenigd.Domain.Leden;

namespace Verenigd.App.ViewModels.Webapp;

/// <summary>De onderdelen van Webapp in de navigatiebalk.</summary>
public enum WebappSubtab
{
    Voorstellen,
    Leiding,
    Synchroniseren,
}

/// <summary>Een regel in de wachtrij (besluit 34): oud → nieuw, met een waarschuwing als Verenigd intussen iets anders heeft.</summary>
public sealed class VoorstelRegel(Voorstelregel regel)
{
    public Voorstel Voorstel => regel.Voorstel;

    public string Lidnaam => regel.Lidnaam;

    public string Omschrijving => regel.Omschrijving;

    public string? Oud => Weergave(regel.Voorstel.Oud);

    public string? Nieuw => Weergave(regel.Voorstel.Nieuw) ?? "(leeg)";

    public string IngediendDoor => regel.Voorstel.IngediendDoor;

    public string Wanneer => DateTime.SpecifyKind(regel.Voorstel.IngediendOpUtc, DateTimeKind.Utc).ToLocalTime().ToString("d-M-yyyy HH:mm", CultureInfo.InvariantCulture);

    public string? Waarschuwing => regel.LidOnbekend
        ? "⚠ Dit lid staat niet (meer) in Verenigd."
        : regel.WijktAf ? $"⚠ In Verenigd staat nu: {Weergave(regel.HuidigeWaarde) ?? "(leeg)"}" : null;

    public bool KanGoedkeuren => !regel.LidOnbekend;

    private string? Weergave(string? waarde) =>
        waarde is null ? null
        : regel.Voorstel.Veld.EndsWith("telefoon", StringComparison.Ordinal) || regel.Voorstel.Veld.EndsWith("mobiel", StringComparison.Ordinal)
            ? Telefoonnummer.Weergave(waarde)
            : waarde;
}

public sealed class LeiderRegel(Leider leider)
{
    public Leider Leider => leider;

    public string Naam => leider.Naam;

    public string Email => leider.Email;

    public string Speltakkentekst => leider.Speltakken.Count == 0 ? "(geen speltak)" : string.Join(", ", leider.Speltakken);
}

public sealed partial class SpeltakKeuze(string naam) : ObservableObject
{
    public string Naam { get; } = naam;

    [ObservableProperty]
    public partial bool IsGekozen { get; set; }
}

/// <summary>
/// Pagina Webapp (fase 7, besluiten 33–38 en 222–233): de wachtrij met voorstellen van de leiding, het beheer van
/// leidingaccounts per speltak en de koppeling met het Supabase-project (inloggen met MFA, synchroniseren).
/// </summary>
public sealed partial class WebappViewModel : TabbladViewModel, IPaginameldingen
{
    private readonly IDatabaseSessie _sessie;
    private readonly IDialoogService _dialogen;
    private readonly WebappDienst _dienst;

    public WebappViewModel(IDatabaseSessie sessie, IDialoogService dialogen, TimeProvider tijd, IWebappServer server)
        : base(ModuleCatalogus.Geef(ModuleSoort.Webapp), administratie: null)
    {
        _sessie = sessie;
        _dialogen = dialogen;
        _dienst = new WebappDienst(sessie, server, tijd);
        VernieuwCommand = new AsyncRelayCommand(VernieuwAsync);
        BewaarProjectCommand = new AsyncRelayCommand(BewaarProjectAsync);
        InloggenCommand = new AsyncRelayCommand(InloggenAsync, () => KanInloggen);
        OntkoppelCommand = new AsyncRelayCommand(OntkoppelAsync, () => IsGekoppeld);
        SynchroniseerCommand = new AsyncRelayCommand(SynchroniseerAsync, () => IsGekoppeld);
        GoedkeurenCommand = new AsyncRelayCommand<VoorstelRegel>(KeurGoedAsync);
        AfwijzenCommand = new AsyncRelayCommand<VoorstelRegel>(WijsAfAsync);
        NodigUitCommand = new AsyncRelayCommand(NodigUitAsync, () => KanUitnodigen);
        BewaarSpeltakkenCommand = new AsyncRelayCommand(BewaarSpeltakkenAsync, () => GekozenLeider is not null);
        TrekInCommand = new AsyncRelayCommand<LeiderRegel>(TrekInAsync);
    }

    public IAsyncRelayCommand VernieuwCommand { get; }

    public IAsyncRelayCommand BewaarProjectCommand { get; }

    public IAsyncRelayCommand InloggenCommand { get; }

    public IAsyncRelayCommand OntkoppelCommand { get; }

    public IAsyncRelayCommand SynchroniseerCommand { get; }

    public IAsyncRelayCommand<VoorstelRegel> GoedkeurenCommand { get; }

    public IAsyncRelayCommand<VoorstelRegel> AfwijzenCommand { get; }

    public IAsyncRelayCommand NodigUitCommand { get; }

    public IAsyncRelayCommand BewaarSpeltakkenCommand { get; }

    public IAsyncRelayCommand<LeiderRegel> TrekInCommand { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Onderdeel))]
    public partial WebappSubtab Subtab { get; set; }

    public override int? Onderdeel
    {
        get => (int)Subtab;
        set => Subtab = value is { } i ? (WebappSubtab)i : WebappSubtab.Voorstellen;
    }

    public ObservableCollection<VoorstelRegel> Voorstellen { get; } = [];

    public ObservableCollection<LeiderRegel> Leiding { get; } = [];

    /// <summary>De speltakken uit de groepsindeling (besluiten 43 en 215).</summary>
    public ObservableCollection<SpeltakKeuze> Speltakken { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InloggenCommand))]
    public partial bool IsIngesteld { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OntkoppelCommand), nameof(SynchroniseerCommand))]
    public partial bool IsGekoppeld { get; private set; }

    [ObservableProperty]
    public partial string? Projectadres { get; set; }

    [ObservableProperty]
    public partial string? PubliekeSleutel { get; set; }

    [ObservableProperty]
    public partial string? BeheerderEmail { get; set; }

    /// <summary>Uit de PasswordBox (code-behind); wordt na het inloggen gewist en nooit bewaard.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InloggenCommand))]
    public partial string Wachtwoord { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InloggenCommand))]
    public partial string Code { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Synchronisatietekst { get; private set; } = "Nog niet gesynchroniseerd.";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BewaarSpeltakkenCommand))]
    public partial LeiderRegel? GekozenLeider { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NodigUitCommand))]
    public partial string NieuweNaam { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NodigUitCommand))]
    public partial string NieuwEmail { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? Melding { get; private set; }

    [ObservableProperty]
    public partial string? Foutmelding { get; private set; }

    public bool HeeftVoorstellen => Voorstellen.Count > 0;

    private string SchoneCode => new([.. Code.Where(char.IsDigit)]);

    private bool KanInloggen => IsIngesteld && Wachtwoord.Length > 0 && SchoneCode.Length == 6;

    private bool KanUitnodigen => NieuweNaam.Trim().Length > 0 && NieuwEmail.Contains('@', StringComparison.Ordinal) && Speltakken.Any(s => s.IsGekozen);

    partial void OnGekozenLeiderChanged(LeiderRegel? value)
    {
        foreach (var speltak in Speltakken)
        {
            speltak.IsGekozen = value?.Leider.Speltakken.Contains(speltak.Naam, StringComparer.Ordinal) ?? false;
        }
    }

    private async Task VernieuwAsync()
    {
        Foutmelding = null;
        try
        {
            // Eerst de stand (licht), zodat de pagina meteen op het juiste onderdeel staat; daarna de speltakken.
            await LeesStandAsync();
            if (!IsGekoppeld)
            {
                Subtab = WebappSubtab.Synchroniseren;
            }

            var groepen = await _sessie.Leden.HaalGroepenAsync();
            Speltakken.Clear();
            foreach (var groep in groepen.Where(g => g.Soort == GroepSoort.Speltak).OrderBy(g => g.Naam, StringComparer.Ordinal))
            {
                var keuze = new SpeltakKeuze(groep.Naam);
                keuze.PropertyChanged += (_, _) => NodigUitCommand.NotifyCanExecuteChanged();
                Speltakken.Add(keuze);
            }
        }
        catch (Exception ex) when (ex is System.Data.Common.DbException or InvalidOperationException or IOException)
        {
            // Niet stil laten verdwijnen: de pagina toont wat er misging (besluit 168).
            Foutmelding = "✕ Laden mislukt. Kies F5 om het opnieuw te proberen.";
            return;
        }

        if (IsGekoppeld)
        {
            await VoerUitAsync(async () =>
            {
                await LaadVoorstellenAsync();
                await LaadLeidingAsync();
            });
        }
    }

    private async Task LeesStandAsync()
    {
        var instellingen = await _dienst.LaadAsync();
        IsIngesteld = instellingen.IsIngesteld;
        IsGekoppeld = instellingen.IsGekoppeld;
        Projectadres ??= instellingen.Projectadres;
        PubliekeSleutel ??= instellingen.PubliekeSleutel;
        BeheerderEmail ??= instellingen.BeheerderEmail;
        Synchronisatietekst = instellingen.LaatsteSynchronisatieUtc is { } moment
            ? $"{(instellingen.LaatsteGelukt ? "✓" : "⚠")} {instellingen.LaatsteMelding} ({DateTime.SpecifyKind(moment, DateTimeKind.Utc).ToLocalTime().ToString("d-M-yyyy HH:mm", CultureInfo.InvariantCulture)})"
            : "Nog niet gesynchroniseerd.";
    }

    private async Task BewaarProjectAsync()
    {
        Foutmelding = null;
        try
        {
            await _dienst.BewaarProjectAsync(Projectadres ?? "", PubliekeSleutel ?? "", BeheerderEmail ?? "");
            Melding = "✓ Project opgeslagen. Log nu in met uw wachtwoord en de code uit de authenticator-app.";
        }
        catch (ArgumentException ex)
        {
            Foutmelding = "✕ " + ex.Message.Split(" (Parameter", StringSplitOptions.None)[0];
        }

        await LeesStandAsync();
    }

    private async Task InloggenAsync()
    {
        var gelukt = await VoerUitAsync(() => _dienst.InloggenAsync(Wachtwoord, SchoneCode));
        Wachtwoord = string.Empty;
        Code = string.Empty;
        await LeesStandAsync();
        if (gelukt)
        {
            Melding = "✓ Ingelogd als beheerder.";
            await SynchroniseerAsync();
        }
    }

    private async Task OntkoppelAsync()
    {
        await _dienst.OntkoppelAsync();
        Voorstellen.Clear();
        Leiding.Clear();
        await LeesStandAsync();
        Melding = "✓ Uitgelogd. De gegevens op de server blijven staan.";
    }

    private async Task SynchroniseerAsync()
    {
        var gelukt = await VoerUitAsync(async () =>
        {
            var resultaat = await _dienst.SynchroniseerAsync();
            Melding = $"✓ {resultaat.Leden} leden gesynchroniseerd, {resultaat.OpenVoorstellen} voorstellen open.";
            await LaadVoorstellenAsync();
            await LaadLeidingAsync();
        });
        await LeesStandAsync();
        if (!gelukt && !IsGekoppeld)
        {
            Subtab = WebappSubtab.Synchroniseren;
        }
    }

    private async Task KeurGoedAsync(VoorstelRegel? regel)
    {
        if (regel is null || !regel.KanGoedkeuren)
        {
            return;
        }

        if (regel.Waarschuwing is { } waarschuwing
            && !_dialogen.Bevestig("Voorstel goedkeuren", $"{waarschuwing}. Toch {regel.Omschrijving.ToLowerInvariant()} van {regel.Lidnaam} wijzigen in {regel.Nieuw}?"))
        {
            return;
        }

        if (await VoerUitAsync(() => _dienst.KeurGoedAsync(regel.Voorstel)))
        {
            Voorstellen.Remove(regel);
            OnPropertyChanged(nameof(HeeftVoorstellen));
            Melding = $"✓ Goedgekeurd: {regel.Omschrijving} van {regel.Lidnaam}. Wijzigt het een SOL3-veld, voer het dan ook in SOL in (besluit 34).";
        }
    }

    private async Task WijsAfAsync(VoorstelRegel? regel)
    {
        if (regel is null)
        {
            return;
        }

        if (await VoerUitAsync(() => _dienst.WijsAfAsync(regel.Voorstel)))
        {
            Voorstellen.Remove(regel);
            OnPropertyChanged(nameof(HeeftVoorstellen));
            Melding = $"✓ Afgewezen: {regel.Omschrijving} van {regel.Lidnaam}.";
        }
    }

    private async Task NodigUitAsync()
    {
        var speltakken = Speltakken.Where(s => s.IsGekozen).Select(s => s.Naam).ToList();
        if (await VoerUitAsync(() => _dienst.NodigUitAsync(NieuwEmail, NieuweNaam, speltakken)))
        {
            Melding = $"✓ Uitnodiging verstuurd aan {NieuweNaam.Trim()}.";
            NieuweNaam = string.Empty;
            NieuwEmail = string.Empty;
            await VoerUitAsync(LaadLeidingAsync);
        }
    }

    private async Task BewaarSpeltakkenAsync()
    {
        if (GekozenLeider is not { } leider)
        {
            return;
        }

        var speltakken = Speltakken.Where(s => s.IsGekozen).Select(s => s.Naam).ToList();
        if (await VoerUitAsync(() => _dienst.ZetSpeltakkenAsync(leider.Leider.Id, speltakken)))
        {
            Melding = $"✓ Speltakken van {leider.Naam} opgeslagen.";
            await VoerUitAsync(LaadLeidingAsync);
        }
    }

    private async Task TrekInAsync(LeiderRegel? regel)
    {
        if (regel is null || !_dialogen.Bevestig("Toegang intrekken", $"Het account van {regel.Naam} verwijderen? {regel.Naam} kan daarna niet meer inloggen op de webapp."))
        {
            return;
        }

        if (await VoerUitAsync(() => _dienst.TrekInAsync(regel.Leider.Id)))
        {
            Melding = $"✓ Toegang van {regel.Naam} ingetrokken.";
            await VoerUitAsync(LaadLeidingAsync);
        }
    }

    private async Task LaadVoorstellenAsync()
    {
        var regels = await _dienst.HaalVoorstellenAsync();
        Voorstellen.Clear();
        foreach (var regel in regels)
        {
            Voorstellen.Add(new VoorstelRegel(regel));
        }

        OnPropertyChanged(nameof(HeeftVoorstellen));
    }

    private async Task LaadLeidingAsync()
    {
        var leiding = await _dienst.HaalLeidingAsync();
        GekozenLeider = null;
        Leiding.Clear();
        foreach (var leider in leiding)
        {
            Leiding.Add(new LeiderRegel(leider));
        }
    }

    /// <summary>Vaste statusteksten (besluit 168): ⚠ als de server onbereikbaar is, ✕ bij een andere fout.</summary>
    private async Task<bool> VoerUitAsync(Func<Task> actie)
    {
        Foutmelding = null;
        try
        {
            await actie();
            return true;
        }
        catch (WebappOnbereikbaarException ex)
        {
            Foutmelding = "⚠ " + ex.Message;
        }
        catch (WebappToegangException ex)
        {
            Foutmelding = "✕ " + ex.Message;
            await LeesStandAsync();
        }
        catch (WebappException ex)
        {
            Foutmelding = "✕ " + ex.Message;
        }
        catch (KeyNotFoundException ex)
        {
            Foutmelding = "✕ " + ex.Message;
        }

        Melding = null;
        return false;
    }
}
