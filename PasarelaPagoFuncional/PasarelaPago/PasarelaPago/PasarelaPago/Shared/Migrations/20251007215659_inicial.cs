using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PasarelaPago.Shared.Migrations
{
    /// <inheritdoc />
    public partial class inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Clientes",
                columns: table => new
                {
                    cedula = table.Column<string>(type: "varchar(25)", unicode: false, maxLength: 25, nullable: false),
                    nombre = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    apellido = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    correo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    telefono = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    direccion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ciudad = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    provincia = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    codigoPostal = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    pais = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Clientes__415B7BE4634A9938", x => x.cedula);
                });

            migrationBuilder.CreateTable(
                name: "Pagos",
                columns: table => new
                {
                    pagoId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    cedula = table.Column<string>(type: "varchar(25)", unicode: false, maxLength: 25, nullable: false),
                    numeroOrden = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    metodoPago = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    monto = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    moneda = table.Column<string>(type: "varchar(3)", unicode: false, maxLength: 3, nullable: false),
                    estadoTilopay = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    numeroAutorizacion = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    marcaTarjeta = table.Column<string>(type: "varchar(12)", unicode: false, maxLength: 12, nullable: true),
                    datosRespuestaTilopay = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    fechaTransaccion = table.Column<DateTime>(type: "datetime2(6)", precision: 6, nullable: false),
                    stateNonce = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false, defaultValueSql: "(replace(CONVERT([varchar](36),newid()),'-',''))")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Pagos__5736FC0C7878344D", x => x.pagoId);
                    table.ForeignKey(
                        name: "FK__Pagos__cedula__5EBF139D",
                        column: x => x.cedula,
                        principalTable: "Clientes",
                        principalColumn: "cedula");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Pagos_cedula",
                table: "Pagos",
                column: "cedula");

            migrationBuilder.CreateIndex(
                name: "IX_Pagos_NumeroOrden",
                table: "Pagos",
                column: "numeroOrden",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Pagos");

            migrationBuilder.DropTable(
                name: "Clientes");
        }
    }
}
