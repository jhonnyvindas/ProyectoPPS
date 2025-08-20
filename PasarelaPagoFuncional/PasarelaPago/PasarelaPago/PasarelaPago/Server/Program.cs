using Microsoft.AspNetCore.ResponseCompression;

var builder = WebApplication.CreateBuilder(args);

// 1. Registra compresión incluyendo el MIME WS-proxy
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

//  Force URLs fijos si no lo has hecho:
// builder.WebHost.UseUrls("https://localhost:7295", "http://localhost:5203");

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();

    app.UseWebAssemblyDebugging();

    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Middleware de compresión antes de servir Blazor
app.UseResponseCompression();

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.UseRouting();
app.UseCors(CordsDev);

app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();