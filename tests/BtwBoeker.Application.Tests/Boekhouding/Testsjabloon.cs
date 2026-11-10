using BtwBoeker.Application.Boekhouding;
using BtwBoeker.Domain.Administraties;
using BtwBoeker.Domain.Financien.Boekhouding;

namespace BtwBoeker.Application.Tests.Boekhouding;

/// <summary>
/// Een klein, verzonnen rekeningschema-sjabloon voor tests (geen echte gegevens). Gedeeld met de App-tests via een
/// gekoppeld bestand.
/// </summary>
internal static class Testsjabloon
{
    public const string Naam = "Testschema verhuur";

    public static Rekeningsjabloon Verhuur(AdministratieSoort? administratie = AdministratieSoort.Vereniging) => new(Naam, administratie,
        [new("Vermogen"), new("Liquide middelen"), new("Kosten"), new("Opbrengsten")],
        [
            new(600, "Eigen vermogen", "Vermogen", RekeningSoort.Balans, RekeningRol.Reserve),
            new(1000, "Kas", "Liquide middelen", RekeningSoort.Balans, RekeningRol.Kas),
            new(1100, "Bank", "Liquide middelen", RekeningSoort.Balans),
            new(4100, "Onderhoud", "Kosten", RekeningSoort.Resultaat),
            new(4120, "Energie", "Kosten", RekeningSoort.Resultaat),
            new(8000, "Verhuur", "Opbrengsten", RekeningSoort.Resultaat, Kant: ResultaatKant.Baten),
        ]);
}
