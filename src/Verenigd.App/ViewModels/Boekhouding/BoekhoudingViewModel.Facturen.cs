using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Verenigd.Application.Facturen;
using Verenigd.Domain.Financien.Facturen;

namespace Verenigd.App.ViewModels.Boekhouding;

/// <summary>Subtabbladen Facturen en Relaties (stap 4c-2, besluiten 105–112).</summary>
public sealed partial class BoekhoudingViewModel
{
    private List<FactuurRegel> _alleFacturen = [];
    private bool _factuurselectieWordtGezet;

    private FactuurDienst Factuurdienst => field ??= new FactuurDienst(_sessie, _tijd);

    public Verzameling<FactuurRegel> Facturen { get; } = [];

    public Verzameling<RelatieRegel> Relaties { get; } = [];

    public IReadOnlyList<Factuurfilter> Factuurfilters { get; } = Enum.GetValues<Factuurfilter>();

    [ObservableProperty]
    public partial Factuurfilter Factuurfilter { get; set; }

    [ObservableProperty]
    public partial FactuurRegel? GeselecteerdeFactuur { get; set; }

    [ObservableProperty]
    public partial FactuurEditor? FactuurEditor { get; private set; }

    [ObservableProperty]
    public partial RelatieRegel? GeselecteerdeRelatie { get; set; }

    public string Factuuraantaltekst
    {
        get
        {
            var open = _alleFacturen.Where(f => f.Stand.IsOpen).ToList();
            return $"{open.Count} open · {open.Count(f => f.IsVervallen)} vervallen · {_alleFacturen.Count(f => f.IsConcept)} concept";
        }
    }

    private bool FacturenGewijzigd => FactuurEditor?.IsGewijzigd == true || Relaties.Any(r => r.IsGewijzigd);

    private bool FacturenGeldig =>
        (FactuurEditor is not { IsGewijzigd: true } || FactuurEditor.Fouten.Count == 0) && Relaties.Where(r => r.IsGewijzigd).All(r => r.Fout is null);

    private void LaadFacturen(Guid? factuurSelectie)
    {
        KoppelRelaties(false);
        var gekozenRelatie = GeselecteerdeRelatie?.Id;
        Relaties.Vervang((_stand!.Relaties).Select(relatie => new RelatieRegel(relatie)));

        KoppelRelaties(true);
        GeselecteerdeRelatie = Relaties.FirstOrDefault(r => r.Id == gekozenRelatie);
        _alleFacturen = [.. Factuuroverzicht.Bereken(_stand, Vandaag).Select(s => new FactuurRegel(s))];
        PasFactuurfilterToe(factuurSelectie ?? GeselecteerdeFactuur?.Id);
        OnPropertyChanged(nameof(Factuuraantaltekst));
    }


    private void PasFactuurfilterToe(Guid? selectie)
    {
        var zichtbaar = _alleFacturen.Where(f => Factuurfilter switch
        {
            Factuurfilter.Open => f.Stand.IsOpen,
            Factuurfilter.Vervallen => f.IsVervallen,
            Factuurfilter.Concept => f.IsConcept,
            _ => true,
        });
        _factuurselectieWordtGezet = true;
        try
        {
            Facturen.Vervang(zichtbaar);

            GeselecteerdeFactuur = Facturen.FirstOrDefault(f => f.Id == selectie);
        }
        finally
        {
            _factuurselectieWordtGezet = false;
        }

        if (FactuurEditor is not { IsGewijzigd: true })
        {
            ToonFactuurEditor(GeselecteerdeFactuur is { IsConcept: true } gekozen ? gekozen.Factuur : null, nieuw: false);
        }

        FactuurCommandsVernieuwen();
    }

    partial void OnFactuurfilterChanged(Factuurfilter value)
    {
        if (_stand is not null)
        {
            PasFactuurfilterToe(GeselecteerdeFactuur?.Id);
        }
    }

    partial void OnGeselecteerdeFactuurChanged(FactuurRegel? value)
    {
        FactuurCommandsVernieuwen();
        if (_factuurselectieWordtGezet || value is null || FactuurEditor?.Id == value.Id)
        {
            return;
        }

        if (FactuurEditor is not { IsGewijzigd: true }
            || _dialogen.Bevestig("Factuur wijzigen", "De huidige factuur is niet opgeslagen. Wilt u de wijzigingen verwerpen?"))
        {
            ToonFactuurEditor(value.IsConcept ? value.Factuur : null, nieuw: false);
        }
    }

    private void ToonFactuurEditor(Verkoopfactuur? factuur, bool nieuw)
    {
        if (FactuurEditor is not null)
        {
            FactuurEditor.PropertyChanged -= FactuurEditorGewijzigd;
        }

        FactuurEditor = _stand is null || (factuur is null && !nieuw)
            ? null
            : new FactuurEditor(factuur, _stand.Relaties, _stand.Rekeningen, Vandaag, _stand.Factuurinstellingen?.Termijn ?? 14);
        if (FactuurEditor is not null)
        {
            FactuurEditor.PropertyChanged += FactuurEditorGewijzigd;
        }

        CommandsVernieuwen();
    }

