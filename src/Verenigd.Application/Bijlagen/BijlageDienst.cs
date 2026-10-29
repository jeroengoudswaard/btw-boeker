using Verenigd.Application.Boekhouding;
using Verenigd.Application.Database;
using Verenigd.Domain.Administraties;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Financien.Bijlagen;

namespace Verenigd.Application.Bijlagen;

public sealed record BijlageBestand(string Naam, byte[] Inhoud);

public sealed record OvergeslagenBestand(string Bestandsnaam, string Reden);

public sealed record BijlagenToegevoegd(IReadOnlyList<Bijlage> Toegevoegd, IReadOnlyList<OvergeslagenBestand> Overgeslagen);

/// <summary>
/// Bijlagen bij journaalposten en het postvak bonnen (besluiten 83–86, 96, 97 en 135–137). De inhoud staat versleuteld in
/// de database en komt nooit onversleuteld op schijf, behalve bij <i>Opslaan als…</i> in de app.
/// </summary>
public sealed class BijlageDienst(IDatabaseSessie sessie, IPdfTekstLezer lezer, TimeProvider tijd)
{
    /// <summary>
    /// Voegt bestanden toe aan een post, of aan het postvak als <paramref name="postId"/> leeg is. Van een PDF worden datum
    /// en bedrag gelezen. Ongeldige bestanden en bestanden die er al zijn worden overgeslagen, met de reden.
    /// </summary>
    /// <exception cref="InvalidOperationException">De post bestaat niet.</exception>
    public async Task<BijlagenToegevoegd> VoegToeAsync(
        AdministratieSoort administratie, IReadOnlyList<BijlageBestand> bestanden, Guid? postId, CancellationToken annulering = default)
    {
        ArgumentNullException.ThrowIfNull(bestanden);
        var nieuw = new List<(int Volgorde, Bijlage Bijlage, byte[] Inhoud)>();
        var overgeslagen = new List<(int Volgorde, OvergeslagenBestand Bestand)>();
        foreach (var (volgorde, bestand) in bestanden.Index())
        {
            try
            {
                var bijlage = new Bijlage(administratie, bestand.Naam, bestand.Inhoud, tijd.GetUtcNow().UtcDateTime);
                if (bijlage.Soort == BijlageSoort.Pdf)
                {
                    var gegevens = BonHerkenning.Herken(LeesVeilig(bestand.Inhoud));
                    bijlage.ZetGegevens(gegevens.Datum, gegevens.Bedrag);
                }

                nieuw.Add((volgorde, bijlage, bestand.Inhoud));
            }
            catch (ArgumentException ex)
            {
                overgeslagen.Add((volgorde, new OvergeslagenBestand(bestand.Naam, ex.Message)));
            }
        }

        return await sessie.Boekhouding.PasToeAsync(administratie, stand =>
        {
            if (postId is { } id && stand.Posten.All(p => p.Id != id))
            {
                throw new InvalidOperationException("De post bestaat niet (meer).");
            }

            var toegevoegd = new List<Bijlage>();
            var dubbel = new List<(int Volgorde, OvergeslagenBestand Bestand)>();
            foreach (var (volgorde, bijlage, inhoud) in nieuw)
            {
                if (stand.Bijlagen.Concat(toegevoegd).FirstOrDefault(b => b.Controlecode == bijlage.Controlecode) is { } bestaand)
                {
                    dubbel.Add((volgorde, new OvergeslagenBestand(bijlage.Bestandsnaam, Waar(stand, bestaand))));
                    continue;
                }

                if (postId is { } post)
                {
                    bijlage.KoppelAan(post);
                }

                toegevoegd.Add(bijlage);
            }

            var wijziging = new BoekhoudingWijziging<BijlagenToegevoegd>(
                new BijlagenToegevoegd(toegevoegd, [.. overgeslagen.Concat(dubbel).OrderBy(o => o.Volgorde).Select(o => o.Bestand)]));
            foreach (var (_, bijlage, inhoud) in nieuw.Where(n => toegevoegd.Contains(n.Bijlage)))
            {
                wijziging.Toevoegen.Add(bijlage);
                wijziging.Toevoegen.Add(new BijlageInhoud(bijlage.Id, inhoud));
            }

            return wijziging;
        }, annulering).ConfigureAwait(false);
    }

