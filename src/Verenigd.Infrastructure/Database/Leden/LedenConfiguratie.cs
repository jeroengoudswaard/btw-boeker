using Microsoft.EntityFrameworkCore;
using Verenigd.Domain.Gedeeld;
using Verenigd.Domain.Leden;

namespace Verenigd.Infrastructure.Database.Leden;

/// <summary>Tabellen van de ledengegevens. Geen enkele koppeling naar de financiële tabellen (besluit 36).</summary>
internal static class LedenConfiguratie
{
    public static void Configureer(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Groep>(b =>
        {
            b.ToTable("Groepen");
            b.HasKey(g => g.Id);
            b.Property(g => g.Naam).IsRequired().HasMaxLength(Groep.MaximaleNaamLengte).UseCollation("BINARY");
            b.HasIndex(g => g.Naam).IsUnique();
            b.Property(g => g.Soort).IsRequired();
        });

        modelBuilder.Entity<Persoon>(b =>
        {
            b.ToTable("Personen");
            b.HasKey(p => p.Id);
            b.Property(p => p.Id).ValueGeneratedNever();
            b.Property(p => p.Lidnummer).IsRequired().HasMaxLength(20).HasConversion(l => l.Waarde, s => Lidnummer.Parse(s));
            b.HasIndex(p => p.Lidnummer).IsUnique();
            b.Property(p => p.Voornaam).IsRequired().HasMaxLength(100);
            b.Property(p => p.Tussenvoegsel).HasMaxLength(50);
            b.Property(p => p.Achternaam).IsRequired().HasMaxLength(150);
            b.Property(p => p.Gender).HasMaxLength(50);
            b.Property(p => p.Email).HasMaxLength(254);
            b.Property(p => p.Telefoon).HasMaxLength(50);
            // Salesforce-ID's zijn hoofdlettergevoelig (besluit 30).
            b.Property(p => p.ContactpersoonId).HasMaxLength(18).UseCollation("BINARY");
            b.Property(p => p.Status).HasMaxLength(50);
            b.Property(p => p.Straat).HasMaxLength(200);
            b.Property(p => p.Huisnummer).HasMaxLength(20);
            b.Property(p => p.Toevoeging).HasMaxLength(20);
            b.Property(p => p.Postcode).HasMaxLength(20);
            b.Property(p => p.Plaats).HasMaxLength(100);
            b.Property(p => p.Land).HasMaxLength(100);
            b.Property(p => p.Notities).HasMaxLength(Persoon.MaximaleNotitieLengte);
            b.Property(p => p.LaatstGewijzigdUtc).IsRequired();

            b.HasMany(p => p.Functies).WithOne().HasForeignKey("PersoonId").IsRequired().OnDelete(DeleteBehavior.Cascade);
            b.HasMany(p => p.Voogden).WithOne().HasForeignKey("PersoonId").IsRequired().OnDelete(DeleteBehavior.Cascade);
            b.HasMany(p => p.Herkomst).WithOne().HasForeignKey("PersoonId").IsRequired().OnDelete(DeleteBehavior.Cascade);
            b.Navigation(p => p.Functies).UsePropertyAccessMode(PropertyAccessMode.Field);
            b.Navigation(p => p.Voogden).UsePropertyAccessMode(PropertyAccessMode.Field);
            b.Navigation(p => p.Herkomst).UsePropertyAccessMode(PropertyAccessMode.Field);
            b.Ignore(p => p.Gegevens);
        });

        modelBuilder.Entity<Functie>(b =>
        {
            b.ToTable("Functies");
            b.HasKey(f => f.Id);
            b.HasOne(f => f.Groep).WithMany().HasForeignKey("GroepId").IsRequired().OnDelete(DeleteBehavior.Restrict);
            b.Property(f => f.Rol).IsRequired().HasMaxLength(100);
            b.Property(f => f.Status).HasMaxLength(50);
        });

        modelBuilder.Entity<Voogd>(b =>
        {
            b.ToTable("Voogden");
            b.HasKey(v => v.Id);
            b.HasIndex("PersoonId", nameof(Voogd.Volgnummer)).IsUnique();
            b.Property(v => v.Naam).HasMaxLength(200);
            b.Property(v => v.Email).HasMaxLength(254);
            b.Property(v => v.Telefoon).HasMaxLength(50);
            b.Property(v => v.Mobiel).HasMaxLength(50);
            b.Ignore(v => v.Gegevens);
        });

        modelBuilder.Entity<Veldherkomst>(b =>
        {
            b.ToTable("Veldherkomst");
            b.HasKey(h => h.Id);
            b.HasIndex("PersoonId", nameof(Veldherkomst.Veld)).IsUnique();
            b.Property(h => h.Veld).IsRequired().HasMaxLength(50);
        });
    }
}
