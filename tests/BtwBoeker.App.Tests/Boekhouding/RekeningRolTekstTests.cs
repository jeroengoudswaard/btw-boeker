using BtwBoeker.App.ViewModels.Boekhouding;
using BtwBoeker.Domain.Financien.Boekhouding;

namespace BtwBoeker.App.Tests.Boekhouding;

/// <summary>Besluit 49: de rolkeuze in het rekeningschema toont gewone woorden, geen namen uit de code.</summary>
public sealed class RekeningRolTekstTests
{
    [Theory]
    [InlineData(RekeningRol.Geen, "Geen")]
    [InlineData(RekeningRol.Reserve, "Reserve")]
    [InlineData(RekeningRol.Kruisposten, "Tussenrekening overboekingen")]
    [InlineData(RekeningRol.Kas, "Kas")]
    [InlineData(RekeningRol.TeBetalenBtw, "Te betalen btw")]
    [InlineData(RekeningRol.Voorbelasting, "Voorbelasting")]
    [InlineData(RekeningRol.Debiteuren, "Debiteuren")]
    [InlineData(RekeningRol.Crediteuren, "Crediteuren")]
    public void Elke_rol_heeft_een_naam(RekeningRol rol, string naam) => Assert.Equal(naam, RekeningRolTekst.Naam(rol));

    [Fact]
    public void Alle_rollen_hebben_een_eigen_naam() =>
        Assert.Equal(Enum.GetValues<RekeningRol>().Length, Enum.GetValues<RekeningRol>().Select(RekeningRolTekst.Naam).Distinct().Count());
}
