using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Verenigd.App.Diensten;
using Verenigd.Application.Facturen;
using Verenigd.Domain.Administraties;
using Verenigd.Domain.Financien.Facturen;

namespace Verenigd.App.ViewModels.Boekhouding;

/// <summary>
/// Instellingen › Facturen: afzender, betaaltermijn, voettekst en logo van één administratie op de verkoopfactuur
/// (besluiten 112 en 209). Het logo staat in de versleutelde database.
/// </summary>
public sealed partial class FactuurInstellingenViewModel : ObservableObject
{
    private readonly FactuurDienst _dienst;
    private readonly IDialoogService _dialogen;
    private string _origineel = string.Empty;
    private byte[]? _huidigLogo;

    /// <summary><c>null</c>: ongewijzigd; leeg: wissen; anders het nieuwe logo.</summary>
    private byte[]? _nieuwLogo;

    public FactuurInstellingenViewModel(
        FactuurDienst dienst, IDialoogService dialogen, AdministratieSoort administratie = AdministratieSoort.Vereniging, string? naam = null)
    {
        _dienst = dienst;
        _dialogen = dialogen;
        Administratie = administratie;
        Titel = $"Facturen ({naam ?? administratie.ToString()})";
        LaadCommand = new AsyncRelayCommand(LaadAsync);
        _origineel = Momentopname();
    }

    public IAsyncRelayCommand LaadCommand { get; }

    /// <summary>Besluit 209: elke administratie heeft eigen gegevens op de factuur.</summary>
    public AdministratieSoort Administratie { get; }

    public string Titel { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd))]
    public partial string Naam { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd))]
    public partial string Straat { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd))]
    public partial string Postcode { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd))]
    public partial string Plaats { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd))]
    public partial string Kvk { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd), nameof(Fout))]
    public partial string Iban { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd))]
    public partial string Email { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd))]
    public partial string Telefoon { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd), nameof(Fout))]
    public partial string Termijn { get; set; } = "14";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd))]
    public partial string Voettekst { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Fout))]
    public partial string? Logofout { get; private set; }

    public string Logotekst
    {
        get
        {
            var logo = _nieuwLogo ?? _huidigLogo;
            return logo is not { Length: > 0 } ? "Geen logo"
                : string.Create(CultureInfo.CurrentCulture, $"{(_nieuwLogo is null ? "Logo ingesteld" : "Nieuw logo")} ({Math.Max(1, logo.Length / 1024)} kB)");
        }
    }

    public string? Fout =>
        Logofout
        ?? (Iban.Trim().Length > 0 && (!Domain.Financien.Iban.TryMaak(Iban, out var iban) || !iban.ControlegetalGeldig)
            ? "Het IBAN is ongeldig (controleer het controlegetal)."
            : Termijnwaarde is null ? "Vul een betaaltermijn van 0 tot en met 365 dagen in."
            : null);

    public bool IsGewijzigd => _nieuwLogo is not null || Momentopname() != _origineel;

    private int? Termijnwaarde => int.TryParse(Termijn, NumberStyles.None, CultureInfo.InvariantCulture, out var t) && t <= 365 ? t : null;

    public async Task LaadAsync()
    {
        var instellingen = (await _dienst.LaadAsync(Administratie)).Factuurinstellingen ?? new Factuurinstellingen(Administratie);
        Naam = instellingen.Naam ?? string.Empty;
        Straat = instellingen.Straat ?? string.Empty;
        Postcode = instellingen.Postcode ?? string.Empty;
        Plaats = instellingen.Plaats ?? string.Empty;
        Kvk = instellingen.Kvk ?? string.Empty;
        Iban = instellingen.Iban is { } iban ? Domain.Financien.Iban.Maak(iban).ToString() : string.Empty;
        Email = instellingen.Email ?? string.Empty;
        Telefoon = instellingen.Telefoon ?? string.Empty;
        Termijn = instellingen.Termijn.ToString(CultureInfo.InvariantCulture);
        Voettekst = instellingen.Voettekst ?? string.Empty;
        _huidigLogo = instellingen.Logo;
        _nieuwLogo = null;
        Logofout = null;
        _origineel = Momentopname();
        LogoGewijzigd();
    }

    /// <exception cref="ArgumentException">Ongeldige invoer.</exception>
    public async Task BewaarAsync()
    {
        await _dienst.BewaarInstellingenAsync(Administratie,
            new FactuurinstellingenInvoer(Naam, Straat, Postcode, Plaats, Kvk, Iban, Email, Telefoon, Termijnwaarde ?? -1, Voettekst), _nieuwLogo);
        if (_nieuwLogo is not null)
        {
            _huidigLogo = _nieuwLogo.Length == 0 ? null : _nieuwLogo;
            _nieuwLogo = null;
        }

        _origineel = Momentopname();
        LogoGewijzigd();
    }

    [RelayCommand]
    private void KiesLogo()
    {
        if (_dialogen.KiesAfbeelding() is not { } pad)
        {
            return;
        }

        try
        {
            var logo = File.ReadAllBytes(pad);
            new Factuurinstellingen(Administratie).ZetLogo(logo); // dezelfde controle als bij opslaan
            _nieuwLogo = logo;
            Logofout = null;
        }
        catch (ArgumentException ex)
        {
            Logofout = Meldingen.Zonder(ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Logofout = "Het bestand kan niet worden gelezen.";
        }

        LogoGewijzigd();
    }

    [RelayCommand]
    private void WisLogo()
    {
        _nieuwLogo = _huidigLogo is null ? null : [];
        Logofout = null;
        LogoGewijzigd();
    }

    private void LogoGewijzigd()
    {
        OnPropertyChanged(nameof(Logotekst));
        OnPropertyChanged(nameof(IsGewijzigd));
        OnPropertyChanged(nameof(Fout));
    }

    private string Momentopname() =>
        string.Join('|', Naam.Trim(), Straat.Trim(), Postcode.Trim(), Plaats.Trim(), Kvk.Trim(), Iban.Trim(), Email.Trim(), Telefoon.Trim(), Termijn.Trim(), Voettekst.Trim());
}
