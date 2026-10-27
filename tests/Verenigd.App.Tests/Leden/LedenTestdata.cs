using Verenigd.Application.Import;
using Verenigd.Domain.Gedeeld;
using Verenigd.Domain.Leden;

namespace Verenigd.App.Tests.Leden;

/// <summary>Vult een <see cref="InMemoryLedenOpslag"/> met verzonnen leden via de echte importlogica.</summary>
internal static class LedenTestdata
{
    public static readonly DateOnly Vandaag = new(2026, 10, 27);

    public static ImportLid Lid(
        string lidnummer, string voornaam, string achternaam, string groep = "Welpen", Lidtype lidtype = Lidtype.Jeugdlid,
        string? email = null, string rol = "Lid") =>
        new(
            Lidnummer.Parse(lidnummer),
            new LidGegevens(voornaam, null, achternaam, new DateOnly(2015, 3, 1), "Vrouw", email ?? $"{voornaam.ToLowerInvariant()}@example.org",
                "+31612345678", null, "Actief", "Dorpsstraat", "1", null, "1234 AB", "Ergens", "Nederland"),
            [new VoogdGegevens(1, null, true, "voogd@example.org", null, "+31612345678")],
            [
                new FunctieGegevens(groep, rol, lidtype, new DateOnly(2025, 9, 19), null, "Actief"),
                new FunctieGegevens("Vrijwilligers (Migratie)", "Lid", lidtype, new DateOnly(2018, 11, 7), null, "Actief"),
            ],
            null,
            null);

    public static async Task VulAsync(InMemoryLedenOpslag opslag, params ImportLid[] leden)
    {
        var import = new Sol3Import(leden, []);
        var stand = await opslag.LaadAsync();
        var keuzes = ImportKeuzes.Standaard(ImportPlanner.Plan(stand, import, Vandaag));
        await opslag.PasToeAsync(s => ImportToepasser.PasToe(s, import, keuzes, Vandaag, DateTime.UtcNow));
    }
}
