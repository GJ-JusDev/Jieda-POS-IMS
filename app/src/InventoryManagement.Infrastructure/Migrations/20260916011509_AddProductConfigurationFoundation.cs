using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventoryManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductConfigurationFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConfigurationSnapshot",
                table: "SaleItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProductInputs",
                columns: table => new
                {
                    ProductInputId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    InputName = table.Column<string>(type: "TEXT", nullable: false),
                    Label = table.Column<string>(type: "TEXT", nullable: false),
                    InputType = table.Column<int>(type: "INTEGER", nullable: false),
                    Required = table.Column<bool>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    Unit = table.Column<string>(type: "TEXT", nullable: true),
                    DefaultValue = table.Column<string>(type: "TEXT", nullable: true),
                    HelpText = table.Column<string>(type: "TEXT", nullable: true),
                    MinimumValue = table.Column<decimal>(type: "TEXT", nullable: true),
                    MaximumValue = table.Column<decimal>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductInputs", x => x.ProductInputId);
                    table.ForeignKey(
                        name: "FK_ProductInputs_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "ProductId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductInputOptions",
                columns: table => new
                {
                    ProductInputOptionId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProductInputId = table.Column<int>(type: "INTEGER", nullable: false),
                    OptionName = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: true),
                    AdditionalPrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductInputOptions", x => x.ProductInputOptionId);
                    table.ForeignKey(
                        name: "FK_ProductInputOptions_ProductInputs_ProductInputId",
                        column: x => x.ProductInputId,
                        principalTable: "ProductInputs",
                        principalColumn: "ProductInputId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductInputOptions_ProductInputId",
                table: "ProductInputOptions",
                column: "ProductInputId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductInputs_ProductId",
                table: "ProductInputs",
                column: "ProductId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductInputOptions");

            migrationBuilder.DropTable(
                name: "ProductInputs");

            migrationBuilder.DropColumn(
                name: "ConfigurationSnapshot",
                table: "SaleItems");
        }
    }
}
