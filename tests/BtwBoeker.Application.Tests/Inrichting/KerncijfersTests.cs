using BtwBoeker.Application.Boekhouding;
using BtwBoeker.Application.Inrichting;
using BtwBoeker.Application.Tests.Boekhouding;
using BtwBoeker.Domain.Administraties;
using BtwBoeker.Domain.Financien;
using BtwBoeker.Domain.Financien.Boekhouding;

namespace BtwBoeker.Application.Tests.Inrichting;

/// <summary>Kerncijfers op Start per administratie.</summary>
public sealed class KerncijfersTests
{
    private static readonly DateOnly Vandaag = new(2026, 11, 3);
    private static readonly DateTime Nu = new(2026, 11, 3, 10, 0, 0, DateTimeKind.Utc);

    private static BoekhoudingTestdata MetUitgave(long centen)
    {
        var data = new BoekhoudingTestdata();
        var bank = data.Bankdagboek("BNK", "NL91ABNA0417164300");
        var uitgave = new Journaalpost(AdministratieSoort.Vereniging, bank, new DateOnly(2026, 10, 1), "Bouwmarkt");
        uitgave.Wijzig(bank, uitgave.Datum, "Bouwmarkt",
            [new RegelInvoer(data.Energie, null, new Bedrag(centen), Bedrag.Nul), new RegelInvoer(data.Bank, null, Bedrag.Nul, new Bedrag(centen))]);
        uitgave.Boek(1, Nu);
        data.Posten.Add(uitgave);
        return data;
    }

    [Fact]
    public void Per_bankrekening_het_saldo_en_het_resultaat_tot_vandaag()
    {
        var data = MetUitgave(2500);

        var cijfers = Kerncijfers.Bepaal(data.Stand, Vandaag);

        var bank = Assert.Single(cijfers.Bankrekeningen);
        Assert.Equal(Grootboek.Saldo(data.Stand, data.Bank, 2026), bank.Saldo.Centen);
        Assert.Null(bank.Datum); // nog geen afschrift ingelezen: het saldo volgens het grootboek
        Assert.Equal(-2500, cijfers.Resultaat.Centen); // een tekort
    }

    [Fact]
    public void Een_niet_inggerichte_administratie_heeft_geen_boekhouding()
    {
        var stand = new BoekhoudingStand(AdministratieSoort.Vereniging, [], [], [], [], []);

        var cijfers = Kerncijfers.Bepaal(stand, Vandaag);

        Assert.Empty(cijfers.Bankrekeningen);
        Assert.False(cijfers.HeeftBoekhouding);
    }
}
