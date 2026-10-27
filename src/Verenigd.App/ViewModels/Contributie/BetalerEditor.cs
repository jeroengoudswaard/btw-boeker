using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Verenigd.Application.Incasso;
using Verenigd.Domain.Financien;

namespace Verenigd.App.ViewModels.Contributie;

/// <summary>Bewerkt één betaler: IBAN, rekeninghouder, machtiging (besluit 48) en gekoppelde leden.</summary>
public sealed partial class BetalerEditor : ObservableObject
{
    private readonly IReadOnlyList<Betaler> _alleBetalers;
    private readonly string _origineel;

    public BetalerEditor(Betaler? betaler, IEnumerable<LidKeuze> leden, IReadOnlyList<LidKeuze> beschikbaar, IReadOnlyList<Betaler> alleBetalers)
    {
        _alleBetalers = alleBetalers;
        Id = betaler?.Id;
        Iban = betaler?.Iban.ToString() ?? string.Empty;
        Rekeninghouder = betaler?.Rekeninghouder;
        Mandaatkenmerk = betaler?.Mandaatkenmerk;
        OndertekendOp = Weergave.AlsDateTime(betaler?.MachtigingOndertekendOp);
        IngetrokkenOp = Weergave.AlsDateTime(betaler?.MachtigingIngetrokkenOp);
        foreach (var lid in leden)
        {
            Leden.Add(lid);
        }

        BeschikbareLeden = beschikbaar;
        Leden.CollectionChanged += (_, _) => Gewijzigd();
        _origineel = Momentopname();
    }

    public Guid? Id { get; }

    public bool IsNieuw => Id is null;

    public ObservableCollection<LidKeuze> Leden { get; } = [];

    public IReadOnlyList<LidKeuze> BeschikbareLeden { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Fouten), nameof(IsGewijzigd))]
    public partial string Iban { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Fouten), nameof(IsGewijzigd))]
    public partial string? Rekeninghouder { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Fouten), nameof(IsGewijzigd))]
    public partial string? Mandaatkenmerk { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Fouten), nameof(IsGewijzigd))]
    public partial DateTime? OndertekendOp { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Fouten), nameof(IsGewijzigd))]
    public partial DateTime? IngetrokkenOp { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(VoegLidToeCommand))]
    public partial LidKeuze? LidToevoegen { get; set; }

    public IReadOnlyList<string> Fouten => Invoer.Fouten(_alleBetalers);

    public bool IsGewijzigd => Momentopname() != _origineel;

    public BetalerInvoer Invoer => new(
        Id,
        Iban ?? string.Empty,
        string.IsNullOrWhiteSpace(Rekeninghouder) ? null : Rekeninghouder.Trim(),
        string.IsNullOrWhiteSpace(Mandaatkenmerk) ? null : Mandaatkenmerk.Trim(),
        Weergave.AlsDatum(OndertekendOp),
        Weergave.AlsDatum(IngetrokkenOp),
        [.. Leden.Select(l => l.Lidnummer)]);

    [RelayCommand(CanExecute = nameof(KanLidToevoegen))]
    private void VoegLidToe()
    {
        if (LidToevoegen is { } lid && !Leden.Contains(lid))
        {
            Leden.Add(lid);
        }

        LidToevoegen = null;
    }

    private bool KanLidToevoegen() => LidToevoegen is not null;

    [RelayCommand]
    private void VerwijderLid(LidKeuze? lid)
    {
        if (lid is not null)
        {
            Leden.Remove(lid);
        }
    }

    private void Gewijzigd()
    {
        OnPropertyChanged(nameof(Fouten));
        OnPropertyChanged(nameof(IsGewijzigd));
    }

    private string Momentopname() =>
        string.Join('|', Iban, Rekeninghouder, Mandaatkenmerk, OndertekendOp, IngetrokkenOp, string.Join(',', Leden.Select(l => l.Lidnummer.Waarde)));
}
