using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace BtwBoeker.Infrastructure.Database.Migraties
{
    /// <inheritdoc />
    public partial class BtwBoekerBasis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Administraties",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Soort = table.Column<int>(type: "INTEGER", nullable: false),
                    Naam = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Accentkleur = table.Column<string>(type: "TEXT", maxLength: 7, nullable: false),
                    ZonderBonDrempel = table.Column<long>(type: "INTEGER", nullable: false, defaultValue: 1000L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Administraties", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Begrotingen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Administratie = table.Column<int>(type: "INTEGER", nullable: false),
                    Jaar = table.Column<int>(type: "INTEGER", nullable: false),
                    Versie = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Omschrijving = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    VastgesteldOp = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    AangemaaktUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Begrotingen", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Boekjaren",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Administratie = table.Column<int>(type: "INTEGER", nullable: false),
                    Jaar = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    AfgeslotenUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsHistorisch = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Boekjaren", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Jaarrekeningindelingen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Administratie = table.Column<int>(type: "INTEGER", nullable: false),
                    Rapport = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Jaarrekeningindelingen", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Meerjarenplannen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Administratie = table.Column<int>(type: "INTEGER", nullable: false),
                    Startjaar = table.Column<int>(type: "INTEGER", nullable: false),
                    AantalJaren = table.Column<int>(type: "INTEGER", nullable: false),
                    Versie = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Omschrijving = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    VastgesteldOp = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    AangemaaktUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Meerjarenplannen", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Relaties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Naam = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Contactpersoon = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Straat = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Postcode = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    Plaats = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 254, nullable: true),
                    Telefoon = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Iban = table.Column<string>(type: "TEXT", maxLength: 34, nullable: true),
                    IsDebiteur = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsCrediteur = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notitie = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    IsActief = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Relaties", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Rubrieken",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Administratie = table.Column<int>(type: "INTEGER", nullable: false),
                    Naam = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Volgorde = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rubrieken", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Startwizard",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Stap = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    IsAfgerond = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Startwizard", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Indelingsregels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Volgorde = table.Column<int>(type: "INTEGER", nullable: false),
                    Afdeling = table.Column<int>(type: "INTEGER", nullable: false),
                    Soort = table.Column<int>(type: "INTEGER", nullable: false),
                    Tekst = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    IndelingId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Indelingsregels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Indelingsregels_Jaarrekeningindelingen_IndelingId",
                        column: x => x.IndelingId,
                        principalTable: "Jaarrekeningindelingen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Grootboekrekeningen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Administratie = table.Column<int>(type: "INTEGER", nullable: false),
                    Nummer = table.Column<int>(type: "INTEGER", nullable: false),
                    Naam = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    RubriekId = table.Column<int>(type: "INTEGER", nullable: true),
                    Soort = table.Column<int>(type: "INTEGER", nullable: false),
                    Rol = table.Column<int>(type: "INTEGER", nullable: false),
                    Kant = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActief = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Grootboekrekeningen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Grootboekrekeningen_Rubrieken_RubriekId",
                        column: x => x.RubriekId,
                        principalTable: "Rubrieken",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "IndelingsregelRubrieken",
                columns: table => new
                {
                    RegelId = table.Column<int>(type: "INTEGER", nullable: false),
                    RubriekId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IndelingsregelRubrieken", x => new { x.RegelId, x.RubriekId });
                    table.ForeignKey(
                        name: "FK_IndelingsregelRubrieken_Indelingsregels_RegelId",
                        column: x => x.RegelId,
                        principalTable: "Indelingsregels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IndelingsregelRubrieken_Rubrieken_RubriekId",
                        column: x => x.RubriekId,
                        principalTable: "Rubrieken",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Begrotingsregels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RekeningId = table.Column<int>(type: "INTEGER", nullable: false),
                    Bedrag = table.Column<long>(type: "INTEGER", nullable: false),
                    Toelichting = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    BegrotingId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Begrotingsregels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Begrotingsregels_Begrotingen_BegrotingId",
                        column: x => x.BegrotingId,
                        principalTable: "Begrotingen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Begrotingsregels_Grootboekrekeningen_RekeningId",
                        column: x => x.RekeningId,
                        principalTable: "Grootboekrekeningen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Boekingsregels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Administratie = table.Column<int>(type: "INTEGER", nullable: false),
                    Volgorde = table.Column<int>(type: "INTEGER", nullable: false),
                    Naam = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    TegenIban = table.Column<string>(type: "TEXT", maxLength: 34, nullable: true),
                    NaamBevat = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    OmschrijvingBevat = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Richting = table.Column<int>(type: "INTEGER", nullable: false),
                    BedragVan = table.Column<long>(type: "INTEGER", nullable: true),
                    BedragTot = table.Column<long>(type: "INTEGER", nullable: true),
                    Transactiecode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    RekeningId = table.Column<int>(type: "INTEGER", nullable: false),
                    Omschrijving = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    IsActief = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Boekingsregels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Boekingsregels_Grootboekrekeningen_RekeningId",
                        column: x => x.RekeningId,
                        principalTable: "Grootboekrekeningen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Dagboeken",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Administratie = table.Column<int>(type: "INTEGER", nullable: false),
                    Soort = table.Column<int>(type: "INTEGER", nullable: false),
                    Iban = table.Column<string>(type: "TEXT", maxLength: 34, nullable: true),
                    Code = table.Column<string>(type: "TEXT", maxLength: 5, nullable: false),
                    Naam = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    RekeningId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dagboeken", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Dagboeken_Grootboekrekeningen_RekeningId",
                        column: x => x.RekeningId,
                        principalTable: "Grootboekrekeningen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HistorischeRegels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Administratie = table.Column<int>(type: "INTEGER", nullable: false),
                    Jaar = table.Column<int>(type: "INTEGER", nullable: false),
                    RekeningId = table.Column<int>(type: "INTEGER", nullable: false),
                    Datum = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Omschrijving = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Debet = table.Column<long>(type: "INTEGER", nullable: false),
                    Credit = table.Column<long>(type: "INTEGER", nullable: false),
                    Soort = table.Column<int>(type: "INTEGER", nullable: false),
                    Volgorde = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistorischeRegels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistorischeRegels_Grootboekrekeningen_RekeningId",
                        column: x => x.RekeningId,
                        principalTable: "Grootboekrekeningen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HistorischeTotalen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Administratie = table.Column<int>(type: "INTEGER", nullable: false),
                    Jaar = table.Column<int>(type: "INTEGER", nullable: false),
                    RekeningId = table.Column<int>(type: "INTEGER", nullable: false),
                    Debet = table.Column<long>(type: "INTEGER", nullable: false),
                    Credit = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistorischeTotalen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistorischeTotalen_Grootboekrekeningen_RekeningId",
                        column: x => x.RekeningId,
                        principalTable: "Grootboekrekeningen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Meerjarenposten",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RekeningId = table.Column<int>(type: "INTEGER", nullable: false),
                    Toelichting = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    PlanId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Meerjarenposten", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Meerjarenposten_Grootboekrekeningen_RekeningId",
                        column: x => x.RekeningId,
                        principalTable: "Grootboekrekeningen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Meerjarenposten_Meerjarenplannen_PlanId",
                        column: x => x.PlanId,
                        principalTable: "Meerjarenplannen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Bankafschriften",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Administratie = table.Column<int>(type: "INTEGER", nullable: false),
                    DagboekId = table.Column<int>(type: "INTEGER", nullable: false),
                    Nummer = table.Column<string>(type: "TEXT", maxLength: 35, nullable: false),
                    Datum = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Beginsaldo = table.Column<long>(type: "INTEGER", nullable: false),
                    Eindsaldo = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bankafschriften", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bankafschriften_Dagboeken_DagboekId",
                        column: x => x.DagboekId,
                        principalTable: "Dagboeken",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Journaalposten",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Administratie = table.Column<int>(type: "INTEGER", nullable: false),
                    DagboekId = table.Column<int>(type: "INTEGER", nullable: false),
                    Datum = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Jaar = table.Column<int>(type: "INTEGER", nullable: false),
                    Omschrijving = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Soort = table.Column<int>(type: "INTEGER", nullable: false),
                    Volgnummer = table.Column<int>(type: "INTEGER", nullable: true),
                    GeboektUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TegenboekingVan = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Journaalposten", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Journaalposten_Dagboeken_DagboekId",
                        column: x => x.DagboekId,
                        principalTable: "Dagboeken",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Meerjarenbedragen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Jaar = table.Column<int>(type: "INTEGER", nullable: false),
                    Bedrag = table.Column<long>(type: "INTEGER", nullable: false),
                    PostId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Meerjarenbedragen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Meerjarenbedragen_Meerjarenposten_PostId",
                        column: x => x.PostId,
                        principalTable: "Meerjarenposten",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Bankmutaties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Administratie = table.Column<int>(type: "INTEGER", nullable: false),
                    DagboekId = table.Column<int>(type: "INTEGER", nullable: false),
                    Sleutel = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false, collation: "BINARY"),
                    Boekdatum = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Bedrag = table.Column<long>(type: "INTEGER", nullable: false),
                    IsBij = table.Column<bool>(type: "INTEGER", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    TegenIban = table.Column<string>(type: "TEXT", maxLength: 34, nullable: true),
                    Tegennaam = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Omschrijving = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    EndToEndId = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    MandaatId = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    BatchAantal = table.Column<int>(type: "INTEGER", nullable: true),
                    BatchKenmerk = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Retourreden = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    PostId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bankmutaties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bankmutaties_Dagboeken_DagboekId",
                        column: x => x.DagboekId,
                        principalTable: "Dagboeken",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Bankmutaties_Journaalposten_PostId",
                        column: x => x.PostId,
                        principalTable: "Journaalposten",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Bijlagen",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Administratie = table.Column<int>(type: "INTEGER", nullable: false),
                    PostId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Bestandsnaam = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Soort = table.Column<int>(type: "INTEGER", nullable: false),
                    Grootte = table.Column<int>(type: "INTEGER", nullable: false),
                    Controlecode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Datum = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Bedrag = table.Column<long>(type: "INTEGER", nullable: true),
                    ToegevoegdUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bijlagen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bijlagen_Journaalposten_PostId",
                        column: x => x.PostId,
                        principalTable: "Journaalposten",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Journaalregels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RekeningId = table.Column<int>(type: "INTEGER", nullable: false),
                    Omschrijving = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Debet = table.Column<long>(type: "INTEGER", nullable: false),
                    Credit = table.Column<long>(type: "INTEGER", nullable: false),
                    PostId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Journaalregels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Journaalregels_Grootboekrekeningen_RekeningId",
                        column: x => x.RekeningId,
                        principalTable: "Grootboekrekeningen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Journaalregels_Journaalposten_PostId",
                        column: x => x.PostId,
                        principalTable: "Journaalposten",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BijlageInhoud",
                columns: table => new
                {
                    BijlageId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Gegevens = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BijlageInhoud", x => x.BijlageId);
                    table.ForeignKey(
                        name: "FK_BijlageInhoud_Bijlagen_BijlageId",
                        column: x => x.BijlageId,
                        principalTable: "Bijlagen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Administraties",
                columns: new[] { "Id", "Accentkleur", "Naam", "Soort" },
                values: new object[,]
                {
                    { 1, "#2E7D32", "Vereniging", 1 },
                    { 2, "#1565C0", "Stichting", 2 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Administraties_Soort",
                table: "Administraties",
                column: "Soort",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bankafschriften_DagboekId_Datum_Nummer",
                table: "Bankafschriften",
                columns: new[] { "DagboekId", "Datum", "Nummer" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bankmutaties_Administratie_Sleutel",
                table: "Bankmutaties",
                columns: new[] { "Administratie", "Sleutel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bankmutaties_DagboekId",
                table: "Bankmutaties",
                column: "DagboekId");

            migrationBuilder.CreateIndex(
                name: "IX_Bankmutaties_PostId",
                table: "Bankmutaties",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_Begrotingen_Administratie_Jaar_Versie",
                table: "Begrotingen",
                columns: new[] { "Administratie", "Jaar", "Versie" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Begrotingsregels_BegrotingId_RekeningId",
                table: "Begrotingsregels",
                columns: new[] { "BegrotingId", "RekeningId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Begrotingsregels_RekeningId",
                table: "Begrotingsregels",
                column: "RekeningId");

            migrationBuilder.CreateIndex(
                name: "IX_Bijlagen_Administratie",
                table: "Bijlagen",
                column: "Administratie");

            migrationBuilder.CreateIndex(
                name: "IX_Bijlagen_Controlecode",
                table: "Bijlagen",
                column: "Controlecode");

            migrationBuilder.CreateIndex(
                name: "IX_Bijlagen_PostId",
                table: "Bijlagen",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_Boekingsregels_Administratie_Volgorde",
                table: "Boekingsregels",
                columns: new[] { "Administratie", "Volgorde" });

            migrationBuilder.CreateIndex(
                name: "IX_Boekingsregels_RekeningId",
                table: "Boekingsregels",
                column: "RekeningId");

            migrationBuilder.CreateIndex(
                name: "IX_Boekjaren_Administratie_Jaar",
                table: "Boekjaren",
                columns: new[] { "Administratie", "Jaar" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Dagboeken_Administratie_Code",
                table: "Dagboeken",
                columns: new[] { "Administratie", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Dagboeken_Administratie_Iban",
                table: "Dagboeken",
                columns: new[] { "Administratie", "Iban" },
                unique: true,
                filter: "\"Iban\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Dagboeken_RekeningId",
                table: "Dagboeken",
                column: "RekeningId");

            migrationBuilder.CreateIndex(
                name: "IX_Grootboekrekeningen_Administratie_Nummer",
                table: "Grootboekrekeningen",
                columns: new[] { "Administratie", "Nummer" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Grootboekrekeningen_RubriekId",
                table: "Grootboekrekeningen",
                column: "RubriekId");

            migrationBuilder.CreateIndex(
                name: "IX_HistorischeRegels_Administratie_Jaar",
                table: "HistorischeRegels",
                columns: new[] { "Administratie", "Jaar" });

            migrationBuilder.CreateIndex(
                name: "IX_HistorischeRegels_RekeningId",
                table: "HistorischeRegels",
                column: "RekeningId");

            migrationBuilder.CreateIndex(
                name: "IX_HistorischeTotalen_Administratie_Jaar_RekeningId",
                table: "HistorischeTotalen",
                columns: new[] { "Administratie", "Jaar", "RekeningId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HistorischeTotalen_RekeningId",
                table: "HistorischeTotalen",
                column: "RekeningId");

            migrationBuilder.CreateIndex(
                name: "IX_IndelingsregelRubrieken_RubriekId",
                table: "IndelingsregelRubrieken",
                column: "RubriekId");

            migrationBuilder.CreateIndex(
                name: "IX_Indelingsregels_IndelingId",
                table: "Indelingsregels",
                column: "IndelingId");

            migrationBuilder.CreateIndex(
                name: "IX_Jaarrekeningindelingen_Administratie_Rapport",
                table: "Jaarrekeningindelingen",
                columns: new[] { "Administratie", "Rapport" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Journaalposten_Administratie_Jaar",
                table: "Journaalposten",
                columns: new[] { "Administratie", "Jaar" });

            migrationBuilder.CreateIndex(
                name: "IX_Journaalposten_DagboekId_Jaar_Volgnummer",
                table: "Journaalposten",
                columns: new[] { "DagboekId", "Jaar", "Volgnummer" },
                unique: true,
                filter: "\"Volgnummer\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Journaalregels_PostId",
                table: "Journaalregels",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_Journaalregels_RekeningId",
                table: "Journaalregels",
                column: "RekeningId");

            migrationBuilder.CreateIndex(
                name: "IX_Meerjarenbedragen_PostId_Jaar",
                table: "Meerjarenbedragen",
                columns: new[] { "PostId", "Jaar" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Meerjarenplannen_Administratie_Startjaar_Versie",
                table: "Meerjarenplannen",
                columns: new[] { "Administratie", "Startjaar", "Versie" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Meerjarenposten_PlanId_RekeningId",
                table: "Meerjarenposten",
                columns: new[] { "PlanId", "RekeningId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Meerjarenposten_RekeningId",
                table: "Meerjarenposten",
                column: "RekeningId");

            migrationBuilder.CreateIndex(
                name: "IX_Relaties_Naam",
                table: "Relaties",
                column: "Naam");

            migrationBuilder.CreateIndex(
                name: "IX_Rubrieken_Administratie_Naam",
                table: "Rubrieken",
                columns: new[] { "Administratie", "Naam" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Administraties");

            migrationBuilder.DropTable(
                name: "Bankafschriften");

            migrationBuilder.DropTable(
                name: "Bankmutaties");

            migrationBuilder.DropTable(
                name: "Begrotingsregels");

            migrationBuilder.DropTable(
                name: "BijlageInhoud");

            migrationBuilder.DropTable(
                name: "Boekingsregels");

            migrationBuilder.DropTable(
                name: "Boekjaren");

            migrationBuilder.DropTable(
                name: "HistorischeRegels");

            migrationBuilder.DropTable(
                name: "HistorischeTotalen");

            migrationBuilder.DropTable(
                name: "IndelingsregelRubrieken");

            migrationBuilder.DropTable(
                name: "Journaalregels");

            migrationBuilder.DropTable(
                name: "Meerjarenbedragen");

            migrationBuilder.DropTable(
                name: "Relaties");

            migrationBuilder.DropTable(
                name: "Startwizard");

            migrationBuilder.DropTable(
                name: "Begrotingen");

            migrationBuilder.DropTable(
                name: "Bijlagen");

            migrationBuilder.DropTable(
                name: "Indelingsregels");

            migrationBuilder.DropTable(
                name: "Meerjarenposten");

            migrationBuilder.DropTable(
                name: "Journaalposten");

            migrationBuilder.DropTable(
                name: "Jaarrekeningindelingen");

            migrationBuilder.DropTable(
                name: "Meerjarenplannen");

            migrationBuilder.DropTable(
                name: "Dagboeken");

            migrationBuilder.DropTable(
                name: "Grootboekrekeningen");

            migrationBuilder.DropTable(
                name: "Rubrieken");
        }
    }
}
