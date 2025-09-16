using Microsoft.EntityFrameworkCore;
using PasarelaPago.Server.Data;
using PasarelaPago.Shared.Models;

namespace PasarelaPago.Server.Data;

public class TilopayDbContext : DbContext

{

    public TilopayDbContext(DbContextOptions<TilopayDbContext> options) : base(options) { }



    public DbSet<Cliente> Clientes => Set<Cliente>();

    public DbSet<Pago> Pagos => Set<Pago>();



    protected override void OnModelCreating(ModelBuilder modelBuilder)

    {

        // Clientes

        modelBuilder.Entity<Cliente>(e =>

        {

            e.HasKey(c => c.cedula);

            e.Property(c => c.cedula).HasMaxLength(25).IsUnicode(false);



            e.Property(c => c.nombre).HasMaxLength(50);

            e.Property(c => c.apellido).HasMaxLength(50);

            e.Property(c => c.correo).HasMaxLength(100);

            e.Property(c => c.telefono).HasMaxLength(20);

            e.Property(c => c.direccion).HasMaxLength(100);

            e.Property(c => c.ciudad).HasMaxLength(50);

            e.Property(c => c.provincia).HasMaxLength(50);

            e.Property(c => c.codigoPostal).HasMaxLength(20);

            e.Property(c => c.pais).HasMaxLength(10);

        });



        // Pagos

        modelBuilder.Entity<Pago>(e =>

        {

            e.HasKey(p => p.pagoId);



            e.Property(p => p.numeroOrden).HasMaxLength(64).IsUnicode(false);

            e.Property(p => p.cedula).HasMaxLength(25).IsUnicode(false).IsRequired();



            e.Property(p => p.metodoPago).HasMaxLength(10).IsUnicode(false).IsRequired();

            e.Property(p => p.monto).HasColumnType("decimal(18,2)");

            e.Property(p => p.moneda).HasMaxLength(3).IsUnicode(false).IsRequired();

            e.Property(p => p.estadoTilopay).HasMaxLength(20).IsUnicode(false);

            e.Property(p => p.numeroAutorizacion).HasMaxLength(50).IsUnicode(false);

            e.Property(p => p.marcaTarjeta).HasMaxLength(12).IsUnicode(false);

            e.Property(p => p.datosRespuestaTilopay).HasColumnType("nvarchar(max)");

            e.Property(p => p.fechaTransaccion).HasColumnType("datetime2(6)");



            e.HasOne(p => p.Cliente)

            .WithMany(c => c.Pagos)

            .HasForeignKey(p => p.cedula)

            .HasPrincipalKey(c => c.cedula)

            .OnDelete(DeleteBehavior.Restrict);

        });

    }
}