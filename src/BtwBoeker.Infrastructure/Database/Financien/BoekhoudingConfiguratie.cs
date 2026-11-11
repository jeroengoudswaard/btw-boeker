using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using BtwBoeker.Domain.Financien;
using BtwBoeker.Domain.Financien.Bank;
using BtwBoeker.Domain.Financien.Boekhouding;

namespace BtwBoeker.Infrastructure.Database.Financien;

/// <summary>Tabellen van het grootboek, per administratie.</summary>
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
            b.Property(x => x.Iban).HasMaxLength(34).HasConversion(IbanConverter);
            b.HasIndex(x => new { x.Administratie, x.Iban }).IsUnique().HasFilter("\"Iban\" IS NOT NULL");
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
            // Doorlopende nummering per dagboek per jaar; concepten hebben nog geen nummer.
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

        ConfigureerBank(modelBuilder);
    }

    private static readonly ValueConverter<Iban, string> IbanConverter = new(i => i.Waarde, s => Iban.Maak(s));
    private static readonly ValueConverter<Bedrag, long> BedragConverter = new(b => b.Centen, c => new Bedrag(c));

    /// <summary>Tabellen van de bankimport.</summary>
    private static void ConfigureerBank(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Bankafschrift>(b =>
        {
            b.ToTable("Bankafschriften");
            b.HasKey(x => x.Id);
            b.Property(x => x.Nummer).IsRequired().HasMaxLength(35);
            b.HasOne(x => x.Dagboek).WithMany().HasForeignKey("DagboekId").IsRequired().OnDelete(DeleteBehavior.Restrict);
            b.HasIndex("DagboekId", nameof(Bankafschrift.Datum), nameof(Bankafschrift.Nummer)).IsUnique();
        });

        modelBuilder.Entity<Bankmutatie>(b =>
        {
            b.ToTable("Bankmutaties");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            // Nooit dubbel inlezen; hoofdlettergevoelig vergelijken (bankreferenties).
            b.Property(x => x.Sleutel).IsRequired().HasMaxLength(200).UseCollation("BINARY");
            b.HasIndex(x => new { x.Administratie, x.Sleutel }).IsUnique();
            b.HasOne(x => x.Dagboek).WithMany().HasForeignKey("DagboekId").IsRequired().OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Journaalpost>().WithMany().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => x.PostId);
            b.Property(x => x.Bedrag).HasConversion(BedragConverter);
            b.Property(x => x.Code).HasMaxLength(100).HasConversion(c => c.Waarde, s => BankTransactiecode.Lees(s));
            b.Property(x => x.TegenIban).HasMaxLength(34);
            b.Property(x => x.Tegennaam).HasMaxLength(Bankmutatie.MaximaleTekstLengte);
            b.Property(x => x.Omschrijving).HasMaxLength(Bankmutatie.MaximaleTekstLengte);
            b.Property(x => x.EndToEndId).HasMaxLength(Bankmutatie.MaximaleTekstLengte);
            b.Property(x => x.MandaatId).HasMaxLength(Bankmutatie.MaximaleTekstLengte);
            b.Property(x => x.BatchKenmerk).HasMaxLength(Bankmutatie.MaximaleTekstLengte);
            b.Property(x => x.Retourreden).HasMaxLength(10);
            b.Ignore(x => x.Saldo);
            b.Ignore(x => x.Standaardomschrijving);
        });

        modelBuilder.Entity<Boekingsregel>(b =>
        {
            b.ToTable("Boekingsregels");
            b.HasKey(x => x.Id);
            b.Property(x => x.Naam).IsRequired().HasMaxLength(100);
            b.Property(x => x.TegenIban).HasMaxLength(34);
            b.Property(x => x.NaamBevat).HasMaxLength(100);
            b.Property(x => x.OmschrijvingBevat).HasMaxLength(100);
            b.Property(x => x.Transactiecode).HasMaxLength(50);
            b.Property(x => x.Omschrijving).HasMaxLength(Journaalpost.MaximaleOmschrijvingLengte);
            b.Property(x => x.BedragVan).HasConversion(BedragConverter);
            b.Property(x => x.BedragTot).HasConversion(BedragConverter);
            b.HasOne(x => x.Rekening).WithMany().HasForeignKey("RekeningId").IsRequired().OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.Administratie, x.Volgorde });
            b.Ignore(x => x.Voorwaarden);
        });
    }
}
