using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Verenigd.App.ViewModels.Contributie;
using Verenigd.Application.Begrotingen;
using Verenigd.Application.Boekhouding;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Financien.Boekhouding;

namespace Verenigd.App.ViewModels.Boekhouding;

/// <summary>
/// Subtabblad Begroting (fase 5a, besluiten 146–155): een jaarbedrag per resultaatrekening, overnemen met een percentage,
/// vaststellen en herzien. Ctrl+S slaat de bedragen op; Ctrl+P drukt de begroting voor de ALV af.
/// </summary>
public sealed partial class BoekhoudingViewModel
{
    private BegrotingDienst Begrotingdienst => field ??= new BegrotingDienst(_sessie, _tijd);

    public ObservableCollection<BegrotingRij> Begrotingsrijen { get; } = [];

    public IReadOnlyList<BegrotingBron> Begrotingsbronnen { get; } = Enum.GetValues<BegrotingBron>();

    [ObservableProperty]
    public partial IReadOnlyList<int> Begrotingsjaren { get; private set; } = [];

    [ObservableProperty]
    public partial int Begrotingsjaar { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<BegrotingVersie> Begrotingsversies { get; private set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HeeftBegroting), nameof(IsBegrotingBewerkbaar), nameof(IsOpslaanHoofdactie))]
    public partial BegrotingVersie? GekozenBegrotingsversie { get; set; }

    [ObservableProperty]
    public partial string Begrotingstotalen { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial BegrotingBron OvernemenBron { get; set; }

    [ObservableProperty]
    public partial int OvernemenJaar { get; set; }

    [ObservableProperty]
    public partial string OvernemenPercentage { get; set; } = "0";

    [ObservableProperty]
    public partial DateTime? VaststellenOp { get; set; }

    [ObservableProperty]
    public partial string VaststellenOmschrijving { get; set; } = string.Empty;

    public bool HeeftBegroting => GekozenBegrotingsversie is not null;

    public bool IsBegrotingBewerkbaar => GekozenBegrotingsversie is { Begroting.IsVastgesteld: false };

    private bool BegrotingGewijzigd => Begrotingsrijen.Any(r => r.IsGewijzigd);

    private bool BegrotingGeldig => Begrotingsrijen.Where(r => r.IsGewijzigd).All(r => r.Fout is null);

    partial void OnBegrotingsjaarChanged(int value)
    {
        OvernemenJaar = value - 1;
        LaadBegroting(null);
    }

    partial void OnGekozenBegrotingsversieChanged(BegrotingVersie? value)
    {
        if (!_begrotingWordtGeladen)
        {
            VulBegrotingsrijen();
        }

        BegrotingCommandsVernieuwen();
    }

    private bool _begrotingWordtGeladen;

    /// <summary>Jaren met een boekjaar of begroting, plus het volgende jaar.</summary>
    private void LaadBegroting(int? versieId)
    {
        if (_stand is null)
        {
            return;
        }

        Begrotingsjaren = [.. _stand.Boekjaren.Where(b => !b.IsHistorisch).Select(b => b.Jaar)
            .Concat(_stand.Begrotingen.Select(b => b.Jaar)).Append(Vandaag.Year).Append(Vandaag.Year + 1).Distinct().Order()];
        if (Begrotingsjaar == 0)
        {
            Begrotingsjaar = Vandaag.Year; // roept LaadBegroting opnieuw aan
            return;
        }

        _begrotingWordtGeladen = true;
        try
        {
            Begrotingsversies = [.. _stand.Begrotingen.Where(b => b.Jaar == Begrotingsjaar).OrderBy(b => b.Versie).Select(b => new BegrotingVersie(b))];
            var geldend = Begrotingsrekenaar.Geldend(_stand, Begrotingsjaar);
            GekozenBegrotingsversie = Begrotingsversies.FirstOrDefault(v => v.Id == versieId)
                ?? Begrotingsversies.FirstOrDefault(v => v.Begroting.Status == Domain.Financien.Begrotingen.BegrotingStatus.Concept && v.Begroting.Versie > (geldend?.Versie ?? 0))
                ?? Begrotingsversies.FirstOrDefault(v => v.Id == geldend?.Id);
        }
        finally
        {
            _begrotingWordtGeladen = false;
        }

        VulBegrotingsrijen();
        OnPropertyChanged(nameof(Afdruktabel));
    }

    private void VulBegrotingsrijen()
    {
        foreach (var rij in Begrotingsrijen)
        {
            rij.PropertyChanged -= BegrotingsrijGewijzigd;
        }

        Begrotingsrijen.Clear();
        if (_stand is null || GekozenBegrotingsversie is not { } versie)
        {
            Begrotingstotalen = string.Empty;
            CommandsVernieuwen();
            return;
        }

        // Het overzicht van de gekozen versie: zet die tijdelijk als enige begroting van het jaar.
        var stand = _stand with { Begrotingen = [.. _stand.Begrotingen.Where(b => b.Jaar != Begrotingsjaar), versie.Begroting] };
        var overzicht = Begrotingsrekenaar.Overzicht(stand, Begrotingsjaar, Vandaag);
        foreach (var regel in overzicht.Baten.Concat(overzicht.Lasten).SelectMany(r => r.Regels))
        {
            var rij = new BegrotingRij(regel, versie.Begroting.Regels.FirstOrDefault(r => r.Rekening.Id == regel.Rekening.Id)?.Toelichting);
            rij.PropertyChanged += BegrotingsrijGewijzigd;
            Begrotingsrijen.Add(rij);
        }

        BerekenBegrotingstotalen();
        CommandsVernieuwen();
    }

    private void BegrotingsrijGewijzigd(object? sender, PropertyChangedEventArgs e)
    {
        BerekenBegrotingstotalen();
        CommandsVernieuwen();
    }

    private void BerekenBegrotingstotalen()
    {
        long Som(ResultaatKant kant) => Begrotingsrijen.Where(r => (r.Rekening.Kant == ResultaatKant.Baten) == (kant == ResultaatKant.Baten))
            .Sum(r => r.Bedrag?.Centen ?? 0);
        var baten = Som(ResultaatKant.Baten);
        var lasten = Som(ResultaatKant.Lasten);
        Begrotingstotalen = string.Create(CultureInfo.CurrentCulture,
            $"{Woorden["Begroot"]}: {Woorden["Baten"].ToLowerInvariant()} {new Bedrag(baten)} · {Woorden["Lasten"].ToLowerInvariant()} {new Bedrag(lasten)} · resultaat {new Bedrag(baten - lasten)}");
    }

    private async Task BewaarBegrotingAsync()
    {
        if (GekozenBegrotingsversie is { } versie && BegrotingGewijzigd)
        {
            await Begrotingdienst.BewaarAsync(Soort, versie.Id, [.. Begrotingsrijen.Where(r => r.IsGewijzigd).Select(r => r.Invoer)]);
        }
    }

    [RelayCommand(CanExecute = nameof(KanBegrotingMaken))]
    private Task BegrotingMakenAsync() =>
        VoerUitAsync(async () =>
        {
            var id = await Begrotingdienst.MaakAsync(Soort, Begrotingsjaar);
            await HerlaadBegrotingAsync(id);
            Melding = $"De begroting {Begrotingsjaar} is aangemaakt.";
        }, null);

    /// <summary>Vervangt alle bedragen (na bevestiging als er al bedragen zijn).</summary>
    [RelayCommand(CanExecute = nameof(KanBegrotingWijzigen))]
    private async Task BegrotingOvernemenAsync()
    {
        if (!decimal.TryParse(OvernemenPercentage.Replace('.', ','), NumberStyles.Number, CultureInfo.GetCultureInfo("nl-NL"), out var percentage))
        {
            Foutmelding = "Vul een percentage in, bijvoorbeeld 3 of 2,5.";
            return;
        }

        var bron = OvernemenBron == BegrotingBron.Werkelijk ? $"de werkelijke cijfers van {OvernemenJaar}" : $"de begroting van {OvernemenJaar}";
        if (Begrotingsrijen.Any(r => r.Bedrag is { Centen: > 0 })
            && !_dialogen.Bevestig("Begroting overnemen", $"Alle bedragen vervangen door {bron} plus {percentage.ToString(CultureInfo.CurrentCulture)}%, afgerond op tientallen euro's?"))
        {
            return;
        }

        var id = GekozenBegrotingsversie!.Id;
        await VoerUitAsync(async () =>
        {
            await Begrotingdienst.NeemOverAsync(Soort, id, OvernemenBron, OvernemenJaar, percentage);
            await HerlaadBegrotingAsync(id);
            Melding = $"Overgenomen van {bron} plus {percentage.ToString(CultureInfo.CurrentCulture)}%. Pas de bedragen zo nodig aan.";
        }, null);
    }

    [RelayCommand(CanExecute = nameof(KanBegrotingWijzigen))]
    private async Task BegrotingVaststellenAsync()
    {
        var datum = Weergave.AlsDatum(VaststellenOp) ?? Vandaag;
        if (!_dialogen.Bevestig("Begroting vaststellen",
            $"De begroting {Begrotingsjaar} vaststellen per {Weergave.Datum(datum)}? Daarna kan deze versie niet meer worden gewijzigd; wel kunt u een herziening maken."))
        {
            return;
        }

        var id = GekozenBegrotingsversie!.Id;
        await VoerUitAsync(async () =>
        {
            await BewaarBegrotingAsync();
            await Begrotingdienst.StelVastAsync(Soort, id, datum, VaststellenOmschrijving);
            await HerlaadBegrotingAsync(id);
            Melding = $"De begroting {Begrotingsjaar} is vastgesteld.";
        }, null);
    }

    [RelayCommand(CanExecute = nameof(KanBegrotingHerzien))]
    private Task BegrotingHerzienAsync() =>
        VoerUitAsync(async () =>
        {
            var id = await Begrotingdienst.HerzieAsync(Soort, GekozenBegrotingsversie!.Id);
            await HerlaadBegrotingAsync(id);
            Melding = "Er is een herziening gemaakt; de vastgestelde versie blijft bewaard.";
        }, null);

    private bool KanBegrotingMaken() => !IsBezig && IsIngericht && Begrotingsversies.Count == 0 && Begrotingsjaar > 0;

    private bool KanBegrotingWijzigen() => !IsBezig && IsBegrotingBewerkbaar;

    private bool KanBegrotingHerzien() =>
        !IsBezig && GekozenBegrotingsversie is { Begroting.IsVastgesteld: true } versie && versie.Id == Begrotingsversies[^1].Id;

    private async Task HerlaadBegrotingAsync(int versieId)
    {
        _stand = await _dienst.LaadAsync(Soort);
        LaadBegroting(versieId);
    }

    private void BegrotingCommandsVernieuwen()
    {
        BegrotingMakenCommand.NotifyCanExecuteChanged();
        BegrotingOvernemenCommand.NotifyCanExecuteChanged();
        BegrotingVaststellenCommand.NotifyCanExecuteChanged();
        BegrotingHerzienCommand.NotifyCanExecuteChanged();
    }
}
