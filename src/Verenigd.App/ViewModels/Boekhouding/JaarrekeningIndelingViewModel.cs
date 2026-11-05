using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Verenigd.Application.Boekhouding;
using Verenigd.Domain.Financien.Boekhouding;

namespace Verenigd.App.ViewModels.Boekhouding;

public sealed record Keuze<T>(T Waarde, string Naam);

/// <summary>Een rubriek in de lijst van een regel; grijs als hij al bij een andere regel van hetzelfde deel staat.</summary>
public sealed partial class RubriekKeuze(Rubriek rubriek) : ObservableObject
{
    public Rubriek Rubriek { get; } = rubriek;

    public string Naam => Rubriek.Naam;

    [ObservableProperty]
    public partial bool IsGekozen { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KanKiezen))]
    public partial bool IsElders { get; set; }

    public bool KanKiezen => !IsElders || IsGekozen;
}

/// <summary>Eén regel in de editor.</summary>
public sealed partial class IndelingsregelRij : ObservableObject
{
    public IndelingsregelRij(JaarrekeningAfdeling afdeling, IndelingsregelSoort soort, string tekst, IEnumerable<Rubriek> beschikbaar, IEnumerable<Rubriek> gekozen)
    {
        Afdeling = afdeling;
        Soort = soort;
        Tekst = tekst;
        var lijst = gekozen.ToList();
        Rubrieken = [.. beschikbaar.Select(r => new RubriekKeuze(r) { IsGekozen = lijst.Exists(g => g.Id == r.Id) })];
        foreach (var keuze in Rubrieken)
        {
            keuze.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(RubriekKeuze.IsGekozen))
                {
                    OnPropertyChanged(nameof(Omschrijving));
                    RubriekGewijzigd?.Invoke(this, EventArgs.Empty);
                }
            };
        }
    }

    public event EventHandler? RubriekGewijzigd;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Omschrijving))]
    public partial JaarrekeningAfdeling Afdeling { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Omschrijving), nameof(IsRegel))]
    public partial IndelingsregelSoort Soort { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Omschrijving))]
    public partial string Tekst { get; set; }

    public ObservableCollection<RubriekKeuze> Rubrieken { get; }

    public bool IsRegel => Soort == IndelingsregelSoort.Regel;

    /// <summary>Zoals in de lijst: kopjes vet in de weergave, regels ingesprongen, met het aantal rubrieken.</summary>
    public string Omschrijving => Soort switch
    {
        IndelingsregelSoort.Kop => $"{Jaarrekeningindeling.Naam(Afdeling)} · {Tekst}",
        IndelingsregelSoort.Subtotaal => $"{Jaarrekeningindeling.Naam(Afdeling)} ·     Σ {Tekst}",
        _ => $"{Jaarrekeningindeling.Naam(Afdeling)} ·     {Tekst} ({Rubrieken.Count(r => r.IsGekozen)} rubriek{(Rubrieken.Count(r => r.IsGekozen) == 1 ? string.Empty : "en")})",
    };

    public IndelingsregelGegevens NaarGegevens() =>
        new(Afdeling, Soort, Tekst, Soort == IndelingsregelSoort.Regel ? [.. Rubrieken.Where(r => r.IsGekozen).Select(r => r.Rubriek.Id)] : []);
}

/// <summary>
/// Venster <i>Indeling bewerken</i> (besluiten 263–268): kopjes, regels met rubrieken en subtotalen per deel. Een rubriek
/// kan maar bij één regel van een deel staan; bij de andere regels is hij grijs.
/// </summary>
public sealed partial class JaarrekeningIndelingViewModel : ObservableObject
{
    private readonly JaarrekeningDienst _dienst;
    private readonly BoekhoudingStand _stand;
    private readonly List<Rubriek> _beschikbaar;

