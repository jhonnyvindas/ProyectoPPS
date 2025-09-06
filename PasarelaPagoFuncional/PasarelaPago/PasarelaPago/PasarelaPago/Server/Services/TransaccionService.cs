using PasarelaPago.Shared.Models;
using PasarelaPago.Server.Data;
using Microsoft.EntityFrameworkCore;

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
        var clienteExistente = await _context.Clientes
            .FirstOrDefaultAsync(c => c.correo == cliente.correo);

        if (clienteExistente == null)
        {
            _context.Clientes.Add(cliente);
            await _context.SaveChangesAsync();
            pago.clienteId = cliente.clienteId;
        }
        else
        {
            pago.clienteId = clienteExistente.clienteId;
        }

        _context.Pagos.Add(pago);
        await _context.SaveChangesAsync();
    }
}