using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PasarelaPago.Server.Services;
using PasarelaPago.Shared.Dtos;
using PasarelaPago.Shared.Models;


namespace PasarelaPago.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TransaccionController : ControllerBase
    {
        private readonly TransaccionService _svc;
        private readonly TilopayDBContext _context;
        private readonly ResultadoTokenService _tokens;

        public TransaccionController(
            TransaccionService svc,
            TilopayDBContext context,
            ResultadoTokenService tokens)
        {
            _svc = svc;
            _context = context;
            _tokens = tokens;
        }

        [HttpPost("preparar-orden")]
        public async Task<ActionResult<PrepararOrdenResponse>> PrepararOrden([FromBody] PrepararOrdenRequest req)
        {
            if (req is null) return BadRequest("Cuerpo vacío.");
            if (string.IsNullOrWhiteSpace(req.NumeroOrden)) return BadRequest("NumeroOrden requerido.");
            if (string.IsNullOrWhiteSpace(req.Cedula)) return BadRequest("Cedula requerida.");

            if (string.IsNullOrWhiteSpace(req.Moneda)) req.Moneda = "CRC";
            req.Moneda = req.Moneda.Trim().ToUpperInvariant();
            if (req.Moneda.Length > 3) req.Moneda = req.Moneda.Substring(0, 3);

            var numeroOrden = req.NumeroOrden.Trim();
            if (numeroOrden.Length > 64) numeroOrden = numeroOrden[..64];

            var cedula = req.Cedula.Trim();
            if (cedula.Length > 25) cedula = cedula[..25];

            await using var tx = await _context.Database.BeginTransactionAsync();

            try
            {
                var cliente = await _context.Clientes.FirstOrDefaultAsync(c => c.Cedula == cedula);
                if (cliente is null)
                {
                    cliente = new Cliente
                    {
                        Cedula = cedula,
                        Nombre = req.Nombre ?? string.Empty,
                        Apellido = req.Apellido ?? string.Empty,
                        Correo = req.Email,
                        Pais = req.Pais
                    };
                    _context.Clientes.Add(cliente);
                    await _context.SaveChangesAsync(); 
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(req.Nombre)) cliente.Nombre = req.Nombre!;
                    if (!string.IsNullOrWhiteSpace(req.Apellido)) cliente.Apellido = req.Apellido!;
                    if (!string.IsNullOrWhiteSpace(req.Email)) cliente.Correo = req.Email!;
                    if (!string.IsNullOrWhiteSpace(req.Pais)) cliente.Pais = req.Pais!;
                    await _context.SaveChangesAsync();
                }

                var pago = await _context.Pagos.FirstOrDefaultAsync(p => p.NumeroOrden == numeroOrden);

                if (pago is null)
                {
                    pago = new Pago
                    {
                        NumeroOrden = numeroOrden,
                        Cedula = cedula,                    
                        MetodoPago = "payfac",
                        Monto = req.Monto,
                        Moneda = req.Moneda,
                        EstadoTilopay = "pendiente",
                        FechaTransaccion = DateTime.UtcNow,
                        StateNonce = string.IsNullOrWhiteSpace(req.StateNonce)
                            ? Guid.NewGuid().ToString("N")
                            : req.StateNonce.Trim()
                    };
                    _context.Pagos.Add(pago);
                }
                else
                {
                    pago.Cedula = cedula; 
                    pago.Monto = req.Monto;
                    pago.Moneda = req.Moneda;
                    pago.MetodoPago = string.IsNullOrWhiteSpace(pago.MetodoPago) ? "payfac" : pago.MetodoPago;
                    pago.EstadoTilopay ??= "pendiente";
                    if (pago.FechaTransaccion == default) pago.FechaTransaccion = DateTime.UtcNow;
                    if (string.IsNullOrWhiteSpace(pago.StateNonce)) pago.StateNonce = Guid.NewGuid().ToString("N");
                }

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                var token = _tokens.Save(numeroOrden);
                Console.WriteLine($"[TOKENS] Guardado token {token} -> {numeroOrden} @ {DateTime.UtcNow:O}");

                return Ok(new PrepararOrdenResponse
                {
                    Token = token,
                    ExpiraUtc = DateTime.UtcNow.AddMinutes(30)
                });
            }
            catch (DbUpdateException dbex)
            {
                await tx.RollbackAsync();
                var root = dbex.GetBaseException()?.Message ?? dbex.Message;
                Console.WriteLine($"[ERROR preparar-orden][DB] {root}");
                return StatusCode(500, $"Error BD al asegurar la orden: {root}");
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                Console.WriteLine($"[ERROR preparar-orden] {ex}");
                return StatusCode(500, $"Error al asegurar la orden: {ex.Message}");
            }
        }


        public sealed class PrepararOrdenRequest
        {
            public string NumeroOrden { get; set; } = default!;
            public string Cedula { get; set; } = default!;
            public decimal Monto { get; set; }
            public string Moneda { get; set; } = "CRC";
            public string? StateNonce { get; set; }

            public string? Nombre { get; set; }
            public string? Apellido { get; set; }
            public string? Email { get; set; }
            public string? Pais { get; set; }
        }


        public sealed class PrepararOrdenResponse
        {
            public string Token { get; set; } = default!;
            public DateTime ExpiraUtc { get; set; }
        }

        public sealed class ResultadoPagoDto
        {
            public string NumeroOrden { get; set; } = default!;
            public string? Cedula { get; set; }
            public string? Estado { get; set; }
            public decimal Monto { get; set; }
            public string Moneda { get; set; } = "CRC";
            public string? NumeroAutorizacion { get; set; }
            public string? MarcaTarjeta { get; set; }
            public DateTime FechaTransaccion { get; set; }
            public string? Nombre { get; set; }
            public string? Apellido { get; set; }
            public string? DisplayCustomer { get; set; }
            public string? Email { get; set; }
            public string? Pais { get; set; }
            public string? TilopayTx { get; set; }
        }

        [HttpGet("resultado/{token}")]
        public async Task<ActionResult<ResultadoPagoDto>> ResultadoPorToken(string token)
        {
            // 1) Resolver número de orden por token o fallback 'order'
            if (!_tokens.TryGet(token, out var numeroOrden))
            {
                var order = HttpContext.Request.Query["order"].ToString();
                if (!string.IsNullOrWhiteSpace(order))
                {
                    numeroOrden = order;
                    Console.WriteLine($"[TOKENS] Fallback por 'order'='{order}' (token perdido en caché).");
                }
                else
                {
                    return NotFound("Token inválido o expirado (no está en caché).");
                }
            }

            // 2) Cargar pago desde BD
            var pago = await _context.Pagos.FirstOrDefaultAsync(p => p.NumeroOrden == numeroOrden);
            if (pago is null)
                return NotFound($"Orden '{numeroOrden}' no encontrada en BD.");

            // 3) Leer query params (si es que vienen)
            var qs = HttpContext.Request.Query;

            string code = qs["code"].ToString();
            string status = qs["status"].ToString();
            string description = qs["description"].ToString();
            string auth = qs["auth"].ToString();
            string brand = qs["brand"].ToString();
            string last4 = qs["last-digits"].ToString();
            string tpt = qs["tilopay-transaction"].ToString();
            if (string.IsNullOrWhiteSpace(tpt))
                tpt = qs["tpt"].ToString();

            // 4) Detectar si realmente llegaron datos de Tilopay en esta llamada
            bool tieneInfoTilopay = !string.IsNullOrWhiteSpace(code)
                                 || !string.IsNullOrWhiteSpace(status)
                                 || !string.IsNullOrWhiteSpace(description)
                                 || !string.IsNullOrWhiteSpace(auth)
                                 || !string.IsNullOrWhiteSpace(brand)
                                 || !string.IsNullOrWhiteSpace(tpt);

            // 5) Normalización de estado (solo si llegaron params)
            static string NormalizarEstado(string codeVal, string statusVal, string descVal)
            {
                var s = (statusVal ?? "").Trim().ToLowerInvariant();
                var c = (codeVal ?? "").Trim().ToLowerInvariant();

                // más completo para casos aprobados
                if (c == "1" || s == "success" || s == "approved" || s == "captured" || s == "completed" || s == "paid" ||
                    (descVal?.IndexOf("aprob", StringComparison.OrdinalIgnoreCase) >= 0))
                    return "aprobado";

                // pendientes/revisión
                if (s == "pending" || s == "review" ||
                    (descVal?.IndexOf("pend", StringComparison.OrdinalIgnoreCase) >= 0))
                    return "pendiente";

                // por defecto: rechazado
                return "rechazado";
            }

            // 6) Persistir SOLO si esta llamada vino con información de Tilopay.
            if (tieneInfoTilopay)
            {
                var estado = NormalizarEstado(code, status, description);

                pago.EstadoTilopay = estado;
                if (!string.IsNullOrWhiteSpace(auth)) pago.NumeroAutorizacion = auth;
                if (!string.IsNullOrWhiteSpace(brand)) pago.MarcaTarjeta = brand;
                if (pago.FechaTransaccion == default) pago.FechaTransaccion = DateTime.UtcNow;

                pago.DatosRespuestaTilopay =
                    $"{{\"code\":\"{code}\",\"status\":\"{status}\",\"description\":\"{description}\",\"auth\":\"{auth}\",\"tx_id\":\"{tpt}\",\"order\":\"{numeroOrden}\"}}";

                await _context.SaveChangesAsync();
            }
            // Si NO llegaron params, no tocamos nada: devolvemos lo que ya está en BD.

            // 7) Armar DTO para la respuesta (tomando lo que quedó en BD)
            Cliente? cliente = null;
            if (!string.IsNullOrWhiteSpace(pago.Cedula))
                cliente = await _context.Clientes.AsNoTracking().FirstOrDefaultAsync(c => c.Cedula == pago.Cedula);

            string display = (cliente != null)
                ? $"{(cliente.Nombre ?? "").Trim()} {(cliente.Apellido ?? "").Trim()}".Trim()
                : null;

            var dto = new ResultadoPagoDto
            {
                NumeroOrden = pago.NumeroOrden,
                Cedula = pago.Cedula,
                Estado = pago.EstadoTilopay ?? "desconocido",
                Monto = pago.Monto,
                Moneda = pago.Moneda,
                NumeroAutorizacion = pago.NumeroAutorizacion,
                MarcaTarjeta = pago.MarcaTarjeta,
                FechaTransaccion = pago.FechaTransaccion,
                TilopayTx = string.IsNullOrWhiteSpace(tpt) ? null : tpt,
                Nombre = cliente?.Nombre,
                Apellido = cliente?.Apellido,
                DisplayCustomer = display,
                Email = cliente?.Correo,
                Pais = cliente?.Pais
            };

            return Ok(dto);
        }


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
            var order = body.Pago?.NumeroOrden;

            if (string.IsNullOrWhiteSpace(ced))
                return BadRequest("La cédula es requerida.");

            if (string.IsNullOrWhiteSpace(order))
                return BadRequest("El número de orden (numeroOrden) es requerido.");

            body.Cliente ??= new Cliente { Cedula = ced };
            body.Cliente.Cedula = ced;
            body.Pago.Cedula = ced;

            await _svc.GuardarTransaccionAsync(body.Cliente, body.Pago);
            return Ok();
        }

        [HttpGet("dashboard")]
        public async Task<ActionResult<PaginacionResponse<DTOTransacciones>>> GetTransacciones([FromQuery] FiltroTransacciones filtro)
        {
            try
            {
                var success = new[] { "approved", "success", "captured", "completed", "paid", "aprobado" };

                var query =
                    from pago in _svc.Transacciones.AsNoTracking()
                    join cliente in _context.Clientes on pago.Cedula equals cliente.Cedula
                    select new
                    {
                        Pago = pago,
                        Cliente = cliente
                    };

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

                if (!string.IsNullOrWhiteSpace(filtro.EstadoTransaccion))
                {
                    var estado = filtro.EstadoTransaccion.Trim().ToLowerInvariant();
                    query = estado switch
                    {
                        "aprobado" => query.Where(t =>
                            t.Pago.EstadoTilopay != null &&
                            success.Contains(t.Pago.EstadoTilopay.ToLower())
                        ),
                        "rechazado" => query.Where(t =>
                            t.Pago.EstadoTilopay == null ||
                            !success.Contains(t.Pago.EstadoTilopay.ToLower())
                        ),
                        _ => query
                    };
                }

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
                        nombreCliente = t.Cliente != null ? $"{t.Cliente.Nombre ?? ""} {t.Cliente.Apellido ?? ""}" : "Desconocido",
                        pais = t.Cliente != null ? t.Cliente.Pais : "Desconocido",
                        monto = t.Pago.Monto,
                        moneda = t.Pago.Moneda,
                        numeroOrden = t.Pago.NumeroOrden,
                        fechaTransaccion = t.Pago.FechaTransaccion,
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
                Console.WriteLine($"Error en GetTransacciones: {ex}");
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("callback/{token}")]
        public async Task<IActionResult> Callback(string token)
        {
            // 1) Resolver numeroOrden por token o fallback 'order'
            if (!_tokens.TryGet(token, out var numeroOrden))
            {
                var order = HttpContext.Request.Query["order"].ToString();
                if (string.IsNullOrWhiteSpace(order))
                    return NotFound("Token inválido o expirado.");
                numeroOrden = order;
            }

            // 2) Tomar QS y persistir estado
            var qs = HttpContext.Request.Query;
            string code = qs["code"].ToString();
            string status = qs["status"].ToString();
            string description = qs["description"].ToString();
            string auth = qs["auth"].ToString();
            string brand = qs["brand"].ToString();
            string tpt = string.IsNullOrWhiteSpace(qs["tilopay-transaction"])
                ? qs["tpt"].ToString()
                : qs["tilopay-transaction"].ToString();

            var pago = await _context.Pagos.FirstOrDefaultAsync(p => p.NumeroOrden == numeroOrden);
            if (pago is null) return NotFound($"Orden '{numeroOrden}' no encontrada.");

            static string NormalizarEstado(string c, string s, string d)
            {
                s = (s ?? "").Trim().ToLowerInvariant();
                c = (c ?? "").Trim().ToLowerInvariant();

                if (c == "1" || s == "success" || s == "approved" || s == "captured" || s == "completed" || s == "paid" ||
                    d?.Contains("aprob", StringComparison.OrdinalIgnoreCase) == true)
                    return "aprobado";

                if (s == "pending" || s == "review" ||
                    d?.Contains("pend", StringComparison.OrdinalIgnoreCase) == true)
                    return "pendiente";

                return "rechazado";
            }

            var estado = NormalizarEstado(code, status, description);
            pago.EstadoTilopay = estado;
            if (!string.IsNullOrWhiteSpace(auth)) pago.NumeroAutorizacion = auth;
            if (!string.IsNullOrWhiteSpace(brand)) pago.MarcaTarjeta = brand;
            if (pago.FechaTransaccion == default) pago.FechaTransaccion = DateTime.UtcNow;

            pago.DatosRespuestaTilopay =
                $"{{\"code\":\"{code}\",\"status\":\"{status}\",\"description\":\"{description}\",\"auth\":\"{auth}\",\"tx_id\":\"{tpt}\",\"order\":\"{numeroOrden}\"}}";

            await _context.SaveChangesAsync();

            return Redirect($"/pagos/resultado/{token}");
        }

        [HttpGet("por-orden/{order}")]
        public async Task<ActionResult<ResultadoPagoDto>> ObtenerPorOrden(string order)
        {
            var pago = await _context.Pagos.FirstOrDefaultAsync(p => p.NumeroOrden == order);
            if (pago is null) return NotFound($"Orden '{order}' no encontrada.");

            Cliente? cliente = null;
            if (!string.IsNullOrWhiteSpace(pago.Cedula))
                cliente = await _context.Clientes.AsNoTracking().FirstOrDefaultAsync(c => c.Cedula == pago.Cedula);

            string? display = cliente is null
                ? null
                : $"{(cliente.Nombre ?? "").Trim()} {(cliente.Apellido ?? "").Trim()}".Trim();

            var dto = new ResultadoPagoDto
            {
                NumeroOrden = pago.NumeroOrden,
                Cedula = pago.Cedula,
                Estado = pago.EstadoTilopay ?? "desconocido",
                Monto = pago.Monto,
                Moneda = pago.Moneda,
                NumeroAutorizacion = pago.NumeroAutorizacion,
                MarcaTarjeta = pago.MarcaTarjeta,
                FechaTransaccion = pago.FechaTransaccion,
                TilopayTx = null,
                Nombre = cliente?.Nombre,
                Apellido = cliente?.Apellido,
                DisplayCustomer = display,
                Email = cliente?.Correo,
                Pais = cliente?.Pais
            };

            return Ok(dto);
        }

    }
}
