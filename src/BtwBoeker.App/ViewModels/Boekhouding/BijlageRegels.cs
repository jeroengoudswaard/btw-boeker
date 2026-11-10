using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BtwBoeker.App.Diensten;
using BtwBoeker.Domain.Financien;
using BtwBoeker.Domain.Financien.Bijlagen;
using BtwBoeker.Domain.Financien.Boekhouding;

namespace BtwBoeker.App.ViewModels.Boekhouding;

/// <summary>Eén bijlage in het blok <i>Bijlagen</i> van een post.</summary>
public sealed record BijlageRegel(Bijlage Bijlage)
{
    public Guid Id => Bijlage.Id;
    public string Bestandsnaam => Bijlage.Bestandsnaam;
    public string Soort => Bijlage.Soort.ToString().ToUpperInvariant();
    public string Grootte => Bijlagetekst.Grootte(Bijlage.Grootte);

    /// <summary>Segoe Fluent Icons: document of foto.</summary>
    public string Pictogram => Bijlage.Soort == BijlageSoort.Pdf ? "" : "";

    public string Toegankelijkenaam => $"{Bestandsnaam}, {Soort}, {Grootte}";
}

/// <summary>Een post waaraan een bon uit het postvak gekoppeld kan worden.</summary>
public sealed record PostKeuze(Journaalpost Post, bool IsVoorstel)
{
    public string Tekst =>
        $"{(IsVoorstel ? "✓ " : string.Empty)}{Post.Nummer} · {Weergave.Datum(Post.Datum)} · {Post.Omschrijving} · {Post.TotaalDebet}";

    public override string ToString() => Tekst;
}

/// <summary>
/// Eén bon in het postvak. Datum en bedrag zijn herkend of door de gebruiker ingevuld; bij elke
/// wijziging worden de voorgestelde posten opnieuw berekend.
/// </summary>
public sealed partial class BonRegel : ObservableObject
{
    private readonly Func<DateOnly?, Bedrag?, IReadOnlyList<PostKeuze>> _keuzes;

    public BonRegel(Bijlage bijlage, Func<DateOnly?, Bedrag?, IReadOnlyList<PostKeuze>> keuzes)
    {
        Bijlage = bijlage;
        _keuzes = keuzes;
        Datum = Weergave.AlsDateTime(bijlage.Datum);
        Bedragtekst = bijlage.Bedrag is { } bedrag ? Weergave.Invoer(bedrag) : string.Empty;
        Herbereken();
    }

    public Bijlage Bijlage { get; }
    public Guid Id => Bijlage.Id;
    public string Bestandsnaam => Bijlage.Bestandsnaam;
    public string Soort => Bijlage.Soort.ToString().ToUpperInvariant();
    public string Datumtekst => Weergave.Datum(Weergave.AlsDatum(Datum));

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Datumtekst), nameof(IsGewijzigd))]
    public partial DateTime? Datum { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Bedrag), nameof(Fout), nameof(IsGewijzigd), nameof(IsNegatief))]
    public partial string Bedragtekst { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<PostKeuze> Postkeuzes { get; private set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Voorsteltekst))]
    public partial PostKeuze? GekozenPost { get; set; }

    /// <summary><c>null</c> als het veld leeg of ongeldig is.</summary>
    /// <summary>Negatief bij een creditnota.</summary>
    public Bedrag? Bedrag => Application.Bijlagen.BonHerkenning.TryParseBedrag(Bedragtekst, out var bedrag) ? bedrag : null;

    /// <summary>Een creditnota: het bedrag wordt groen getoond.</summary>
    public bool IsNegatief => Bedrag is { Centen: < 0 };

    public string? Fout => Bedragtekst.Trim().Length > 0 && Bedrag is null ? "Vul een bedrag in zoals 23,45 (of -15,00 bij een creditnota)." : null;

    public bool IsGewijzigd => Weergave.AlsDatum(Datum) != Bijlage.Datum || Bedrag != Bijlage.Bedrag;

    public string Voorsteltekst => Postkeuzes.FirstOrDefault(k => k.IsVoorstel) is { } voorstel
        ? $"{voorstel.Post.Nummer} ({voorstel.Post.Omschrijving}) ✓"
        : "–";

    partial void OnDatumChanged(DateTime? value) => Herbereken();

    partial void OnBedragtekstChanged(string value) => Herbereken();

    private void Herbereken()
    {
        var vorige = GekozenPost;
        Postkeuzes = _keuzes(Weergave.AlsDatum(Datum), Bedrag);
        GekozenPost = vorige is { IsVoorstel: false } && Postkeuzes.FirstOrDefault(k => k.Post.Id == vorige.Post.Id) is { } zelfde
            ? zelfde
            : Postkeuzes.FirstOrDefault(k => k.IsVoorstel);
        OnPropertyChanged(nameof(Voorsteltekst));
    }
}

/// <summary>Het voorbeeldvenster van een bijlage: pagina's bekijken, zoomen en <i>Opslaan als…</i>.</summary>
public sealed partial class BijlageVoorbeeldViewModel(string bestandsnaam, BijlageSoort soort, byte[] inhoud, IDialoogService dialogen) : ObservableObject
{
    public const double MinimaleZoom = 0.25;
    public const double MaximaleZoom = 4;

    public string Bestandsnaam { get; } = bestandsnaam;
    public BijlageSoort Soort { get; } = soort;

    /// <summary>Alleen in het geheugen; komt nooit onversleuteld op schijf behalve via <see cref="OpslaanAlsCommand"/>.</summary>
    public byte[] Inhoud { get; } = inhoud;

    public bool IsPdf => Soort == BijlageSoort.Pdf;

    public string Titel => $"{Bestandsnaam} – Bijlage";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Zoomtekst))]
    [NotifyCanExecuteChangedFor(nameof(ZoomInCommand), nameof(ZoomUitCommand))]
    public partial double Zoom { get; set; } = 1;

    public string Zoomtekst => Zoom.ToString("P0", CultureInfo.CurrentCulture);

    [ObservableProperty]
    public partial string? Melding { get; private set; }

    [ObservableProperty]
    public partial string? Foutmelding { get; set; }

    [RelayCommand(CanExecute = nameof(KanZoomIn))]
    private void ZoomIn() => Zoom = Math.Min(MaximaleZoom, Zoom * 1.25);

    [RelayCommand(CanExecute = nameof(KanZoomUit))]
    private void ZoomUit() => Zoom = Math.Max(MinimaleZoom, Zoom / 1.25);

    [RelayCommand]
    private void ZoomHerstel() => Zoom = 1;

    /// <summary>Ctrl+S.</summary>
    [RelayCommand]
    private async Task OpslaanAlsAsync()
    {
        if (dialogen.KiesBijlageDoel(Bestandsnaam) is not { } doel)
        {
            return;
        }

        try
        {
            await File.WriteAllBytesAsync(doel, Inhoud);
            Melding = $"Opgeslagen als {Path.GetFileName(doel)}.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Foutmelding = "De bijlage kon niet worden opgeslagen: " + ex.Message;
        }
    }

    private bool KanZoomIn() => Zoom < MaximaleZoom;

    private bool KanZoomUit() => Zoom > MinimaleZoom;
}

internal static class Bijlagetekst
{
    public static string Grootte(long bytes) => bytes < 1024 * 1024
        ? string.Create(CultureInfo.CurrentCulture, $"{Math.Max(1, bytes / 1024)} kB")
        : string.Create(CultureInfo.CurrentCulture, $"{bytes / (1024d * 1024):0.0} MB");
}
