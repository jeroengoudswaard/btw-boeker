using Microsoft.EntityFrameworkCore;
using BtwBoeker.Domain.Financien;
using BtwBoeker.Domain.Financien.Begrotingen;
using BtwBoeker.Domain.Financien.Boekhouding;

namespace BtwBoeker.Infrastructure.Database.Financien;

/// <summary>Begrotingen per jaar en versie (fase 5a); staan los van journaal en grootboek.</summary>
internal static class BegrotingConfiguratie
{
    public static void Configureer(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Begroting>(b =>
        {
            b.ToTable("Begrotingen");
            b.HasKey(x => x.Id);
            b.Property(x => x.Omschrijving).IsRequired().HasMaxLength(Begroting.MaximaleOmschrijvingLengte);
            b.HasIndex(x => new { x.Administratie, x.Jaar, x.Versie }).IsUnique();
            b.HasMany(x => x.Regels).WithOne().HasForeignKey("BegrotingId").IsRequired().OnDelete(DeleteBehavior.Cascade);
            b.Navigation(x => x.Regels).UsePropertyAccessMode(PropertyAccessMode.Field);
            b.Ignore(x => x.IsVastgesteld);
        });

        modelBuilder.Entity<Meerjarenplan>(b =>
        {
            b.ToTable("Meerjarenplannen");
            b.HasKey(x => x.Id);
            b.Property(x => x.Omschrijving).IsRequired().HasMaxLength(Begroting.MaximaleOmschrijvingLengte);
            b.HasIndex(x => new { x.Administratie, x.Startjaar, x.Versie }).IsUnique();
            b.HasMany(x => x.Posten).WithOne().HasForeignKey("PlanId").IsRequired().OnDelete(DeleteBehavior.Cascade);
            b.Navigation(x => x.Posten).UsePropertyAccessMode(PropertyAccessMode.Field);
            b.Ignore(x => x.IsVastgesteld);
            b.Ignore(x => x.Eindjaar);
            b.Ignore(x => x.Jaren);
        });

        modelBuilder.Entity<Meerjarenpost>(b =>
        {
            b.ToTable("Meerjarenposten");
            b.HasKey(x => x.Id);
            b.HasOne(x => x.Rekening).WithMany().HasForeignKey("RekeningId").IsRequired().OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.Toelichting).HasMaxLength(Meerjarenpost.MaximaleToelichtingLengte);
            b.HasIndex("PlanId", "RekeningId").IsUnique();
            b.HasMany(x => x.Bedragen).WithOne().HasForeignKey("PostId").IsRequired().OnDelete(DeleteBehavior.Cascade);
            b.Navigation(x => x.Bedragen).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<Meerjarenbedrag>(b =>
        {
            b.ToTable("Meerjarenbedragen");
            b.HasKey(x => x.Id);
            b.Property(x => x.Bedrag).HasConversion(v => v.Centen, c => new Bedrag(c));
            b.HasIndex("PostId", nameof(Meerjarenbedrag.Jaar)).IsUnique();
        });

        // Eigen jaarrekeningindeling (besluiten 263–268): alleen weergave, nooit onderdeel van het grootboek.
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

        modelBuilder.Entity<Begrotingsregel>(b =>
        {
            b.ToTable("Begrotingsregels");
            b.HasKey(x => x.Id);
            b.HasOne(x => x.Rekening).WithMany().HasForeignKey("RekeningId").IsRequired().OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.Bedrag).HasConversion(v => v.Centen, c => new Bedrag(c));
            b.Property(x => x.Toelichting).HasMaxLength(Begrotingsregel.MaximaleToelichtingLengte);
            b.HasIndex("BegrotingId", "RekeningId").IsUnique();
        });
    }
}
