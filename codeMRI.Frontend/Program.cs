using codeMRI.Frontend;
using codeMRI.Frontend.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// API URL - matching API launch settings
var apiUrl = "http://localhost:5247";

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiUrl) });
builder.Services.AddScoped<WikiApiClient>();
builder.Services.AddScoped<AppState>();

await builder.Build().RunAsync();