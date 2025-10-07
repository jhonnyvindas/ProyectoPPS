using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using PasarelaPago.Server.Services;
using PasarelaPago.Shared.Models;

var builder = WebApplication.CreateBuilder(args);

var cs = builder.Configuration.GetConnectionString("DefaultConnection");
Console.WriteLine($"[DB] ConnectionString: {(string.IsNullOrWhiteSpace(cs) ? "(null/empty)" : cs)}");

builder.Services.AddResponseCompression(options =>
{
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes
        .Concat(new[] { "application/octet-stream" });
});

builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

const string CordsDev = "CordsDev";
builder.Services.AddCors(opt =>
{
    opt.AddPolicy(CordsDev, p => p
        .WithOrigins("https://localhost:7295", "http://localhost:5203")
        .AllowAnyHeader()
        .AllowAnyMethod()
    );
});

builder.Services.AddDbContext<TilopayDBContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));


builder.Services.AddScoped<TransaccionService>();

var app = builder.Build();
// ---- Diagnóstico de BD al inicio ----
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TilopayDBContext>();
    var cnn = db.Database.GetDbConnection();
    Console.WriteLine($"[DB] Provider   : {db.Database.ProviderName}");
    Console.WriteLine($"[DB] DataSource : {cnn.DataSource}");
    Console.WriteLine($"[DB] Database   : {cnn.Database}");
    Console.WriteLine($"[DB] CanConnect : {db.Database.CanConnect()}");
}


if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();

    app.UseWebAssemblyDebugging();

    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseResponseCompression();

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.UseRouting();
app.UseCors(CordsDev);

app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();