    private void FactuurEditorGewijzigd(object? sender, PropertyChangedEventArgs e) => CommandsVernieuwen();

    /// <summary>Opslaan via Ctrl+S, samen met de rest van het tabblad.</summary>
    private async Task<Guid?> BewaarFacturenAsync()
    {
        foreach (var relatie in Relaties.Where(r => r.IsGewijzigd).ToList())
        {
            await Factuurdienst.BewaarRelatieAsync(relatie.Invoer);
        }

        if (FactuurEditor is { IsGewijzigd: true } editor)
        {
            var id = await Factuurdienst.BewaarConceptAsync(Soort, editor.Invoer);
            ToonFactuurEditor(null, nieuw: false);
            Factuurfilter = Factuurfilter.Concept;
            return id;
        }

        return null;
    }

    // ---- Relaties ---------------------------------------------------------------------------

    [RelayCommand]
    private void NieuweRelatie()
    {
        var regel = new RelatieRegel(null);
        regel.PropertyChanged += RelatieGewijzigd;
        Relaties.Add(regel);
        GeselecteerdeRelatie = regel;
        Subtab = BoekhoudingSubtab.Relaties;
        CommandsVernieuwen();
    }

    private void KoppelRelaties(bool koppel)
    {
        foreach (var regel in Relaties)
        {
            regel.PropertyChanged -= RelatieGewijzigd;
            if (koppel)
            {
                regel.PropertyChanged += RelatieGewijzigd;
            }
        }
    }

    private void RelatieGewijzigd(object? sender, PropertyChangedEventArgs e) => CommandsVernieuwen();

    // ---- Facturen ---------------------------------------------------------------------------

    /// <summary>Alleen via de knop; Ctrl+N blijft "Nieuwe post" (besluit 111).</summary>
    [RelayCommand]
    private void NieuweFactuur()
    {
        if (_stand is null
            || (FactuurEditor is { IsGewijzigd: true }
                && !_dialogen.Bevestig("Nieuwe factuur", "De huidige factuur is niet opgeslagen. Wilt u de wijzigingen verwerpen?")))
        {
            return;
        }

        Subtab = BoekhoudingSubtab.Facturen;
        _factuurselectieWordtGezet = true;
        GeselecteerdeFactuur = null;
        _factuurselectieWordtGezet = false;
        ToonFactuurEditor(null, nieuw: true);
    }

    private bool KanDefinitiefMaken() =>
        !IsBezig && FactuurEditor is { } editor && editor.Fouten.Count == 0 && (editor.Id is not null || editor.IsGewijzigd);

    /// <summary>Kent het nummer toe en boekt de factuur in het verkoopdagboek; daarna is hij onveranderlijk.</summary>
    [RelayCommand(CanExecute = nameof(KanDefinitiefMaken))]
    private async Task MaakFactuurDefinitiefAsync()
    {
        if (FactuurEditor is not { } editor
            || !_dialogen.Bevestig("Definitief maken",
                $"Wilt u deze {(editor.IsCreditfactuur ? "creditfactuur" : "factuur")} van {editor.Totaaltekst} definitief maken? Het nummer wordt toegekend "
                + "en de factuur wordt geboekt; daarna kan hij alleen nog met een creditfactuur worden gecorrigeerd."))
        {
            return;
        }

        Guid? definitief = null;
        string? factuurnummer = null;
        await VoerUitAsync(async () =>
        {
            var id = editor.IsGewijzigd ? await Factuurdienst.BewaarConceptAsync(Soort, editor.Invoer) : editor.Id!.Value;
            var nummer = await Factuurdienst.MaakDefinitiefAsync(Soort, id);
            Melding = $"{(editor.IsCreditfactuur ? "Creditfactuur" : "Factuur")} {nummer} is definitief en geboekt.";
            ToonFactuurEditor(null, nieuw: false);
            Factuurfilter = editor.IsCreditfactuur ? Factuurfilter.Alle : Factuurfilter.Open;
            await LaadAsync(Editor?.Id);
            PasFactuurfilterToe(id);
            (definitief, factuurnummer) = (id, nummer);
        }, null);

        // Besluit 207: na Definitief maken aanbieden om de factuur met de PDF te mailen.
        if (definitief is { } factuurId && !editor.IsCreditfactuur && EmailAanbieden is not null
            && _alleFacturen.FirstOrDefault(f => f.Id == factuurId) is { } regel
            && _dialogen.Bevestig("Factuur mailen", $"Wilt u factuur {factuurnummer} per e-mail versturen, met de PDF als bijlage?"))
        {
            await MailFactuurAsync(regel, Domain.Communicatie.Emailsoort.Factuur);
        }
    }

    private bool KanFactuurMailen() => !IsBezig && GeselecteerdeFactuur is { IsConcept: false, Factuur.IsCreditfactuur: false };

    /// <summary>De gekozen factuur (opnieuw) mailen met de PDF als bijlage.</summary>
    [RelayCommand(CanExecute = nameof(KanFactuurMailen))]
    private Task FactuurMailenAsync() => MailFactuurAsync(GeselecteerdeFactuur!, Domain.Communicatie.Emailsoort.Factuur);

