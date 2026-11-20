using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BtwBoeker.App.ViewModels.Boekhouding;

/// <summary>
/// UX-review stap 4 (besluit 40): alleen de acties die nu kunnen. Kan iets nog niet, dan staat er waarom, met één knop naar
/// wat eerst moet; een uitgeschakelde knop zonder uitleg komt niet meer voor.
/// </summary>
public sealed partial class BoekhoudingViewModel
{
    /// <summary>Het boekjaar in het detail van Boekjaren.</summary>
    [ObservableProperty]
    public partial JaarRegel? GeselecteerdJaar { get; set; }

    partial void OnGeselecteerdJaarChanged(JaarRegel? value) => MeldDetail();

    /// <summary>Keuze 3A: <i>Volgend boekjaar openen</i> pas vanaf december van het laatste jaar, of als dat jaar voorbij is.</summary>
    public bool IsVolgendBoekjaarZichtbaar =>
        Boekjaren.Count == 0 || Vandaag.Year > Boekjaren[^1] || (Vandaag.Year == Boekjaren[^1] && Vandaag.Month == 12);

    public string VolgendBoekjaarTekst => string.Create(CultureInfo.CurrentCulture,
        $"Boekjaar {(Boekjaren.Count == 0 ? Vandaag.Year : Boekjaren[^1] + 1)} openen");

    /// <summary>Keuze 1A: <i>Toewijzen</i> verschijnt pas als er een rekening is gekozen.</summary>
    public bool IsToewijzenZichtbaar => GeselecteerdeMutatie is { IsTeVerwerken: true } && ToewijsRekening is not null;

    /// <summary>Een verwerkte transactie heeft een post; de knop <i>Naar de post</i> opent die in het journaal.</summary>
    public bool IsNaarPostZichtbaar => GeselecteerdeMutatie?.Mutatie.PostId is { } id && _stand?.Posten.Any(p => p.Id == id) == true;

    /// <summary>De grootboekkaart toont zonder rekening alleen de keuze en een uitleg.</summary>
    public bool IsKaartRekeningGekozen => KaartRekening is not null;

    /// <summary>De knop naar wat eerst moet voordat een boekjaar kan worden afgesloten.</summary>
    [RelayCommand]
    private void Vervolg(JaarRegel? regel)
    {
        switch (regel?.Vervolg)
        {
            case Vervolgactie.NaarJournaal:
                NaarJournaal(regel.Jaar);
                break;
            case Vervolgactie.NaarEerderJaar:
                GeselecteerdJaar = Jaren.FirstOrDefault(j => j.Jaar == regel.Controle?.EerderJaar);
                break;
            case Vervolgactie.NaarRekeningschema:
                Subtab = BoekhoudingSubtab.Rekeningschema;
                break;
        }
    }

    /// <summary>Besluit 50: een btw-rekening aanwijzen kan alleen in het rekeningschema.</summary>
    [RelayCommand]
    private void NaarRekeningschema() => Subtab = BoekhoudingSubtab.Rekeningschema;

    /// <summary>Naar het journaal, eventueel van een bepaald jaar (vanuit Boekjaren of Rapporten).</summary>
    [RelayCommand]
    private void NaarJournaal(int? jaar)
    {
        if (jaar is { } j && Boekjaren.Contains(j))
        {
            Jaar = j;
        }

        Subtab = BoekhoudingSubtab.Journaal;
    }

    /// <summary>Van een verwerkte banktransactie naar de post in het journaal.</summary>
    [RelayCommand]
    private void NaarPost()
    {
        if (GeselecteerdeMutatie?.Mutatie.PostId is not { } id || _stand?.Posten.FirstOrDefault(p => p.Id == id) is not { } post)
        {
            return;
        }

        if (!BevestigVerwerpen())
        {
            return;
        }

        Subtab = BoekhoudingSubtab.Journaal;
        if (Boekjaren.Contains(post.Datum.Year))
        {
            Jaar = post.Datum.Year;
        }

        if (Posten.All(p => p.Id != id))
        {
            // Een filter mag de post niet verbergen.
            Zoektekst = string.Empty;
            AlleenZonderBon = false;
        }

        GeselecteerdePost = Posten.FirstOrDefault(p => p.Id == id);
    }

    private void MeldStatus()
    {
        OnPropertyChanged(nameof(IsToewijzenZichtbaar));
        OnPropertyChanged(nameof(IsNaarPostZichtbaar));
    }
}
