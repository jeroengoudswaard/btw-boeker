using CommunityToolkit.Mvvm.Input;

namespace BtwBoeker.App.ViewModels.Boekhouding;

/// <summary>
/// UX-review stap 3 (besluit 38): één patroon voor lijst en detail. De lijst neemt de volle breedte; het detail opent pas bij
/// een selectie of <i>Nieuw</i> en sluit met ✕ of Esc. Opslaan blijft één opslag per pagina (Ctrl+S); de knop staat in het
/// detail, of onderaan als er geen detail open is.
/// </summary>
public sealed partial class BoekhoudingViewModel
{
    /// <summary>Of het detail van het huidige onderdeel open is.</summary>
    public bool IsDetailOpen => Subtab switch
    {
        BoekhoudingSubtab.Journaal => Editor is not null,
        BoekhoudingSubtab.Bank => GeselecteerdeMutatie is not null,
        BoekhoudingSubtab.Postvak => GeselecteerdeBon is not null,
        BoekhoudingSubtab.Boekingsregels => GeselecteerdeRegel is not null,
        BoekhoudingSubtab.Relaties => GeselecteerdeRelatie is not null,
        BoekhoudingSubtab.Rekeningschema => GeselecteerdeRekening is not null,
        _ => false,
    };

    /// <summary>De knop Opslaan in het detail.</summary>
    public bool HeeftOnopgeslagenWijzigingenMetDetail => HeeftOnopgeslagenWijzigingen && IsDetailOpen;

    /// <summary>De knop Opslaan onderaan, als er wijzigingen zijn maar geen detail open is.</summary>
    public bool HeeftOnopgeslagenWijzigingenZonderDetail => HeeftOnopgeslagenWijzigingen && !IsDetailOpen;

    /// <summary>De jaarkeuze alleen bij meer dan één boekjaar, en alleen waar het jaar iets doet.</summary>
    public bool IsJaarkeuzeZichtbaar =>
        HeeftMeerdereBoekjaren && Subtab is BoekhoudingSubtab.Journaal or BoekhoudingSubtab.Bank or BoekhoudingSubtab.Rapporten or BoekhoudingSubtab.Grootboekkaart;

    /// <summary>✕ of Esc: het detail van het huidige onderdeel sluiten. Een wijziging blijft staan tot Opslaan; een gewijzigde post of
    /// een ingevulde nieuwe regel wordt pas na bevestiging verworpen.</summary>
    [RelayCommand]
    private Task SluitDetailAsync()
    {
        switch (Subtab)
        {
            case BoekhoudingSubtab.Journaal:
                if (Editor is not null && BevestigVerwerpen())
                {
                    _selectieWordtGezet = true;
                    GeselecteerdePost = null;
                    _selectieWordtGezet = false;
                    ToonEditor(null, nieuw: false);
                }

                break;
            case BoekhoudingSubtab.Bank:
                GeselecteerdeMutatie = null;
                break;
            case BoekhoudingSubtab.Postvak:
                GeselecteerdeBon = null;
                break;
            case BoekhoudingSubtab.Boekingsregels:
                SluitRegel();
                break;
            case BoekhoudingSubtab.Relaties:
                GeselecteerdeRelatie = null;
                break;
            case BoekhoudingSubtab.Rekeningschema:
                GeselecteerdeRekening = null;
                break;
        }

        MeldDetail();
        return Task.CompletedTask;
    }

    private void SluitRegel()
    {
        if (GeselecteerdeRegel is not { IsNieuw: true } nieuw)
        {
            GeselecteerdeRegel = null;
            return;
        }

        var ingevuld = nieuw.Naam.Length > 0 || nieuw.GekozenRekening is not null || nieuw.Wanneer != "nog geen voorwaarde";
        if (ingevuld && !_dialogen.Bevestig("Boekingsregel", "De nieuwe regel is niet opgeslagen. Wilt u hem verwerpen?"))
        {
            return;
        }

        nieuw.PropertyChanged -= RegelGewijzigd;
        GeselecteerdeRegel = null;
        Boekingsregels.Remove(nieuw);
        CommandsVernieuwen();
    }

    private void MeldDetail()
    {
        OnPropertyChanged(nameof(IsDetailOpen));
        OnPropertyChanged(nameof(HeeftOnopgeslagenWijzigingenMetDetail));
        OnPropertyChanged(nameof(HeeftOnopgeslagenWijzigingenZonderDetail));
    }

    partial void OnGeselecteerdeRelatieChanged(RelatieRegel? value) => MeldDetail();

    partial void OnGeselecteerdeRekeningChanged(RekeningRegel? value) => MeldDetail();

    /// <summary>Datum en bedrag van een bon bewaren zichzelf als de gebruiker een andere bon kiest of het detail sluit.</summary>
    partial void OnGeselecteerdeBonChanged(BonRegel? oldValue, BonRegel? newValue)
    {
        MeldDetail();
        if (oldValue is { IsGewijzigd: true, Fout: null } bon && !IsBezig)
        {
            _ = BewaarBonStilAsync(bon);
        }
    }

    private async Task BewaarBonStilAsync(BonRegel bon)
    {
        try
        {
            await BewaarBongegevensAsync(bon);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            Foutmelding = ex.Message;
        }
    }
}
