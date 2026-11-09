using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace BtwBoeker.App.ViewModels;

/// <summary>
/// Besluit 234: een <see cref="ObservableCollection{T}"/> die in één keer vervangen kan worden. <c>Clear()</c> en daarna
/// regel voor regel <c>Add()</c> gaf de tabel op het scherm één melding per regel (bij 1.500 posten 1.500 keer
/// opnieuw opbouwen op de UI-thread); <see cref="Vervang"/> geeft één melding.
/// </summary>
public sealed class Verzameling<T> : ObservableCollection<T>
{
    public void Vervang(IEnumerable<T> nieuw)
    {
        ArgumentNullException.ThrowIfNull(nieuw);
        CheckReentrancy();
        Items.Clear();
        foreach (var item in nieuw)
        {
            Items.Add(item);
        }

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
