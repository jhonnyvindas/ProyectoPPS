using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using PasarelaPago.Client;
using MudBlazor.Services;
using Syncfusion.Blazor;


var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddHttpClient("PasarelaPago.ServerAPI", client => client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress));

// Supply HttpClient instances that include access tokens when making requests to the server project
builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("PasarelaPago.ServerAPI"));


// HttpClient default del WASM (ya suele existir)
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });


builder.Services.AddScoped<PasarelaPago.Client.Services.TilopayApi>();

builder.Services.AddMudServices();

builder.Services.AddSyncfusionBlazor();

Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("Mgo+DSMBPh8sVXJ8S0d+X1JPd11dXmJWd1p/THNYflR1fV9DaUwxOX1dQl9mSXxSdERjWXlddX1VT2E=;Mgo+DSMBMAY9C3t2XVhhQlJHfV5AQmBIYVp/TGpJfl96cVxMZVVBJAtUQF1hTH5Rd0diWX9Yc3FcRGRb;MzcxNzE0NEAzMjM4MmUzMDJlMzBTY1k0THgzczZmMVo1SEc0VVRsVWVtakV4UzZhSG1mRDdtTVpWb3lwOFBFPQ==;MzcxNzE0NUAzMjM4MmUzMDJlMzBMMzllNDZ1ckNCakNEQ0hGZUFFcEJ5TGJhZ0t0eEdsWU5qTHVWT3lpbGNnPQ==");


await builder.Build().RunAsync();
