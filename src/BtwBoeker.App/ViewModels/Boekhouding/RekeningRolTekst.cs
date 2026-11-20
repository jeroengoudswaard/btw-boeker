using BtwBoeker.Domain.Financien.Boekhouding;

namespace BtwBoeker.App.ViewModels.Boekhouding;

/// <summary>Besluit 49: de naam van een rekeningrol in gewone woorden, voor de rolkeuze en het sjabloon.</summary>
public static class RekeningRolTekst
{
    public static string Naam(RekeningRol rol) => rol switch
    {
        RekeningRol.Geen => "Geen",
        RekeningRol.Reserve => "Reserve",
        RekeningRol.Kruisposten => "Tussenrekening overboekingen",
        RekeningRol.Kas => "Kas",
        RekeningRol.TeBetalenBtw => "Te betalen btw",
        RekeningRol.Voorbelasting => "Voorbelasting",
        RekeningRol.Debiteuren => "Debiteuren",
        RekeningRol.Crediteuren => "Crediteuren",
        _ => rol.ToString(),
    };
}
