using PasarelaPago.Shared.Models;
using PasarelaPago.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace PasarelaPago.Server.Services;

public class TransaccionService
{
    private readonly TilopayDbContext _context;

    public TransaccionService(TilopayDbContext context) => _context = context;

    public async Task GuardarTransaccionAsync(Cliente cliente, Pago pago)
    {
        // Validaciones mínimas
        if (string.IsNullOrWhiteSpace(pago.numeroOrden))
            throw new ArgumentException("numeroOrden es requerido");
        if (pago.monto <= 0)
            throw new ArgumentException("monto debe ser > 0");
        if (pago.fechaTransaccion == default)
            pago.fechaTransaccion = DateTime.UtcNow;

        await using var tx = await _context.Database.BeginTransactionAsync();

        try
        {
            var clienteExistente = await _context.Clientes
                .FirstOrDefaultAsync(c => c.correo == cliente.correo);

            int clienteId;

            if (clienteExistente == null)
            {
                _context.Clientes.Add(cliente);
                await _context.SaveChangesAsync(); // para obtener el identity
                clienteId = cliente.clienteId;
            }
            else
            {
                // (Opcional) actualiza datos básicos del cliente
                clienteExistente.nombre = cliente.nombre;
                clienteExistente.apellido = cliente.apellido;
                clienteExistente.telefono = cliente.telefono;
                clienteExistente.direccion = cliente.direccion;
                clienteExistente.ciudad = cliente.ciudad;
                clienteExistente.provincia = cliente.provincia;
                clienteExistente.codigoPostal = cliente.codigoPostal;
                clienteExistente.pais = cliente.pais;

                await _context.SaveChangesAsync();
                clienteId = clienteExistente.clienteId;
            }

            // Asegura FK y campos obligatorios
            pago.clienteId = clienteId;

            _context.Pagos.Add(pago);
            await _context.SaveChangesAsync();

            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }
}
