using CommunityToolkit.Mvvm.ComponentModel;
using Verenigd.Application.Relaties;
using Verenigd.Domain.Financien.Relaties;

namespace Verenigd.App.ViewModels.Boekhouding;

/// <summary>Eén relatie; een nieuwe relatie heeft nog geen Id.</summary>
public sealed partial class RelatieRegel : ObservableObject
{
    private readonly string _origineel;

    public RelatieRegel(Relatie? relatie)
    {
        Id = relatie?.Id;
        Naam = relatie?.Naam ?? string.Empty;
        Contactpersoon = relatie?.Contactpersoon ?? string.Empty;
        Straat = relatie?.Straat ?? string.Empty;
        Postcode = relatie?.Postcode ?? string.Empty;
        Plaats = relatie?.Plaats ?? string.Empty;
        Email = relatie?.Email ?? string.Empty;
        Telefoon = relatie?.Telefoon ?? string.Empty;
        Iban = relatie?.Iban is { } iban ? Domain.Financien.Iban.Maak(iban).ToString() : string.Empty;
        IsDebiteur = relatie?.IsDebiteur ?? true;
        IsCrediteur = relatie?.IsCrediteur ?? false;
        Notitie = relatie?.Notitie ?? string.Empty;
        IsActief = relatie?.IsActief ?? true;
        _origineel = Momentopname();
    }

    public Guid? Id { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd), nameof(Fout))]
    public partial string Naam { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd))]
    public partial string Contactpersoon { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd))]
    public partial string Straat { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd))]
    public partial string Postcode { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd))]
    public partial string Plaats { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd), nameof(Fout))]
    public partial string Email { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd))]
    public partial string Telefoon { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd), nameof(Fout))]
    public partial string Iban { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd), nameof(Fout), nameof(Rol))]
    public partial bool IsDebiteur { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd), nameof(Fout), nameof(Rol))]
    public partial bool IsCrediteur { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd))]
    public partial string Notitie { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd))]
    public partial bool IsActief { get; set; }

    public string Rol => (IsDebiteur, IsCrediteur) switch
    {
        (true, true) => "Debiteur en crediteur",
        (false, true) => "Crediteur",
        _ => "Debiteur",
    };

    public bool IsGewijzigd => !string.Equals(Momentopname(), _origineel, StringComparison.Ordinal);

    public string? Fout
    {
        get
        {
            try
            {
                _ = new Relatie(Gegevens);
                return null;
            }
            catch (ArgumentException ex)
            {
                return Meldingen.Zonder(ex);
            }
        }
    }

    internal RelatieInvoer Invoer => new(Id, Gegevens, IsActief);

    private RelatieGegevens Gegevens => new(Naam, Contactpersoon, Straat, Postcode, Plaats, Email, Telefoon, Iban, IsDebiteur, IsCrediteur, Notitie);

    private string Momentopname() =>
        string.Join('|', Naam?.Trim(), Contactpersoon?.Trim(), Straat?.Trim(), Postcode?.Trim(), Plaats?.Trim(), Email?.Trim(), Telefoon?.Trim(),
            Iban?.Trim(), IsDebiteur, IsCrediteur, Notitie?.Trim(), IsActief);
}

internal static class Meldingen
{
    public static string Zonder(ArgumentException ex) =>
        ex.ParamName is null ? ex.Message : ex.Message.Replace($" (Parameter '{ex.ParamName}')", string.Empty, StringComparison.Ordinal);
}
