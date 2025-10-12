using Microsoft.Extensions.Caching.Memory;

namespace PasarelaPago.Server.Services;

public class ResultadoTokenService
{
    private readonly IMemoryCache _cache;
    private readonly MemoryCacheEntryOptions _opts =
        new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(30));

    public ResultadoTokenService(IMemoryCache cache) => _cache = cache;

    public string Save(string numeroOrden)
    {
        var token = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .Replace("+", "-").Replace("/", "_").Replace("=", "");
        _cache.Set(token, numeroOrden, _opts);
        return token;
    }

    public bool TryGet(string token, out string numeroOrden)
    {
        if (_cache.TryGetValue<string>(token, out var n))
        {
            numeroOrden = n;
            return true;
        }
        numeroOrden = string.Empty;
        return false;
    }

    public void Invalidate(string token) => _cache.Remove(token);
}
