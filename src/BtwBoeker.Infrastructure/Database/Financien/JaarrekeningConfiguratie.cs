using Microsoft.EntityFrameworkCore;
using BtwBoeker.Domain.Financien.Boekhouding;

namespace BtwBoeker.Infrastructure.Database.Financien;

/// <summary>Eigen jaarrekeningindeling (besluit 27): alleen weergave, nooit onderdeel van het grootboek.</summary>
internal static class JaarrekeningConfiguratie
{
    public static void Configureer(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Jaarrekeningindeling>(b =>
        {
            b.ToTable("Jaarrekeningindelingen");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.Administratie, x.Rapport }).IsUnique();
            b.HasMany(x => x.Regels).WithOne().HasForeignKey("IndelingId").IsRequired().OnDelete(DeleteBehavior.Cascade);
            b.Navigation(x => x.Regels).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<Indelingsregel>(b =>
        {
            b.ToTable("Indelingsregels");
            b.HasKey(x => x.Id);
            b.Property(x => x.Tekst).IsRequired().HasMaxLength(Indelingsregel.MaximaleTekstLengte);
            b.HasMany(x => x.Rubrieken).WithMany().UsingEntity(
                "IndelingsregelRubrieken",
                r => r.HasOne(typeof(Rubriek)).WithMany().HasForeignKey("RubriekId").OnDelete(DeleteBehavior.Cascade),
                l => l.HasOne(typeof(Indelingsregel)).WithMany().HasForeignKey("RegelId").OnDelete(DeleteBehavior.Cascade));
            b.Navigation(x => x.Rubrieken).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
    }
}
