using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BtwBoeker.App.ViewModels;

/// <summary>
/// De meldingsbalk rechts in de paginakop (UX-review stap 1, besluit 32: uit het moederproduct; vervangt de statusbalk).
/// ✓ gelukt, ⚠ waarschuwing, ✕ fout, met de tekst wat er gebeurde. Een ✓ verdwijnt na <see cref="Wachttijd"/>; ⚠ en ✕
/// blijven tot de gebruiker ze sluit of naar een andere pagina of een ander onderdeel gaat.
/// </summary>
public sealed partial class Meldingsbalk(TimeProvider tijd) : ObservableObject
{
    public static readonly TimeSpan Wachttijd = TimeSpan.FromSeconds(6);

    private ITimer? _timer;
    private int _ronde;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Teken))]
    public partial Statussoort Soort { get; private set; }

    [ObservableProperty]
    public partial string Tekst { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsZichtbaar { get; private set; }

    public string Teken => Soort switch
    {
        Statussoort.Gelukt => "✓",
        Statussoort.Waarschuwing => "⚠",
        Statussoort.Fout => "✕",
        _ => string.Empty,
    };

    /// <summary>Een gewone melding (<see cref="Statussoort.Info"/>) of een lege tekst haalt de balk weg.</summary>
    public void Toon(Statussoort soort, string tekst)
    {
        ArgumentNullException.ThrowIfNull(tekst);
        var ronde = ++_ronde;
        _timer?.Dispose();
        _timer = null;
        Soort = soort;
        Tekst = tekst;
        IsZichtbaar = soort != Statussoort.Info && tekst.Length > 0;
        if (IsZichtbaar && soort == Statussoort.Gelukt)
        {
            // De timer loopt buiten de UI-thread; de wijziging gaat terug naar de thread van de melding.
            var context = SynchronizationContext.Current;
            _timer = tijd.CreateTimer(_ => Verloop(context, ronde), null, Wachttijd, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>Bij een andere pagina horen een waarschuwing en een fout niet meer bij wat er staat; een ✓ mag uitlopen.</summary>
    public void AnderePagina()
    {
        if (IsZichtbaar && Soort is Statussoort.Waarschuwing or Statussoort.Fout)
        {
            Sluit();
        }
    }

    [RelayCommand]
    private void Sluit()
    {
        _ronde++;
        _timer?.Dispose();
        _timer = null;
        IsZichtbaar = false;
    }

    private void Verloop(SynchronizationContext? context, int ronde)
    {
        if (context is null || ReferenceEquals(context, SynchronizationContext.Current))
        {
            Verberg();
        }
        else
        {
            context.Post(_ => Verberg(), null);
        }

        void Verberg()
        {
            if (ronde == _ronde)
            {
                Sluit();
            }
        }
    }
}
