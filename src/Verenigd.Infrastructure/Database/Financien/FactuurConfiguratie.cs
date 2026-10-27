using Microsoft.EntityFrameworkCore;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Financien.Boekhouding;
using Verenigd.Domain.Financien.Facturen;
using Verenigd.Domain.Financien.Relaties;

namespace Verenigd.Infrastructure.Database.Financien;

/// <summary>Tabellen van relaties en verkoopfacturen (stap 4c-2).</summary>
internal static class FactuurConfiguratie
{
    public static void Configureer(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Relatie>(b =>
        {
            b.ToTable("Relaties");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Naam).IsRequired().HasMaxLength(100);
            b.Property(x => x.Contactpersoon).HasMaxLength(100);
            b.Property(x => x.Straat).HasMaxLength(100);
            b.Property(x => x.Postcode).HasMaxLength(10);
            b.Property(x => x.Plaats).HasMaxLength(100);
            b.Property(x => x.Email).HasMaxLength(254);
            b.Property(x => x.Telefoon).HasMaxLength(20);
            b.Property(x => x.Iban).HasMaxLength(34);
            b.Property(x => x.Notitie).HasMaxLength(2000);
            b.Ignore(x => x.Adresregels);
            b.HasIndex(x => x.Naam);
        });

        modelBuilder.Entity<Verkoopfactuur>(b =>
        {
            b.ToTable("Verkoopfacturen");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Nummer).HasMaxLength(20);
            b.Property(x => x.Ontvanger).HasMaxLength(500);
            b.HasOne<Relatie>().WithMany().HasForeignKey(x => x.RelatieId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Journaalpost>().WithMany().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Verkoopfactuur>().WithMany().HasForeignKey(x => x.CreditVan).OnDelete(DeleteBehavior.Restrict);
            // Besluit 106: nummers per administratie en jaar, zonder gaten of dubbelen.
            b.HasIndex(x => new { x.Administratie, x.Jaar, x.Volgnummer }).IsUnique().HasFilter("\"Volgnummer\" IS NOT NULL");
            b.HasMany(x => x.Regels).WithOne().HasForeignKey("FactuurId").IsRequired().OnDelete(DeleteBehavior.Cascade);
            b.Navigation(x => x.Regels).UsePropertyAccessMode(PropertyAccessMode.Field);
            b.Ignore(x => x.Totaal);
            b.Ignore(x => x.Vervaldatum);
            b.Ignore(x => x.IsCreditfactuur);
            b.Ignore(x => x.IsDefinitief);
        });

        modelBuilder.Entity<Factuurregel>(b =>
        {
            b.ToTable("Factuurregels");
            b.HasKey(x => x.Id);
            b.Property(x => x.Omschrijving).IsRequired().HasMaxLength(200);
            b.Property(x => x.Prijs).HasConversion(v => v.Centen, c => new Bedrag(c));
            b.HasOne(x => x.Rekening).WithMany().HasForeignKey("RekeningId").IsRequired().OnDelete(DeleteBehavior.Restrict);
            b.Ignore(x => x.Bedrag);
        });

        modelBuilder.Entity<Factuurinstellingen>(b =>
        {
            b.ToTable("Factuurinstellingen");
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.Administratie).IsUnique();
            b.Property(x => x.Naam).HasMaxLength(100);
            b.Property(x => x.Straat).HasMaxLength(100);
            b.Property(x => x.Postcode).HasMaxLength(10);
            b.Property(x => x.Plaats).HasMaxLength(100);
            b.Property(x => x.Kvk).HasMaxLength(20);
            b.Property(x => x.Iban).HasMaxLength(34);
            b.Property(x => x.Email).HasMaxLength(254);
            b.Property(x => x.Telefoon).HasMaxLength(20);
            b.Property(x => x.Voettekst).HasMaxLength(500);
            b.Property(x => x.LogoType).HasMaxLength(20);
        });
    }
}
