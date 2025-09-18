using Microsoft.EntityFrameworkCore;
using PasarelaPago.Server.Data;
using PasarelaPago.Shared.Dtos;
using PasarelaPago.Shared.Models;
using System.Transactions;

namespace PasarelaPago.Server.Services;

public class TransaccionService
{
    private readonly TilopayDbContext _context;

    public TransaccionService(TilopayDbContext context)
    {
        _context = context;
    }

    public IQueryable<Pago> Transacciones => _context.Pagos.AsQueryable();

    public async Task GuardarTransaccionAsync(Cliente cliente, Pago pago)
    {
        using (var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
        {
            try
            {
                var clienteExistente = await _context.Clientes
                    .FirstOrDefaultAsync(c => c.cedula == cliente.cedula);

                if (clienteExistente == null)
                {
                    _context.Clientes.Add(cliente);
                }
                else
                {
                    cliente.nombre = clienteExistente.nombre;
                    cliente.apellido = clienteExistente.apellido;
                }

                _context.Pagos.Add(pago);

                await _context.SaveChangesAsync();

                scope.Complete();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al guardar en la base de datos: {ex.Message}");
                throw;
            }
        }
    }
}