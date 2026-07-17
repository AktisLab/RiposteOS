using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RiposteOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOpportunityCountryCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string[]>(
                name: "CountryCodes",
                schema: "sourcing",
                table: "opportunities",
                type: "text[]",
                nullable: false,
                defaultValue: Array.Empty<string>());

            migrationBuilder.Sql(
                """
                UPDATE sourcing.opportunities
                SET "CountryCodes" = CASE
                    WHEN lower("Source") = 'boamp' THEN ARRAY['FRA']::text[]
                    WHEN lower("Source") = 'ted'
                        AND jsonb_typeof("RawPayload" -> 'place-of-performance-country-lot') = 'array'
                    THEN ARRAY(
                        SELECT DISTINCT upper(country)
                        FROM jsonb_array_elements_text(
                            "RawPayload" -> 'place-of-performance-country-lot') AS country)
                    WHEN lower("Source") = 'ted'
                        AND jsonb_typeof("RawPayload" -> 'place-of-performance-country-lot') = 'string'
                    THEN ARRAY[upper("RawPayload" ->> 'place-of-performance-country-lot')]
                    ELSE ARRAY[]::text[]
                END;
                """);

            migrationBuilder.Sql(
                """
                DELETE FROM sourcing.opportunities AS opportunity
                USING sourcing.sourcing_settings AS settings
                WHERE settings."Id" = 1
                    AND cardinality(settings."AllowedCountryCodes") > 0
                    AND NOT (
                        opportunity."CountryCodes"
                        && settings."AllowedCountryCodes");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CountryCodes",
                schema: "sourcing",
                table: "opportunities");
        }
    }
}
