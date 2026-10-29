namespace Verenigd.Infrastructure.Database.Migraties;

/// <summary>SQL die in migraties wordt gebruikt; apart, zodat het los te testen is.</summary>
internal static class MigratieSql
{
    /// <summary>
    /// Besluit 152: baten of lasten voorstellen uit het saldo van de boekingen en de historie (zonder afsluitposten, Soort 3);
    /// zonder saldo vanaf 8000 baten (1), anders lasten (2). Balansrekeningen (Soort 1) krijgen 0.
    /// </summary>
    public const string KantVoorstellen = """
                UPDATE "Grootboekrekeningen" SET "Kant" = CASE
                    WHEN "Soort" <> 2 THEN 0
                    WHEN (COALESCE((SELECT SUM(j."Debet" - j."Credit") FROM "Journaalregels" j JOIN "Journaalposten" p ON p."Id" = j."PostId"
                              WHERE j."RekeningId" = "Grootboekrekeningen"."Id" AND p."Soort" <> 3), 0)
                        + COALESCE((SELECT SUM(h."Debet" - h."Credit") FROM "HistorischeTotalen" h WHERE h."RekeningId" = "Grootboekrekeningen"."Id"), 0)) < 0 THEN 1
                    WHEN (COALESCE((SELECT SUM(j."Debet" - j."Credit") FROM "Journaalregels" j JOIN "Journaalposten" p ON p."Id" = j."PostId"
                              WHERE j."RekeningId" = "Grootboekrekeningen"."Id" AND p."Soort" <> 3), 0)
                        + COALESCE((SELECT SUM(h."Debet" - h."Credit") FROM "HistorischeTotalen" h WHERE h."RekeningId" = "Grootboekrekeningen"."Id"), 0)) > 0 THEN 2
                    WHEN "Nummer" BETWEEN 8000 AND 9999 THEN 1
                    ELSE 2
                END;
                
        """;
}
