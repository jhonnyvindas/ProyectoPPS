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

    public TransaccionController(TransaccionService svc) => _svc = svc;

    public class PagoConCliente

    {
        public Cliente Cliente { get; set; } = default!;

        public Pago Pago { get; set; } = default!;
    }

    [HttpPost]

    public async Task<IActionResult> Post([FromBody] PagoConCliente body)

    {

        if (body is null) return BadRequest("Cuerpo vacío.");

        var ced = body.Cliente?.cedula ?? body.Pago?.cedula;

        if (string.IsNullOrWhiteSpace(ced))

            return BadRequest("La cédula es requerida.");

        body.Cliente ??= new Cliente { cedula = ced };

        body.Cliente.cedula = ced;

        body.Pago.cedula = ced;

        await _svc.GuardarTransaccionAsync(body.Cliente, body.Pago);

        return Ok();

    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<PaginacionResponse<DTOTransacciones>>> GetTransacciones([FromQuery] FiltroTransacciones filtro)
    {
        try
        {
            var query = _svc.Transacciones.Include(p => p.Cliente).AsQueryable();

        if (filtro.FechaInicio.HasValue)
        {
            query = query.Where(t => t.fechaTransaccion >= filtro.FechaInicio.Value.ToUniversalTime());
        }

        if (filtro.FechaFin.HasValue)
        {
            query = query.Where(t => t.fechaTransaccion <= filtro.FechaFin.Value.ToUniversalTime());
        }

        if (!string.IsNullOrWhiteSpace(filtro.EstadoTransaccion))
        {
            var e = filtro.EstadoTransaccion.Trim().ToLower();
            var canon = e switch
            {
                "aprobado" => "success",
                "rechazado" => "failed",
                _ => e
            };

            query = query.Where(t => (t.estadoTilopay ?? "").ToLower() == canon);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Busqueda))
        {
            var busqueda = $"%{filtro.Busqueda.ToLower()}%";
            query = query.Where(t =>
                EF.Functions.Like((t.Cliente.nombre ?? "").ToLower(), busqueda) ||
                EF.Functions.Like((t.Cliente.apellido ?? "").ToLower(), busqueda) ||
                EF.Functions.Like((t.cedula ?? "").ToLower(), busqueda) ||
                EF.Functions.Like((t.numeroOrden ?? "").ToLower(), busqueda)
            );
        }

        var total = await query.CountAsync();

        var data = await query
            .OrderByDescending(t => t.fechaTransaccion)
            .Skip((filtro.Pagina - 1) * filtro.Tamanio)
            .Take(filtro.Tamanio)
            .Select(t => new DTOTransacciones
            {
                cedula = t.cedula,
                nombreCliente = t.Cliente != null ? $"{t.Cliente.nombre} {t.Cliente.apellido}" : "Desconocido",
                pais = t.Cliente != null ? t.Cliente.pais : "Desconocido",
                monto = t.monto,
                moneda = t.moneda,
                numeroOrden = t.numeroOrden,
                estadoTransaccion = t.estadoTilopay,
                fechaTransaccion = t.fechaTransaccion
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

            return BadRequest(ex.Message);
        }

        
    }

}