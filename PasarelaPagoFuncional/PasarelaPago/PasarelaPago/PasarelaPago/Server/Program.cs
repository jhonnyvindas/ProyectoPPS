using Microsoft.AspNetCore.ResponseCompression;
using MudBlazor.Services;
using Syncfusion.Blazor;

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
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddMudServices();
builder.Services.AddSyncfusionBlazor();

Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("Mgo+DSMBPh8sVXJ8S0d+X1JPd11dXmJWd1p/THNYflR1fV9DaUwxOX1dQl9mSXxSdERjWXlddX1VT2E=;Mgo+DSMBMAY9C3t2XVhhQlJHfV5AQmBIYVp/TGpJfl96cVxMZVVBJAtUQF1hTH5Rd0diWX9Yc3FcRGRb;MzcxNzE0NEAzMjM4MmUzMDJlMzBTY1k0THgzczZmMVo1SEc0VVRsVWVtakV4UzZhSG1mRDdtTVpWb3lwOFBFPQ==;MzcxNzE0NUAzMjM4MmUzMDJlMzBMMzllNDZ1ckNCakNEQ0hGZUFFcEJ5TGJhZ0t0eEdsWU5qTHVWT3lpbGNnPQ==");

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