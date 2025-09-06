using Microsoft.EntityFrameworkCore;
using PasarelaPago.Shared.Models;

namespace PasarelaPago.Server.Data;

public class TilopayDbContext : DbContext
{
    public TilopayDbContext(DbContextOptions<TilopayDbContext> options)
        : base(options)
    {
    }

    public DbSet<Cliente> Clientes { get; set; }
    public DbSet<Pago> Pagos { get; set; }
}