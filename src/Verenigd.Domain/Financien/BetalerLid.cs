using Verenigd.Domain.Gedeeld;

namespace Verenigd.Domain.Financien;

/// <summary>Koppeling van een lid aan zijn betaler, uitsluitend via het lidnummer (besluit 36).</summary>
public sealed class BetalerLid
{
    public BetalerLid(Lidnummer lidnummer, Guid betalerId)
    {
        Lidnummer = lidnummer;
        BetalerId = betalerId;
    }

    public Lidnummer Lidnummer { get; private set; }
    public Guid BetalerId { get; private set; }

    public void KoppelAan(Guid betalerId) => BetalerId = betalerId;
}
