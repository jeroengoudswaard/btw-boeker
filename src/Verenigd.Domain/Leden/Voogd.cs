namespace Verenigd.Domain.Leden;

/// <summary>Voogd van een lid; per lid opgeslagen, maximaal twee (besluit 31).</summary>
public sealed class Voogd
{
    private Voogd()
    {
    }

    internal Voogd(VoogdGegevens gegevens)
    {
        Volgnummer = gegevens.Volgnummer;
        Neem(gegevens);
    }

    public int Id { get; private set; }
    public int Volgnummer { get; private set; }
    public string? Naam { get; private set; }

    /// <summary>SOL3 leverde een plaatsvervanger zoals "Voogd 1".</summary>
    public bool NaamOnbekend { get; private set; }

    public string? Email { get; private set; }
    public string? Telefoon { get; private set; }
    public string? Mobiel { get; private set; }

    public VoogdGegevens Gegevens => new(Volgnummer, Naam, NaamOnbekend, Email, Telefoon, Mobiel);

    internal void Neem(VoogdGegevens gegevens)
    {
        Naam = gegevens.Naam;
        NaamOnbekend = gegevens.NaamOnbekend;
        Email = gegevens.Email;
        Telefoon = gegevens.Telefoon;
        Mobiel = gegevens.Mobiel;
    }
}
