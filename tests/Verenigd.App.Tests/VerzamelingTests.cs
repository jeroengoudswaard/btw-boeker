using System.Collections.Specialized;
using Verenigd.App.ViewModels;

namespace Verenigd.App.Tests;

/// <summary>Besluit 234: een lijst in één keer vervangen geeft één melding aan de tabel, niet één per regel.</summary>
public sealed class VerzamelingTests
{
    [Fact]
    public void Vervangen_geeft_een_enkele_reset_melding()
    {
        Verzameling<int> lijst = [1, 2, 3];
        var meldingen = new List<NotifyCollectionChangedAction>();
        var aantalGewijzigd = 0;
        lijst.CollectionChanged += (_, e) => meldingen.Add(e.Action);
        ((System.ComponentModel.INotifyPropertyChanged)lijst).PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(lijst.Count))
            {
                aantalGewijzigd++;
            }
        };

        lijst.Vervang(Enumerable.Range(10, 1500));

        Assert.Equal([NotifyCollectionChangedAction.Reset], meldingen);
        Assert.Equal(1, aantalGewijzigd);
        Assert.Equal(1500, lijst.Count);
        Assert.Equal(10, lijst[0]);
    }

    [Fact]
    public void Gewone_toevoegingen_werken_zoals_een_observablecollection()
    {
        Verzameling<string> lijst = [];
        var meldingen = 0;
        lijst.CollectionChanged += (_, _) => meldingen++;

        lijst.Add("a");
        lijst.Remove("a");

        Assert.Equal(2, meldingen);
    }
}
