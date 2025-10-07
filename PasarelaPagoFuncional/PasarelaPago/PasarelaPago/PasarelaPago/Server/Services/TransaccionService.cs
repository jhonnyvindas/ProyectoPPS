using Microsoft.EntityFrameworkCore;
using PasarelaPago.Shared.Dtos;
using PasarelaPago.Shared.Models;
using System.Transactions;

namespace PasarelaPago.Server.Services;

public class TransaccionService
{
    private readonly TilopayDBContext _context; // OK: Usas TilopayDbContext

    public TransaccionService(TilopayDBContext context)
    {
        _context = context;
    }

    public IQueryable<Pago> Transacciones => _context.Pagos.AsQueryable();

    public async Task GuardarTransaccionAsync(Cliente cliente, Pago pago)
    {
        // Validación de datos mínimos
        if (string.IsNullOrWhiteSpace(cliente.Cedula) || string.IsNullOrWhiteSpace(pago.NumeroOrden))
        {
            throw new ArgumentException("Cédula y Número de Orden son requeridos para la persistencia.");
        }

        // Usamos la transacción para asegurar que ambos (Cliente y Pago) se guarden o ninguno.
        using (var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
        {
            try
            {
                // --- 1. MANEJAR EL CLIENTE (UPSERT POR CÉDULA) ---
                var clienteExistente = await _context.Clientes
                    .FirstOrDefaultAsync(c => c.Cedula == cliente.Cedula);

                if (clienteExistente == null)
                {
                    // CASO A: CLIENTE NUEVO (INSERT)
                    _context.Clientes.Add(cliente);
                }
                else
                {
                    // CASO B: CLIENTE EXISTENTE (UPDATE)

                    // Actualizar datos del cliente
                    clienteExistente.Nombre = cliente.Nombre ?? clienteExistente.Nombre;
                    clienteExistente.Apellido = cliente.Apellido ?? clienteExistente.Apellido;
                    clienteExistente.Correo = cliente.Correo ?? clienteExistente.Correo;
                    clienteExistente.Pais = cliente.Pais ?? clienteExistente.Pais;
                    clienteExistente.Cedula = cliente.Cedula;

                    // CRUCIAL: Se usa ClienteId en lugar de Id
                    // Esta línea estaba causando el CS1061
                    pago.Cedula = clienteExistente.Cedula; // <-- CORRECCIÓN AQUÍ
                }

                // --- 2. MANEJAR EL PAGO (UPSERT POR NUMERO DE ORDEN) ---
                var transaccionExistente = await _context.Pagos
                    .FirstOrDefaultAsync(p => p.NumeroOrden == pago.NumeroOrden);

                var nuevoEstado = (pago.EstadoTilopay ?? "").Trim().ToLowerInvariant();
                var estadoAprobado = "aprobado";

                if (transaccionExistente == null)
                {
                    // CASO X: PAGO NUEVO (INSERT)
                    _context.Pagos.Add(pago);
                }
                else
                {
                    // CASO Y: PAGO EXISTENTE (PREVENIR DUPLICIDAD)
                    var estadoActual = (transaccionExistente.EstadoTilopay ?? "").Trim().ToLowerInvariant();

                    // Regla de Oro: Si ya está aprobado, ignoramos cualquier notificación posterior.
                    if (estadoActual == estadoAprobado)
                    {
                        // No hacemos SaveChangesAsync ni nada, simplemente completamos el scope.
                        scope.Complete();
                        return;
                    }

                    // Si el nuevo estado es 'aprobado', actualizamos la transacción existente
                    if (nuevoEstado == estadoAprobado)
                    {
                        transaccionExistente.EstadoTilopay = pago.EstadoTilopay;
                        transaccionExistente.DatosRespuestaTilopay = pago.DatosRespuestaTilopay;
                        transaccionExistente.Monto = pago.Monto;
                        transaccionExistente.FechaTransaccion = pago.FechaTransaccion;
                    }
                    // Si el nuevo estado es rechazado/pendiente y ya existe, no actualizamos
                    else
                    {
                        // Si ya tenemos un registro (aunque sea pendiente/rechazado), y el nuevo también es no-aprobado, 
                        // lo consideramos duplicado y nos vamos, manteniendo el primer registro.
                        scope.Complete();
                        return;
                    }
                }

                // Si llegamos aquí, hay algo que guardar/actualizar.
                await _context.SaveChangesAsync();

                scope.Complete();
            }
            catch (Exception ex)
            {
                // El scope se desecha y hace Rollback si no se llama a scope.Complete().
                Console.WriteLine($"Error al guardar en la base de datos: {ex.Message}");
                // IMPORTANTE: Debes lanzar la excepción para que el controlador devuelva un 500.
                throw;
            }
        }
    }
}