using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace InfoDumpManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVectorColumnsToGEM : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.AddColumn<Vector>(
                name: "SummaryEmbedding",
                table: "Gems",
                type: "vector(1536)",
                nullable: true);

            migrationBuilder.AddColumn<Vector>(
                name: "TitleEmbedding",
                table: "Gems",
                type: "vector(1536)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CategorySuggestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    GEMId = table.Column<Guid>(type: "uuid", nullable: false),
                    SuggestedCategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProposedCategoryName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ConfidenceScore = table.Column<double>(type: "double precision", nullable: false),
                    Rationale = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    AutoAssigned = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategorySuggestions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "cost_usage_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    GEMId = table.Column<Guid>(type: "uuid", nullable: false),
                    Operation = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TokensUsed = table.Column<int>(type: "integer", nullable: false),
                    Cost = table.Column<decimal>(type: "numeric", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cost_usage_entries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "embedding_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Model = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Vector = table.Column<Vector>(type: "vector(1536)", nullable: false),
                    MetadataJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_embedding_records", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tags", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Gems_SummaryEmbedding",
                table: "Gems",
                column: "SummaryEmbedding")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_Gems_TitleEmbedding",
                table: "Gems",
                column: "TitleEmbedding")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_CategorySuggestions_SuggestedCategoryId",
                table: "CategorySuggestions",
                column: "SuggestedCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_CategorySuggestions_TenantId_GEMId",
                table: "CategorySuggestions",
                columns: new[] { "TenantId", "GEMId" });

            migrationBuilder.CreateIndex(
                name: "IX_cost_usage_entries_TenantId_CreatedAt",
                table: "cost_usage_entries",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_embedding_records_TenantId_ContentType",
                table: "embedding_records",
                columns: new[] { "TenantId", "ContentType" });

            migrationBuilder.CreateIndex(
                name: "IX_Tags_TenantId_Name",
                table: "Tags",
                columns: new[] { "TenantId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CategorySuggestions");

            migrationBuilder.DropTable(
                name: "cost_usage_entries");

            migrationBuilder.DropTable(
                name: "embedding_records");

            migrationBuilder.DropTable(
                name: "Tags");

            migrationBuilder.DropIndex(
                name: "IX_Gems_SummaryEmbedding",
                table: "Gems");

            migrationBuilder.DropIndex(
                name: "IX_Gems_TitleEmbedding",
                table: "Gems");

            migrationBuilder.DropColumn(
                name: "SummaryEmbedding",
                table: "Gems");

            migrationBuilder.DropColumn(
                name: "TitleEmbedding",
                table: "Gems");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:vector", ",,");
        }
    }
}
