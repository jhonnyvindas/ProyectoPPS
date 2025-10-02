using Microsoft.EntityFrameworkCore;
using PasarelaPago.Server.Data;
using PasarelaPago.Shared.Dtos;
using PasarelaPago.Shared.Models;
using System.Transactions;

namespace PasarelaPago.Server.Services;

public class TransaccionService
{
    private readonly TilopayDbContext _context; // OK: Usas TilopayDbContext

    public TransaccionService(TilopayDbContext context)
    {
        _context = context;
    }

    public IQueryable<Pago> Transacciones => _context.Pagos.AsQueryable();

    public async Task GuardarTransaccionAsync(Cliente cliente, Pago pago)
    {
        // Validación de datos mínimos
        if (string.IsNullOrWhiteSpace(cliente.cedula) || string.IsNullOrWhiteSpace(pago.numeroOrden))
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
                    .FirstOrDefaultAsync(c => c.cedula == cliente.cedula);

                if (clienteExistente == null)
                {
                    // CASO A: CLIENTE NUEVO (INSERT)
                    _context.Clientes.Add(cliente);
                }
                else
                {
                    // CASO B: CLIENTE EXISTENTE (UPDATE)

                    // Actualizar datos del cliente
                    clienteExistente.nombre = cliente.nombre ?? clienteExistente.nombre;
                    clienteExistente.apellido = cliente.apellido ?? clienteExistente.apellido;
                    clienteExistente.correo = cliente.correo ?? clienteExistente.correo;
                    clienteExistente.pais = cliente.pais ?? clienteExistente.pais;
                    clienteExistente.cedula = cliente.cedula;

                    // CRUCIAL: Se usa ClienteId en lugar de Id
                    // Esta línea estaba causando el CS1061
                    pago.cedula = clienteExistente.cedula; // <-- CORRECCIÓN AQUÍ
                }

                // --- 2. MANEJAR EL PAGO (UPSERT POR NUMERO DE ORDEN) ---
                var transaccionExistente = await _context.Pagos
                    .FirstOrDefaultAsync(p => p.numeroOrden == pago.numeroOrden);

                var nuevoEstado = (pago.estadoTilopay ?? "").Trim().ToLowerInvariant();
                var estadoAprobado = "aprobado";

                if (transaccionExistente == null)
                {
                    // CASO X: PAGO NUEVO (INSERT)
                    _context.Pagos.Add(pago);
                }
                else
                {
                    // CASO Y: PAGO EXISTENTE (PREVENIR DUPLICIDAD)
                    var estadoActual = (transaccionExistente.estadoTilopay ?? "").Trim().ToLowerInvariant();

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
                        transaccionExistente.estadoTilopay = pago.estadoTilopay;
                        transaccionExistente.datosRespuestaTilopay = pago.datosRespuestaTilopay;
                        transaccionExistente.monto = pago.monto;
                        transaccionExistente.fechaTransaccion = pago.fechaTransaccion;
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