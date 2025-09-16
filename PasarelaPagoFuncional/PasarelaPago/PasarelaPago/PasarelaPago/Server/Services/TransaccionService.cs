using Microsoft.EntityFrameworkCore;
using PasarelaPago.Server.Data;
using PasarelaPago.Shared.Models;
using System.Transactions; // Asegúrate de tener este 'using'

namespace PasarelaPago.Server.Services;

public class TransaccionService
{
    private readonly TilopayDbContext _context;

    public TransaccionService(TilopayDbContext context)
    {
        _context = context;
    }

    public async Task GuardarTransaccionAsync(Cliente cliente, Pago pago)
    {
        // Usa un TransactionScope para garantizar que todo se guarde o nada se guarde.
        // Es una alternativa a BeginTransactionAsync.
        using (var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
        {
            try
            {
                // Busca si el cliente ya existe en la base de datos por su cédula
                var clienteExistente = await _context.Clientes
          .FirstOrDefaultAsync(c => c.cedula == cliente.cedula);

                // Si el cliente no existe, lo agrega al contexto.
                if (clienteExistente == null)
                {
                    _context.Clientes.Add(cliente);
                }
                else
                {
                    // Si el cliente ya existe, se asocia el pago con el cliente existente.
                    // Las actualizaciones de los datos del cliente se harán en el mismo SaveChanges.
                    cliente.nombre = clienteExistente.nombre;
                    cliente.apellido = clienteExistente.apellido;
                    // ... puedes actualizar más campos aquí si lo deseas
                }

                // Agrega el pago al contexto.
                _context.Pagos.Add(pago);

                // Guarda todos los cambios en una sola operación.
                await _context.SaveChangesAsync();

                // Completa la transacción si no hay errores.
                scope.Complete();
            }
            catch (Exception ex)
            {
                // La transacción se revertirá automáticamente
                // Puedes registrar el error aquí para depurar
                Console.WriteLine($"Error al guardar en la base de datos: {ex.Message}");
                throw;
            }
        }
    }
}