    public JaarrekeningIndelingViewModel(JaarrekeningDienst dienst, BoekhoudingStand stand, JaarrekeningRapport rapport)
    {
        _dienst = dienst;
        _stand = stand;
        Rapport = rapport;
        var soort = rapport == JaarrekeningRapport.Balans ? RekeningSoort.Balans : RekeningSoort.Resultaat;
        _beschikbaar = [.. stand.Rubrieken
            .Where(r => !stand.Rekeningen.Any(g => g.Rubriek?.Id == r.Id) || stand.Rekeningen.Any(g => g.Rubriek?.Id == r.Id && g.Soort == soort))
            .OrderBy(r => r.Volgorde)];
        Afdelingen = [.. Jaarrekeningindeling.Afdelingen(rapport).Select(a => new Keuze<JaarrekeningAfdeling>(a, Jaarrekeningindeling.Naam(a)))];
        var eigen = stand.Jaarrekeningindelingen.FirstOrDefault(i => i.Rapport == rapport && i.Regels.Count > 0);
        Laad(eigen is null
            ? Jaarrekeningrekenaar.AutomatischeIndeling(stand, rapport)
            : [.. eigen.Regels.Select(r => new IndelingsregelInvoer(r.Afdeling, r.Soort, r.Tekst, r.Rubrieken))]);
    }

    public JaarrekeningRapport Rapport { get; }

    public string Titel => Rapport == JaarrekeningRapport.Balans ? "Indeling van de balans" : "Indeling van de staat van baten en lasten";

    public IReadOnlyList<Keuze<JaarrekeningAfdeling>> Afdelingen { get; }

    public IReadOnlyList<Keuze<IndelingsregelSoort>> Soorten { get; } =
        [new(IndelingsregelSoort.Kop, "Kop"), new(IndelingsregelSoort.Regel, "Regel"), new(IndelingsregelSoort.Subtotaal, "Subtotaal")];

