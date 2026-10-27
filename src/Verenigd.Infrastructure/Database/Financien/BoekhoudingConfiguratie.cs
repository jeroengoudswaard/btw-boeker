using Microsoft.EntityFrameworkCore;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Financien.Boekhouding;

namespace Verenigd.Infrastructure.Database.Financien;

/// <summary>Tabellen van het grootboek (fase 4a), per administratie.</summary>
internal static class BoekhoudingConfiguratie
{
    public static void Configureer(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Rubriek>(b =>
        {
            b.ToTable("Rubrieken");
            b.HasKey(x => x.Id);
            b.Property(x => x.Naam).IsRequired().HasMaxLength(Rubriek.MaximaleNaamLengte);
            b.HasIndex(x => new { x.Administratie, x.Naam }).IsUnique();
        });

        modelBuilder.Entity<Grootboekrekening>(b =>
        {
            b.ToTable("Grootboekrekeningen");
            b.HasKey(x => x.Id);
            b.Property(x => x.Naam).IsRequired().HasMaxLength(Grootboekrekening.MaximaleNaamLengte);
            b.HasIndex(x => new { x.Administratie, x.Nummer }).IsUnique();
            b.HasOne(x => x.Rubriek).WithMany().HasForeignKey("RubriekId").OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Dagboek>(b =>
        {
            b.ToTable("Dagboeken");
            b.HasKey(x => x.Id);
            b.Property(x => x.Code).IsRequired().HasMaxLength(5);
            b.Property(x => x.Naam).IsRequired().HasMaxLength(100);
            b.HasIndex(x => new { x.Administratie, x.Code }).IsUnique();
            b.HasOne(x => x.Rekening).WithMany().HasForeignKey("RekeningId").OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Boekjaar>(b =>
        {
            b.ToTable("Boekjaren");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.Administratie, x.Jaar }).IsUnique();
            b.Ignore(x => x.IsOpen);
        });

        modelBuilder.Entity<Journaalpost>(b =>
        {
            b.ToTable("Journaalposten");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Omschrijving).IsRequired().HasMaxLength(Journaalpost.MaximaleOmschrijvingLengte);
            b.HasOne(x => x.Dagboek).WithMany().HasForeignKey("DagboekId").IsRequired().OnDelete(DeleteBehavior.Restrict);
            // Doorlopende nummering per dagboek per jaar; concepten hebben nog geen nummer (besluit 71).
            b.HasIndex("DagboekId", nameof(Journaalpost.Jaar), nameof(Journaalpost.Volgnummer)).IsUnique().HasFilter("\"Volgnummer\" IS NOT NULL");
            b.HasIndex(x => new { x.Administratie, x.Jaar });
            b.HasMany(x => x.Regels).WithOne().HasForeignKey("PostId").IsRequired().OnDelete(DeleteBehavior.Cascade);
            b.Navigation(x => x.Regels).UsePropertyAccessMode(PropertyAccessMode.Field);
            b.Ignore(x => x.Nummer);
            b.Ignore(x => x.TotaalDebet);
            b.Ignore(x => x.TotaalCredit);
            b.Ignore(x => x.Verschil);
            b.Ignore(x => x.IsInEvenwicht);
            b.Ignore(x => x.IsGeboekt);
        });

        modelBuilder.Entity<Journaalregel>(b =>
        {
            b.ToTable("Journaalregels");
            b.HasKey(x => x.Id);
            b.HasOne(x => x.Rekening).WithMany().HasForeignKey("RekeningId").IsRequired().OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.Omschrijving).HasMaxLength(200);
            b.Property(x => x.Debet).HasConversion(v => v.Centen, c => new Bedrag(c));
            b.Property(x => x.Credit).HasConversion(v => v.Centen, c => new Bedrag(c));
            b.Ignore(x => x.Saldo);
        });
    }
}
