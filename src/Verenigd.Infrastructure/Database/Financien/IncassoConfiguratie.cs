using Microsoft.EntityFrameworkCore;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Gedeeld;

namespace Verenigd.Infrastructure.Database.Financien;

/// <summary>Tabellen voor contributie en incasso (fase 3). Koppeling met leden alleen via het lidnummer (besluit 36).</summary>
internal static class IncassoConfiguratie
{
    public static void Configureer(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Betaler>(b =>
        {
            b.Property(x => x.Mandaatkenmerk).HasMaxLength(35).UseCollation("BINARY");
        });

        modelBuilder.Entity<Contributiecategorie>(b =>
        {
            b.ToTable("Contributiecategorieen");
            b.HasKey(x => x.Id);
            b.Property(x => x.Naam).IsRequired().HasMaxLength(Contributiecategorie.MaximaleNaamLengte);
            b.HasIndex(x => x.Naam).IsUnique();
            b.Property(x => x.Speltak).HasMaxLength(200);
            b.Ignore(x => x.HeeftTarief);
            // Verwijzing naar de boekhouding zonder databasekoppeling: contributie en grootboek blijven los te laden.
            b.Property(x => x.OpbrengstrekeningId);
        });

        modelBuilder.Entity<Tarief>(b =>
        {
            b.ToTable("Tarieven");
            b.HasKey(x => x.Id);
            b.HasOne(x => x.Categorie).WithMany().HasForeignKey("CategorieId").IsRequired().OnDelete(DeleteBehavior.Restrict);
            b.HasIndex("CategorieId", nameof(Tarief.Boekjaar)).IsUnique();
            b.Property(x => x.Maandbedrag).HasConversion(v => v.Centen, c => new Bedrag(c));
        });

        modelBuilder.Entity<LidCategorie>(b =>
        {
            b.ToTable("LidCategorieen");
            b.HasKey(x => x.Id);
            b.Property(x => x.Lidnummer).IsRequired().HasMaxLength(20).HasConversion(l => l.Waarde, s => Lidnummer.Parse(s));
            b.HasOne(x => x.Categorie).WithMany().HasForeignKey("CategorieId").IsRequired().OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.Lidnummer, x.Ingangsdatum }).IsUnique();
        });

        modelBuilder.Entity<Incassoinstellingen>(b =>
        {
            b.ToTable("Incassoinstellingen");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.IncassantId).IsRequired().HasMaxLength(35).HasConversion(i => i.Waarde, s => IncassantId.Maak(s));
            b.Property(x => x.Naam).IsRequired().HasMaxLength(70);
            b.Property(x => x.Iban).IsRequired().HasMaxLength(34).HasConversion(i => i.Waarde, s => Iban.Maak(s));
            b.Property(x => x.EersteIncassomaand).IsRequired().HasMaxLength(7).HasConversion(m => m.Code, s => Maand.Parse(s));
            b.Property(x => x.Omschrijvingssjabloon).IsRequired().HasMaxLength(200);
        });

        modelBuilder.Entity<Incassorun>(b =>
        {
            b.ToTable("Incassoruns");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Maand).IsRequired().HasMaxLength(7).HasConversion(m => m.Code, s => Maand.Parse(s));
            b.Property(x => x.BestandHash).HasMaxLength(64);
            b.HasMany(x => x.Posten).WithOne().HasForeignKey("RunId").IsRequired().OnDelete(DeleteBehavior.Restrict);
            b.Navigation(x => x.Posten).UsePropertyAccessMode(PropertyAccessMode.Field);
            b.Ignore(x => x.Totaal);
            b.Ignore(x => x.AantalPosten);
            b.Ignore(x => x.Kenmerk);
        });

        modelBuilder.Entity<Incassopost>(b =>
        {
            b.ToTable("Incassoposten");
            b.HasKey(x => x.Id);
            b.Property(x => x.EndToEndId).IsRequired().HasMaxLength(35);
            b.HasIndex(x => x.EndToEndId).IsUnique();
            b.HasOne<Betaler>().WithMany().HasForeignKey(x => x.BetalerId).OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.Iban).IsRequired().HasMaxLength(34).HasConversion(i => i.Waarde, s => Iban.Maak(s));
            b.Property(x => x.Rekeninghouder).IsRequired().HasMaxLength(70);
            b.Property(x => x.Mandaatkenmerk).IsRequired().HasMaxLength(35);
            b.Property(x => x.Omschrijving).IsRequired().HasMaxLength(140);
            b.HasMany(x => x.Regels).WithOne().HasForeignKey("PostId").IsRequired().OnDelete(DeleteBehavior.Restrict);
            b.Navigation(x => x.Regels).UsePropertyAccessMode(PropertyAccessMode.Field);
            b.Property(x => x.HerincassoVan).HasMaxLength(35);
            b.Ignore(x => x.Bedrag);
            b.Ignore(x => x.IsHerincasso);
            b.Ignore(x => x.Vordering);
        });

        modelBuilder.Entity<Postregel>(b =>
        {
            b.ToTable("Postregels");
            b.HasKey(x => x.Id);
            b.Property(x => x.Lidnummer).IsRequired().HasMaxLength(20).HasConversion(l => l.Waarde, s => Lidnummer.Parse(s));
            b.Property(x => x.Maand).IsRequired().HasMaxLength(7).HasConversion(m => m.Code, s => Maand.Parse(s));
            b.Property(x => x.Categorie).IsRequired().HasMaxLength(Contributiecategorie.MaximaleNaamLengte);
            b.Property(x => x.Bedrag).HasConversion(v => v.Centen, c => new Bedrag(c));
            // Besluit 55: een maand wordt nooit twee keer geïncasseerd, behalve na annuleren (besluit 60) en bij een
            // herincasso van een gestorneerde post (besluit 92).
            b.HasIndex(x => new { x.Lidnummer, x.Maand }).IsUnique().HasFilter("\"IsGeannuleerd\" = 0 AND \"IsHerincasso\" = 0");
        });

        modelBuilder.Entity<Domain.Financien.Debiteuren.Herincasso>(b =>
        {
            b.ToTable("Herincassos");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Vordering).IsRequired().HasMaxLength(35).UseCollation("BINARY");
            b.HasIndex(x => x.Vordering);
            b.Ignore(x => x.IsOpen);
        });
    }
}