    /// <summary>Ook bij een geboekte post (besluit 136).</summary>
    /// <exception cref="InvalidOperationException">De bijlage of de post bestaat niet.</exception>
    public Task KoppelAsync(AdministratieSoort administratie, Guid bijlageId, Guid postId, CancellationToken annulering = default) =>
        Wijzig(administratie, bijlageId, (stand, bijlage) =>
        {
            if (stand.Posten.All(p => p.Id != postId))
            {
                throw new InvalidOperationException("De post bestaat niet (meer).");
            }

            bijlage.KoppelAan(postId);
        }, annulering);

    /// <summary>Terug naar het postvak.</summary>
    public Task OntkoppelAsync(AdministratieSoort administratie, Guid bijlageId, CancellationToken annulering = default) =>
        Wijzig(administratie, bijlageId, (_, bijlage) => bijlage.Ontkoppel(), annulering);

    public Task WijzigGegevensAsync(
        AdministratieSoort administratie, Guid bijlageId, DateOnly? datum, Bedrag? bedrag, CancellationToken annulering = default) =>
        Wijzig(administratie, bijlageId, (_, bijlage) => bijlage.ZetGegevens(datum, bedrag), annulering);

    /// <summary>Ook bij een geboekte post (besluit 136); de inhoud wordt mee verwijderd.</summary>
    public Task VerwijderAsync(AdministratieSoort administratie, Guid bijlageId, CancellationToken annulering = default) =>
        sessie.Boekhouding.PasToeAsync(administratie, stand =>
        {
            var wijziging = new BoekhoudingWijziging<bool>(true);
            wijziging.Verwijderen.Add(Zoek(stand, bijlageId));
            return wijziging;
        }, annulering);

    /// <exception cref="InvalidOperationException">De bijlage bestaat niet.</exception>
    public async Task<byte[]> LaadInhoudAsync(AdministratieSoort administratie, Guid bijlageId, CancellationToken annulering = default)
    {
        var stand = await sessie.Boekhouding.LaadAsync(administratie, annulering).ConfigureAwait(false);
        Zoek(stand, bijlageId);
        return await sessie.Boekhouding.LaadBijlageInhoudAsync(bijlageId, annulering).ConfigureAwait(false)
            ?? throw new InvalidOperationException("De inhoud van de bijlage ontbreekt.");
    }

    private Task<bool> Wijzig(AdministratieSoort administratie, Guid bijlageId, Action<BoekhoudingStand, Bijlage> actie, CancellationToken annulering) =>
        sessie.Boekhouding.PasToeAsync(administratie, stand =>
        {
            actie(stand, Zoek(stand, bijlageId));
            return new BoekhoudingWijziging<bool>(true);
        }, annulering);

    private static Bijlage Zoek(BoekhoudingStand stand, Guid bijlageId) =>
        stand.Bijlagen.FirstOrDefault(b => b.Id == bijlageId) ?? throw new InvalidOperationException("De bijlage bestaat niet (meer).");

    private static string Waar(BoekhoudingStand stand, Bijlage bestaand) =>
        bestaand.PostId is { } id && stand.Posten.FirstOrDefault(p => p.Id == id) is { } post
            ? $"Dit bestand is al toegevoegd bij post {post.Nummer}."
            : "Dit bestand is al toegevoegd (in het postvak).";

    private string? LeesVeilig(byte[] pdf)
    {
        try
        {
            return lezer.LeesTekst(pdf);
        }
#pragma warning disable CA1031 // Een onleesbare PDF mag het toevoegen nooit tegenhouden; de gebruiker vult dan zelf in.
        catch (Exception)
#pragma warning restore CA1031
        {
            return null;
        }
    }
}
