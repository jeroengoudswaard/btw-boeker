using Microsoft.EntityFrameworkCore;
using BtwBoeker.Domain.Administraties;
using BtwBoeker.Domain.Financien;
using BtwBoeker.Domain.Financien.Boekhouding;
using BtwBoeker.Infrastructure.Database.Financien;

namespace BtwBoeker.Infrastructure.Database;

public sealed class BtwBoekerDbContext(DbContextOptions<BtwBoekerDbContext> options) : DbContext(options)
{
    /// <summary>Na elk geslaagd opslaan verhoogd, zodat momentopnamen weten dat ze verouderd zijn.</summary>
    internal Wijzigingen? Wijzigingen { get; init; }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        var aantal = base.SaveChanges(acceptAllChangesOnSuccess);
        Wijzigingen?.Verhoog();
        return aantal;
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        var aantal = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken).ConfigureAwait(false);
        Wijzigingen?.Verhoog();
        return aantal;
    }

    public DbSet<Administratie> Administraties => Set<Administratie>();

    public DbSet<Rubriek> Rubrieken => Set<Rubriek>();
    public DbSet<Grootboekrekening> Grootboekrekeningen => Set<Grootboekrekening>();
    public DbSet<Dagboek> Dagboeken => Set<Dagboek>();
    public DbSet<Boekjaar> Boekjaren => Set<Boekjaar>();
    public DbSet<Journaalpost> Journaalposten => Set<Journaalpost>();
    public DbSet<Domain.Financien.Bank.Bankafschrift> Bankafschriften => Set<Domain.Financien.Bank.Bankafschrift>();
    public DbSet<Domain.Financien.Bank.Bankmutatie> Bankmutaties => Set<Domain.Financien.Bank.Bankmutatie>();
    public DbSet<Domain.Financien.Bank.Boekingsregel> Boekingsregels => Set<Domain.Financien.Bank.Boekingsregel>();
    public DbSet<Domain.Financien.Relaties.Relatie> Relaties => Set<Domain.Financien.Relaties.Relatie>();
    public DbSet<Domain.Financien.Bijlagen.Bijlage> Bijlagen => Set<Domain.Financien.Bijlagen.Bijlage>();
    public DbSet<Jaarrekeningindeling> Jaarrekeningindelingen => Set<Jaarrekeningindeling>();
    public DbSet<Domain.Financien.Bijlagen.BijlageInhoud> BijlageInhoud => Set<Domain.Financien.Bijlagen.BijlageInhoud>();
    internal DbSet<StartwizardRij> Startwizard => Set<StartwizardRij>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Administratie>(b =>
        {
            b.ToTable("Administraties");
            b.HasKey(a => a.Id);
            b.Property(a => a.Soort).IsRequired();
            b.HasIndex(a => a.Soort).IsUnique();
            b.Property(a => a.Naam).IsRequired().HasMaxLength(Administratie.MaximaleNaamLengte);
            b.Property(a => a.ZonderBonDrempel).HasDefaultValue(Administratie.StandaardZonderBonDrempel).HasSentinel(-1L);
            b.Property(a => a.Accentkleur)
                .IsRequired()
                .HasMaxLength(7)
                .HasConversion(k => k.ToString(), s => Kleur.Parse(s));

            b.HasData(
                new
                {
                    Id = 1,
                    Soort = AdministratieSoort.Vereniging,
                    Naam = "Mijn administratie",
                    Accentkleur = Administratie.Standaardkleur(AdministratieSoort.Vereniging),
                });
        });

        BoekhoudingConfiguratie.Configureer(modelBuilder);
        RelatieConfiguratie.Configureer(modelBuilder);
        BijlageConfiguratie.Configureer(modelBuilder);
        JaarrekeningConfiguratie.Configureer(modelBuilder);
        modelBuilder.Entity<StartwizardRij>(b =>
        {
            b.ToTable("Startwizard");
            b.HasKey(r => r.Id);
            b.Property(r => r.Id).ValueGeneratedNever();
            b.Property(r => r.Stap).HasConversion<string>().HasMaxLength(20);
        });
    }
}
