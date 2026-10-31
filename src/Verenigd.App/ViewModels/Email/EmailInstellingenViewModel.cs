using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Verenigd.App.Diensten;
using Verenigd.Application.Email;
using Verenigd.Domain.Administraties;
using Verenigd.Domain.Communicatie;

namespace Verenigd.App.ViewModels.Email;

/// <summary>
/// Instellingen › E-mail (besluiten 192 en 193): de afzender, het clientbestand van het eigen Google Cloud-project en de
/// koppeling (alleen toestemming om te versturen). Het adres en de sleutels staan versleuteld in de database, niet in Git.
/// De afzender gaat mee met Opslaan; inlezen, koppelen en ontkoppelen worden direct bewaard.
/// </summary>
public sealed partial class EmailInstellingenViewModel : ObservableObject
{
    private static readonly TimeSpan Wachttijd = TimeSpan.FromMinutes(5);
    private readonly EmailDienst _dienst;
    private readonly IGoogleKoppeling _koppeling;
    private readonly IDialoogService _dialogen;
    private Emailinstellingen _instellingen;

    public EmailInstellingenViewModel(
        EmailDienst dienst, IGoogleKoppeling koppeling, IDialoogService dialogen, AdministratieSoort administratie = AdministratieSoort.Vereniging,
        string? naam = null)
    {
        _dienst = dienst;
        _koppeling = koppeling;
        _dialogen = dialogen;
        Administratie = administratie;
        Titel = $"E-mail ({naam ?? administratie.ToString()})";
        _instellingen = new Emailinstellingen(administratie);
        LaadCommand = new AsyncRelayCommand(LaadAsync);
    }

    public IAsyncRelayCommand LaadCommand { get; }

    /// <summary>Besluit 211: elke administratie heeft een eigen Gmail-account.</summary>
    public AdministratieSoort Administratie { get; }

    public string Titel { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd))]
    public partial string Afzendernaam { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd), nameof(Fout))]
    public partial string Afzenderadres { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Fout))]
    public partial string? Actiefout { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(KoppelCommand), nameof(LeesClientbestandCommand), nameof(OntkoppelCommand))]
    public partial bool IsBezig { get; private set; }

    public bool IsGekoppeld => _instellingen.IsGekoppeld;

    public string Clienttekst => _instellingen.HeeftClient ? $"Ingelezen ({_instellingen.ClientId})" : "Nog niet ingelezen";

    public string Status => (_instellingen.IsGekoppeld, _instellingen.HeeftClient) switch
    {
        (true, _) => $"✓ Gekoppeld op {_instellingen.GekoppeldOpUtc?.ToLocalTime().ToString("d-M-yyyy HH:mm", CultureInfo.InvariantCulture)}",
        (false, true) => "Niet gekoppeld. Kies Koppelen met Google en geef in de browser toestemming om e-mail te versturen.",
        _ => "Niet gekoppeld. Lees eerst het clientbestand van uw Google Cloud-project in (zie de handleiding).",
    };

    public bool IsGewijzigd =>
        !string.Equals(Afzendernaam.Trim(), _instellingen.Afzendernaam ?? string.Empty, StringComparison.Ordinal)
        || !string.Equals(Afzenderadres.Trim(), _instellingen.Afzenderadres ?? string.Empty, StringComparison.Ordinal);

    public string? Fout =>
        IsGewijzigd && !Emailadres.IsGeldig(Afzenderadres) ? "Vul bij de afzender een geldig e-mailadres in (het Gmail-adres waarmee u koppelt)."
        : Actiefout;

    public async Task LaadAsync()
    {
        _instellingen = (await _dienst.LaadAsync()).Instellingen(Administratie);
        Afzendernaam = _instellingen.Afzendernaam ?? string.Empty;
        Afzenderadres = _instellingen.Afzenderadres ?? string.Empty;
        Ververs();
    }

    public async Task BewaarAsync()
    {
        _instellingen.ZetAfzender(Afzendernaam, Afzenderadres);
        await _dienst.BewaarInstellingenAsync(_instellingen);
        Ververs();
    }

    [RelayCommand(CanExecute = nameof(KanWerken))]
    private async Task LeesClientbestandAsync()
    {
        if (_dialogen.KiesClientbestand() is not { } pad)
        {
            return;
        }

        Actiefout = null;
        string inhoud;
        try
        {
            inhoud = await File.ReadAllTextAsync(pad);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Actiefout = $"Het bestand kan niet worden gelezen: {ex.Message}";
            return;
        }

        if (!GoogleClientbestand.TryLees(inhoud, out var id, out var geheim))
        {
            Actiefout = "Dit is geen clientbestand van een OAuth-client van het type Desktop-app. Download het opnieuw in Google Cloud (zie de handleiding).";
            return;
        }

        _instellingen.ZetClient(id, geheim);
        await _dienst.BewaarInstellingenAsync(_instellingen);
        Ververs();
    }

    private bool KanWerken() => !IsBezig;

    private bool KanKoppelen() => !IsBezig && _instellingen.HeeftClient;

    /// <summary>Opent de browser voor de toestemming; wacht hooguit vijf minuten op het antwoord.</summary>
    [RelayCommand(CanExecute = nameof(KanKoppelen))]
    private async Task KoppelAsync()
    {
        IsBezig = true;
        Actiefout = null;
        using var wachten = new CancellationTokenSource(Wachttijd);
        try
        {
            await _dienst.KoppelAsync(Administratie, _koppeling, wachten.Token);
        }
        catch (OperationCanceledException)
        {
            Actiefout = "Koppelen afgebroken: Google gaf binnen vijf minuten geen antwoord. Probeer het opnieuw.";
        }
        catch (Exception ex) when (ex is EmailVerzendException or InvalidOperationException)
        {
            Actiefout = ex.Message;
        }
        finally
        {
            IsBezig = false;
        }

        var afzender = (Afzendernaam, Afzenderadres);
        await LaadAsync();
        (Afzendernaam, Afzenderadres) = afzender; // een nog niet opgeslagen afzender blijft staan
    }

    private bool KanOntkoppelen() => !IsBezig && _instellingen.IsGekoppeld;

    [RelayCommand(CanExecute = nameof(KanOntkoppelen))]
    private async Task OntkoppelAsync()
    {
        if (!_dialogen.Bevestig("Ontkoppelen", "De koppeling met Gmail verwijderen? Daarna kan Verenigd geen e-mail meer versturen tot u opnieuw koppelt."))
        {
            return;
        }

        _instellingen.Ontkoppel();
        await _dienst.BewaarInstellingenAsync(_instellingen);
        Ververs();
    }

    private void Ververs()
    {
        OnPropertyChanged(nameof(IsGekoppeld));
        OnPropertyChanged(nameof(Clienttekst));
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(IsGewijzigd));
        OnPropertyChanged(nameof(Fout));
        KoppelCommand.NotifyCanExecuteChanged();
        OntkoppelCommand.NotifyCanExecuteChanged();
    }
}
