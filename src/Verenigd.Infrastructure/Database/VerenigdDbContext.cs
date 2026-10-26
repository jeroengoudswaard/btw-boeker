using Microsoft.EntityFrameworkCore;
using Verenigd.Domain.Administraties;

namespace Verenigd.Infrastructure.Database;

public sealed class VerenigdDbContext(DbContextOptions<VerenigdDbContext> options) : DbContext(options)
{
    public DbSet<Administratie> Administraties => Set<Administratie>();

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
    }
}