    private bool KanHerinneren() => !IsBezig && GeselecteerdeFactuur is { IsVervallen: true, Stand.IsOpen: true };

    /// <summary>Besluit 207: een herinnering bij een vervallen factuur, ook met de PDF.</summary>
    [RelayCommand(CanExecute = nameof(KanHerinneren))]
    private Task HerinneringSturenAsync() => MailFactuurAsync(GeselecteerdeFactuur!, Domain.Communicatie.Emailsoort.Herinnering);

    private Task MailFactuurAsync(FactuurRegel regel, Domain.Communicatie.Emailsoort soort)
    {
        if (regel.Stand.Relatie is not { } relatie)
        {
            Foutmelding = "Deze factuur heeft geen relatie.";
            return Task.CompletedTask;
        }

        var pdf = _hulpmiddelen.Pdf.Maak(regel.Factuur, _stand?.Factuurinstellingen ?? new Factuurinstellingen(Soort));
        if (Application.Email.Berichten.Factuur(regel.Factuur, relatie, Administratie?.Naam ?? string.Empty, pdf) is not { } ontvanger)
        {
            Foutmelding = $"Bij {relatie.Naam} staat geen geldig e-mailadres. Vul het in bij Relaties.";
            return Task.CompletedTask;
        }

        var aanleiding = soort == Domain.Communicatie.Emailsoort.Herinnering ? $"Herinnering factuur {regel.Nummer}" : $"Factuur {regel.Nummer}";
        return BiedEmailAanAsync(new Email.EmailVoorstel(soort, [ontvanger], $"{aanleiding} aan {relatie.Naam}", Soort));
    }

    private bool KanConceptVerwijderen() => !IsBezig && GeselecteerdeFactuur?.IsConcept == true;

    [RelayCommand(CanExecute = nameof(KanConceptVerwijderen))]
    private async Task VerwijderFactuurconceptAsync()
    {
        if (GeselecteerdeFactuur is not { IsConcept: true } factuur
            || !_dialogen.Bevestig("Concept verwijderen", $"Wilt u de conceptfactuur voor {factuur.Relatie} ({factuur.Totaal}) verwijderen?"))
        {
            return;
        }

        await VoerUitAsync(async () =>
        {
            await Factuurdienst.VerwijderConceptAsync(Soort, factuur.Id);
            ToonFactuurEditor(null, nieuw: false);
            await LaadAsync(Editor?.Id);
        }, "Het concept kon niet worden verwijderd.");
    }

    private bool KanPdfMaken() => !IsBezig && GeselecteerdeFactuur is not null && FactuurEditor is not { IsGewijzigd: true };

    /// <summary>De factuur als PDF met EPC-QR-code (besluiten 95 en 110).</summary>
    [RelayCommand(CanExecute = nameof(KanPdfMaken))]
    private Task FactuurPdfAsync()
    {
        if (GeselecteerdeFactuur is not { } factuur || _dialogen.KiesPdfDoel($"{factuur.Nummer}.pdf") is not { } doel)
        {
            return Task.CompletedTask;
        }

        return VoerUitAsync(async () =>
        {
            var instellingen = _stand?.Factuurinstellingen ?? new Factuurinstellingen(Soort);
            await File.WriteAllBytesAsync(doel, _hulpmiddelen.Pdf.Maak(factuur.Factuur, instellingen));
            Melding = $"Opgeslagen als {Path.GetFileName(doel)}."
                + (instellingen.Naam is null ? " Vul bij Instellingen › Facturen de gegevens van de vereniging in." : string.Empty);
        }, null);
    }

    private bool KanCrediteren() => !IsBezig && GeselecteerdeFactuur?.KanCrediteren == true;

    [RelayCommand(CanExecute = nameof(KanCrediteren))]
    private async Task CreditfactuurAsync()
    {
        if (GeselecteerdeFactuur is not { KanCrediteren: true } factuur
            || !_dialogen.Bevestig("Creditfactuur", $"Wilt u een creditfactuur maken voor {factuur.Nummer} ({factuur.Totaal})? U kunt het concept eerst controleren."))
        {
            return;
        }

        await VoerUitAsync(async () =>
        {
            var datum = Vandaag < factuur.Factuur.Factuurdatum ? factuur.Factuur.Factuurdatum : Vandaag;
            var id = await Factuurdienst.MaakCreditfactuurAsync(Soort, factuur.Id, datum);
            Factuurfilter = Factuurfilter.Concept;
            await LaadAsync(Editor?.Id);
            PasFactuurfilterToe(id);
        }, null);
    }

    private void FactuurCommandsVernieuwen()
    {
        MaakFactuurDefinitiefCommand.NotifyCanExecuteChanged();
        VerwijderFactuurconceptCommand.NotifyCanExecuteChanged();
        FactuurPdfCommand.NotifyCanExecuteChanged();
        CreditfactuurCommand.NotifyCanExecuteChanged();
        FactuurMailenCommand.NotifyCanExecuteChanged();
        HerinneringSturenCommand.NotifyCanExecuteChanged();
    }
}
