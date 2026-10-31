using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Verenigd.App.ViewModels.Contributie;
using Verenigd.Application.Begrotingen;
using Verenigd.Application.Boekhouding;
using Verenigd.Domain.Administraties;
using Verenigd.Domain.Financien;

namespace Verenigd.App.ViewModels.Boekhouding;

/// <summary>
/// Subtabblad Meerjarenplan van de Stichting (fase 5b, besluiten 159–167): per resultaatrekening een bedrag per jaar over
/// 10 jaar, kopiëren naar de volgende jaren, vaststellen en herzien. Ctrl+S slaat op; Ctrl+P drukt liggend af.
/// </summary>
public sealed partial class BoekhoudingViewModel
{
    private bool _meerjarenWordtGeladen;

    private MeerjarenplanDienst Meerjarendienst => field ??= new MeerjarenplanDienst(_sessie, _tijd);

    public ObservableCollection<MeerjarenRij> Meerjarenrijen { get; } = [];

    /// <summary>Besluit 163: alleen bij de Stichting.</summary>
    public bool HeeftMeerjarenplanTab => Soort == AdministratieSoort.Stichting;

    [ObservableProperty]
    public partial IReadOnlyList<int> MeerjarenStartjaren { get; private set; } = [];

    [ObservableProperty]
    public partial int MeerjarenStartjaar { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<MeerjarenVersie> Meerjarenversies { get; private set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HeeftMeerjarenplan), nameof(IsMeerjarenplanBewerkbaar), nameof(MeerjarenJaren), nameof(IsOpslaanHoofdactie))]
    public partial MeerjarenVersie? GekozenMeerjarenversie { get; set; }

    /// <summary>De jaren als kolomkoppen.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<string> Meerjarenkoppen { get; private set; } = [];

    [ObservableProperty]
    public partial string Meerjarentotalen { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial int KopieerVanafJaar { get; set; }

    [ObservableProperty]
    public partial DateTime? MeerjarenVaststellenOp { get; set; }

    [ObservableProperty]
    public partial string MeerjarenVaststellenOmschrijving { get; set; } = string.Empty;

    /// <summary>De jaren van het gekozen plan, voor de keuze "vanaf jaar" bij kopiëren.</summary>
    public IReadOnlyList<int> MeerjarenJaren => GekozenMeerjarenversie is { } versie ? [.. versie.Plan.Jaren] : [];

    public bool HeeftMeerjarenplan => GekozenMeerjarenversie is not null;

    public bool IsMeerjarenplanBewerkbaar => GekozenMeerjarenversie is { Plan.IsVastgesteld: false };

    private bool MeerjarenplanGewijzigd => Meerjarenrijen.Any(r => r.IsGewijzigd);

    private bool MeerjarenplanGeldig => Meerjarenrijen.Where(r => r.IsGewijzigd).All(r => r.Fout is null);

    partial void OnMeerjarenStartjaarChanged(int value) => LaadMeerjarenplan(null);

    partial void OnGekozenMeerjarenversieChanged(MeerjarenVersie? value)
    {
        if (!_meerjarenWordtGeladen)
        {
            VulMeerjarenrijen();
        }

        MeerjarenCommandsVernieuwen();
    }

    private void LaadMeerjarenplan(int? versieId)
    {
        if (_stand is null || !HeeftMeerjarenplanTab)
        {
            return;
        }

        MeerjarenStartjaren = [.. _stand.Meerjarenplannen.Select(p => p.Startjaar).Append(Vandaag.Year).Append(Vandaag.Year + 1).Distinct().Order()];
        if (MeerjarenStartjaar == 0)
        {
            MeerjarenStartjaar = Vandaag.Year + 1; // besluit 167; roept LaadMeerjarenplan opnieuw aan
            return;
        }

        _meerjarenWordtGeladen = true;
        try
        {
            Meerjarenversies = [.. _stand.Meerjarenplannen.Where(p => p.Startjaar == MeerjarenStartjaar).OrderBy(p => p.Versie).Select(p => new MeerjarenVersie(p))];
            var geldend = Meerjarenrekenaar.Geldend(_stand, MeerjarenStartjaar);
            GekozenMeerjarenversie = Meerjarenversies.FirstOrDefault(v => v.Id == versieId)
                ?? Meerjarenversies.FirstOrDefault(v => !v.Plan.IsVastgesteld && v.Plan.Versie > (geldend?.Versie ?? 0))
                ?? Meerjarenversies.FirstOrDefault(v => v.Id == geldend?.Id);
        }
        finally
        {
            _meerjarenWordtGeladen = false;
        }

        VulMeerjarenrijen();
        OnPropertyChanged(nameof(Afdruktabel));
    }

    private void VulMeerjarenrijen()
    {
        foreach (var rij in Meerjarenrijen)
        {
            rij.PropertyChanged -= MeerjarenrijGewijzigd;
        }

        Meerjarenrijen.Clear();
        if (_stand is null || GekozenMeerjarenversie is not { } versie)
        {
            Meerjarenkoppen = [];
            Meerjarentotalen = string.Empty;
            CommandsVernieuwen();
            return;
        }

        var overzicht = Meerjarenrekenaar.Overzicht(_stand, versie.Plan);
        Meerjarenkoppen = [.. overzicht.Jaren.Select(j => j.ToString(CultureInfo.InvariantCulture))];
        if (KopieerVanafJaar < versie.Plan.Startjaar || KopieerVanafJaar > versie.Plan.Eindjaar)
        {
            KopieerVanafJaar = versie.Plan.Startjaar;
        }

        foreach (var regel in overzicht.Baten.Concat(overzicht.Lasten))
        {
            var rij = new MeerjarenRij(regel, overzicht.Jaren);
            rij.PropertyChanged += MeerjarenrijGewijzigd;
            Meerjarenrijen.Add(rij);
        }

        BerekenMeerjarentotalen();
        CommandsVernieuwen();
    }

    private void MeerjarenrijGewijzigd(object? sender, PropertyChangedEventArgs e)
    {
        BerekenMeerjarentotalen();
        CommandsVernieuwen();
    }

    private void BerekenMeerjarentotalen()
    {
        if (GekozenMeerjarenversie is not { } versie)
        {
            return;
        }

        var delen = versie.Plan.Jaren.Select((jaar, i) =>
        {
            var resultaat = Meerjarenrijen.Sum(r => (r.Cellen[i].Bedrag?.Centen ?? 0) * (r.Kanttekst == "Baten" ? 1 : -1));
            return string.Create(CultureInfo.CurrentCulture, $"{jaar}: resultaat {new Bedrag(resultaat)}");
        });
        Meerjarentotalen = string.Join(" · ", delen);
    }

    private async Task BewaarMeerjarenplanAsync()
    {
        if (GekozenMeerjarenversie is { } versie && MeerjarenplanGewijzigd)
        {
            await Meerjarendienst.BewaarAsync(Soort, versie.Id, [.. Meerjarenrijen.Where(r => r.IsGewijzigd).Select(r => r.Invoer)]);
        }
    }

    private RapportTabel? MeerjarenAfdruk() =>
        _stand is not null && GekozenMeerjarenversie is { } versie
            ? Rapporten.Meerjarenplan(Meerjarenrekenaar.Overzicht(_stand, versie.Plan), Administratienaam)
            : null;

    [RelayCommand(CanExecute = nameof(KanMeerjarenplanMaken))]
    private Task MeerjarenplanMakenAsync() =>
        VoerUitAsync(async () =>
        {
            var id = await Meerjarendienst.MaakAsync(Soort, MeerjarenStartjaar);
            await HerlaadMeerjarenplanAsync(id);
            Melding = $"Het meerjarenplan {MeerjarenStartjaar}–{MeerjarenStartjaar + 9} is aangemaakt.";
        }, null);

    /// <summary>Besluit 166: een knop per rij; het bedrag van het gekozen jaar gaat naar alle latere jaren van die rij.</summary>
    [RelayCommand(CanExecute = nameof(KanKopieren))]
    private void NaarVolgendeJarenKopieren(MeerjarenRij? rij) => rij?.KopieerNaarVolgendeJaren(KopieerVanafJaar);

    [RelayCommand(CanExecute = nameof(KanMeerjarenplanWijzigen))]
    private async Task MeerjarenplanVaststellenAsync()
    {
        var versie = GekozenMeerjarenversie!;
        var datum = Weergave.AlsDatum(MeerjarenVaststellenOp) ?? Vandaag;
        if (!_dialogen.Bevestig("Meerjarenplan vaststellen",
            $"Het meerjarenplan {versie.Plan.Startjaar}–{versie.Plan.Eindjaar} vaststellen per {Weergave.Datum(datum)}? Daarna kan deze versie niet meer worden gewijzigd; wel kunt u een herziening maken."))
        {
            return;
        }

        await VoerUitAsync(async () =>
        {
            await BewaarMeerjarenplanAsync();
            await Meerjarendienst.StelVastAsync(Soort, versie.Id, datum, MeerjarenVaststellenOmschrijving);
            await HerlaadMeerjarenplanAsync(versie.Id);
            Melding = "Het meerjarenplan is vastgesteld.";
        }, null);
    }

    [RelayCommand(CanExecute = nameof(KanMeerjarenplanHerzien))]
    private Task MeerjarenplanHerzienAsync() =>
        VoerUitAsync(async () =>
        {
            var id = await Meerjarendienst.HerzieAsync(Soort, GekozenMeerjarenversie!.Id);
            await HerlaadMeerjarenplanAsync(id);
            Melding = "Er is een herziening gemaakt; de vastgestelde versie blijft bewaard.";
        }, null);

    private bool KanMeerjarenplanMaken() => !IsBezig && IsIngericht && HeeftMeerjarenplanTab && Meerjarenversies.Count == 0 && MeerjarenStartjaar > 0;

    private bool KanMeerjarenplanWijzigen() => !IsBezig && IsMeerjarenplanBewerkbaar;

    private bool KanKopieren(MeerjarenRij? rij) => IsMeerjarenplanBewerkbaar && rij is not null;

    private bool KanMeerjarenplanHerzien() =>
        !IsBezig && GekozenMeerjarenversie is { Plan.IsVastgesteld: true } versie && versie.Id == Meerjarenversies[^1].Id;

    private async Task HerlaadMeerjarenplanAsync(int versieId)
    {
        _stand = await _dienst.LaadAsync(Soort);
        LaadMeerjarenplan(versieId);
    }

    private void MeerjarenCommandsVernieuwen()
    {
        MeerjarenplanMakenCommand.NotifyCanExecuteChanged();
        NaarVolgendeJarenKopierenCommand.NotifyCanExecuteChanged();
        MeerjarenplanVaststellenCommand.NotifyCanExecuteChanged();
        MeerjarenplanHerzienCommand.NotifyCanExecuteChanged();
    }
}
