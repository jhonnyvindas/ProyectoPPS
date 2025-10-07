using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PasarelaPago.Server.Services;
using PasarelaPago.Shared.Dtos;
using PasarelaPago.Shared.Models;


namespace PasarelaPago.Server.Controllers;



[ApiController]

[Route("api/[controller]")]

public class TransaccionController : ControllerBase
{
    private readonly TransaccionService _svc;

    public TransaccionController(TransaccionService svc, TilopayDBContext context)
    {
        _svc = svc;
        _context = context;
    }


    private readonly TilopayDBContext _context;


    public class PagoConCliente

    {
        public Cliente Cliente { get; set; } = default!;

        public Pago Pago { get; set; } = default!;
    }


    [HttpPost]
    public async Task<IActionResult> Post([FromBody] PagoConCliente body)
    {
        if (body is null) return BadRequest("Cuerpo vacío.");

        var ced = body.Cliente?.Cedula ?? body.Pago?.Cedula;
        // Agregamos chequeo para el numeroOrden, que es el ID único.
        var order = body.Pago?.NumeroOrden;

        if (string.IsNullOrWhiteSpace(ced))
            return BadRequest("La cédula es requerida.");

        if (string.IsNullOrWhiteSpace(order)) // <-- Validación crucial
            return BadRequest("El número de orden (numeroOrden) es requerido.");

        body.Cliente ??= new Cliente { Cedula = ced };
        body.Cliente.Cedula = ced;
        body.Pago.Cedula = ced;

        // La lógica de UPSERT debe estar dentro de este servicio.
        await _svc.GuardarTransaccionAsync(body.Cliente, body.Pago);

        return Ok();
    }

    // ... (código posterior)

    [HttpGet("dashboard")]
    public async Task<ActionResult<PaginacionResponse<DTOTransacciones>>> GetTransacciones([FromQuery] FiltroTransacciones filtro)
    {
        try
        {
            // Lista canónica de estados "exitosos"
            var success = new[] { "approved", "success", "captured", "completed", "paid", "aprobado" };

            //var query = _svc.Transacciones
               // .AsNoTracking()
                
                // .Include(p => p.Cliente)
                //.AsQueryable();
            var query =
                from pago in _svc.Transacciones.AsNoTracking()
                join cliente in _context.Clientes on pago.Cedula equals cliente.Cedula
                select new
                {
                    Pago = pago,
                    Cliente = cliente
                };


            // 1. Filtro de Fechas (se mantiene, es correcto)
            // Fechas: inicio inclusivo, fin exclusivo (día siguiente) en UTC
            if (filtro.FechaInicio.HasValue)
            {
                var fi = DateTime.SpecifyKind(filtro.FechaInicio.Value.Date, DateTimeKind.Utc);
                query = query.Where(t => t.Pago.FechaTransaccion >= fi);
            }
            if (filtro.FechaFin.HasValue)
            {
                var ff = DateTime.SpecifyKind(filtro.FechaFin.Value.Date.AddDays(1), DateTimeKind.Utc);
                query = query.Where(t => t.Pago.FechaTransaccion < ff);
            }

            // 2. Filtro por estado (se hace más robusto)
            if (!string.IsNullOrWhiteSpace(filtro.EstadoTransaccion))
            {
                var estado = filtro.EstadoTransaccion.Trim().ToLowerInvariant();

                // Usamos un switch expression para claridad y robustez
                query = estado switch
                {
                    "aprobado" => query.Where(t =>
                        t.Pago.EstadoTilopay != null &&
                        success.Contains(t.Pago.EstadoTilopay.ToLower())
                    ),
                    "rechazado" => query.Where(t =>
                        // Todo lo que NO es éxito
                        t.Pago.EstadoTilopay == null ||
                        !success.Contains(t.Pago.EstadoTilopay.ToLower())
                    ),
                    // 🚨 Caso default: Si el estado NO es "aprobado" ni "rechazado",
                    // se asume que es "Todos" o un valor no reconocido, y NO se aplica filtro.
                    _ => query
                };
            }

            // 3. Búsqueda (se mantiene, es correcto)
            if (!string.IsNullOrWhiteSpace(filtro.Busqueda))
            {
                var busq = $"%{filtro.Busqueda.ToLower()}%";
                query = query.Where(t =>
                    EF.Functions.Like((t.Cliente!.Nombre ?? "").ToLower(), busq) ||
                    EF.Functions.Like((t.Cliente!.Apellido ?? "").ToLower(), busq) ||
                    EF.Functions.Like((t.Pago.Cedula ?? "").ToLower(), busq) ||
                    EF.Functions.Like((t.Pago.NumeroOrden ?? "").ToLower(), busq)
                );
            }

            var total = await query.CountAsync();

            var data = await query
                .OrderByDescending(t => t.Pago.FechaTransaccion)
                .Skip((filtro.Pagina - 1) * filtro.Tamanio)
                .Take(filtro.Tamanio)
                .Select(t => new DTOTransacciones
                {
                    cedula = t.Pago.Cedula,
                    // Agregado null-coalescing para evitar excepciones si nombre o apellido son nulos
                    nombreCliente = t.Cliente != null ? $"{t.Cliente.Nombre ?? ""} {t.Cliente.Apellido ?? ""}" : "Desconocido",
                    pais = t.Cliente != null ? t.Cliente.Pais : "Desconocido",
                    monto = t.Pago.Monto,
                    moneda = t.Pago.Moneda,
                    numeroOrden = t.Pago.NumeroOrden,
                    fechaTransaccion = t.Pago.FechaTransaccion,

                    // Normaliza para UI/filtros: solo "aprobado" o "rechazado"
                    estadoTransaccion = t.Pago.EstadoTilopay != null && success.Contains(t.Pago.EstadoTilopay.ToLower())
                        ? "aprobado"
                        : "rechazado"
                })
                .ToListAsync();

            return Ok(new PaginacionResponse<DTOTransacciones>
            {
                TotalRegistros = total,
                Resultados = data
            });
        }
        catch (Exception ex)
        {
            // En un entorno de desarrollo, es útil loguear la excepción completa
            Console.WriteLine($"Error en GetTransacciones: {ex}");
            return BadRequest(ex.Message);
        }
    }

}