    public ObservableCollection<IndelingsregelRij> Regels { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(VerwijderCommand), nameof(OmhoogCommand), nameof(OmlaagCommand))]
    public partial IndelingsregelRij? Geselecteerd { get; set; }

    [ObservableProperty]
    public partial string? Fout { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpslaanCommand))]
    public partial bool IsBezig { get; private set; }

    public bool IsOpgeslagen { get; private set; }

    [RelayCommand]
    private void NieuweRegel() => Voegtoe(IndelingsregelSoort.Regel, "Nieuwe regel");

    [RelayCommand]
    private void NieuweKop() => Voegtoe(IndelingsregelSoort.Kop, "Nieuwe kop");

    [RelayCommand]
    private void NieuwSubtotaal() => Voegtoe(IndelingsregelSoort.Subtotaal, "Subtotaal");

    [RelayCommand(CanExecute = nameof(HeeftSelectie))]
    private void Verwijder()
    {
        if (Geselecteerd is not { } rij)
        {
            return;
        }

        var index = Regels.IndexOf(rij);
        Regels.Remove(rij);
        Geselecteerd = Regels.Count == 0 ? null : Regels[Math.Min(index, Regels.Count - 1)];
        WerkEldersBij();
    }

    [RelayCommand(CanExecute = nameof(KanOmhoog))]
    private void Omhoog() => Verplaats(-1);

    [RelayCommand(CanExecute = nameof(KanOmlaag))]
    private void Omlaag() => Verplaats(1);

    /// <summary>Terug naar één regel per rubriek; opslaan maakt het definitief.</summary>
    [RelayCommand]
    private void Automatisch()
    {
        Laad(Jaarrekeningrekenaar.AutomatischeIndeling(_stand, Rapport));
        Fout = null;
    }

    [RelayCommand(CanExecute = nameof(KanOpslaan))]
    private async Task OpslaanAsync()
    {
        IsBezig = true;
        try
        {
            await _dienst.BewaarAsync(_stand.Administratie, Rapport, [.. Regels.Select(r => r.NaarGegevens())]);
            IsOpgeslagen = true;
            Fout = null;
            Gesloten?.Invoke(this, EventArgs.Empty);
        }
        catch (ArgumentException ex)
        {
            Fout = "✕ De indeling is niet opgeslagen: " + ex.Message.Split(" (Parameter", 2)[0] + " Pas de regel aan en probeer het opnieuw.";
        }
        finally
        {
            IsBezig = false;
        }
    }

    /// <summary>Na opslaan; het venster sluit dan.</summary>
    public event EventHandler? Gesloten;

    private bool HeeftSelectie() => Geselecteerd is not null;

    private bool KanOmhoog() => Geselecteerd is { } rij && Regels.IndexOf(rij) is var i && i > 0 && Regels[i - 1].Afdeling == rij.Afdeling;

    private bool KanOmlaag() => Geselecteerd is { } rij && Regels.IndexOf(rij) is var i && i < Regels.Count - 1 && Regels[i + 1].Afdeling == rij.Afdeling;

    private bool KanOpslaan() => !IsBezig;

    private void Laad(IReadOnlyList<IndelingsregelInvoer> regels)
    {
        foreach (var rij in Regels)
        {
            Ontkoppel(rij);
        }

        Regels.Clear();
        foreach (var regel in regels.OrderBy(r => r.Afdeling))
        {
            Regels.Add(Koppel(new IndelingsregelRij(regel.Afdeling, regel.Soort, regel.Tekst, _beschikbaar, regel.Rubrieken)));
        }

        Geselecteerd = Regels.FirstOrDefault();
        WerkEldersBij();
    }

    private void Voegtoe(IndelingsregelSoort soort, string tekst)
    {
        var afdeling = Geselecteerd?.Afdeling ?? Afdelingen[0].Waarde;
        var rij = Koppel(new IndelingsregelRij(afdeling, soort, tekst, _beschikbaar, []));
        var index = Geselecteerd is { } huidig ? Regels.IndexOf(huidig) + 1 : LaatsteVan(afdeling) + 1;
        Regels.Insert(index, rij);
        Geselecteerd = rij;
        WerkEldersBij();
    }

    private int LaatsteVan(JaarrekeningAfdeling afdeling)
    {
        for (var i = Regels.Count - 1; i >= 0; i--)
        {
            if (Regels[i].Afdeling <= afdeling)
            {
                return i;
            }
        }

        return -1;
    }

    private void Verplaats(int richting)
    {
        if (Geselecteerd is not { } rij)
        {
            return;
        }

        var index = Regels.IndexOf(rij);
        Regels.Move(index, index + richting);
        Geselecteerd = rij;
        OmhoogCommand.NotifyCanExecuteChanged();
        OmlaagCommand.NotifyCanExecuteChanged();
    }

    private IndelingsregelRij Koppel(IndelingsregelRij rij)
    {
        rij.RubriekGewijzigd += RubriekGewijzigd;
        rij.PropertyChanged += RijGewijzigd;
        return rij;
    }

    private void Ontkoppel(IndelingsregelRij rij)
    {
        rij.RubriekGewijzigd -= RubriekGewijzigd;
        rij.PropertyChanged -= RijGewijzigd;
    }

    private void RubriekGewijzigd(object? sender, EventArgs e) => WerkEldersBij();

    private void RijGewijzigd(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not IndelingsregelRij rij)
        {
            return;
        }

        if (e.PropertyName == nameof(IndelingsregelRij.Afdeling))
        {
            // Een regel die van deel wisselt, sluit achteraan bij zijn nieuwe deel aan.
            Regels.Remove(rij);
            Regels.Insert(LaatsteVan(rij.Afdeling) + 1, rij);
            Geselecteerd = rij;
            WerkEldersBij();
        }
        else if (e.PropertyName == nameof(IndelingsregelRij.Soort))
        {
            WerkEldersBij();
        }
    }

    /// <summary>Een rubriek die al bij een andere regel van hetzelfde deel staat, is bij deze regel niet te kiezen.</summary>
    private void WerkEldersBij()
    {
        foreach (var rij in Regels)
        {
            foreach (var keuze in rij.Rubrieken)
            {
                keuze.IsElders = Regels.Any(andere => !ReferenceEquals(andere, rij) && andere.Afdeling == rij.Afdeling && andere.IsRegel
                    && andere.Rubrieken.Any(k => k.IsGekozen && k.Rubriek.Id == keuze.Rubriek.Id));
            }
        }
    }
}
