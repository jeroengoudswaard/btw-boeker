using Microsoft.EntityFrameworkCore;
using Verenigd.Domain.Administraties;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Leden;
using Verenigd.Infrastructure.Database.Financien;
using Verenigd.Infrastructure.Database.Leden;

namespace Verenigd.Infrastructure.Database;

public sealed class VerenigdDbContext(DbContextOptions<VerenigdDbContext> options) : DbContext(options)
{
    public DbSet<Administratie> Administraties => Set<Administratie>();

    public DbSet<Persoon> Personen => Set<Persoon>();
    public DbSet<Groep> Groepen => Set<Groep>();
    public DbSet<Betaler> Betalers => Set<Betaler>();
    public DbSet<BetalerLid> BetalerLeden => Set<BetalerLid>();
    public DbSet<Contributiecategorie> Contributiecategorieen => Set<Contributiecategorie>();
    public DbSet<Tarief> Tarieven => Set<Tarief>();
    public DbSet<LidCategorie> LidCategorieen => Set<LidCategorie>();
    public DbSet<Incassoinstellingen> Incassoinstellingen => Set<Incassoinstellingen>();
    public DbSet<Incassorun> Incassoruns => Set<Incassorun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Administratie>(b =>
        {
            b.ToTable("Administraties");
            b.HasKey(a => a.Id);
            b.Property(a => a.Soort).IsRequired();
            b.HasIndex(a => a.Soort).IsUnique();
            b.Property(a => a.Naam).IsRequired().HasMaxLength(Administratie.MaximaleNaamLengte);
            b.Property(a => a.Accentkleur)
                .IsRequired()
                .HasMaxLength(7)
                .HasConversion(k => k.ToString(), s => Kleur.Parse(s));

            b.HasData(
                new
                {
                    Id = 1,
                    Soort = AdministratieSoort.Vereniging,
                    Naam = "Vereniging",
                    Accentkleur = Administratie.Standaardkleur(AdministratieSoort.Vereniging),
                },
                new
                {
                    Id = 2,
                    Soort = AdministratieSoort.Stichting,
                    Naam = "Stichting",
                    Accentkleur = Administratie.Standaardkleur(AdministratieSoort.Stichting),
                });
        });

        LedenConfiguratie.Configureer(modelBuilder);
        FinancienConfiguratie.Configureer(modelBuilder);
        IncassoConfiguratie.Configureer(modelBuilder);
    }
}
