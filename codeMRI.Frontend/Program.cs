using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using codeMRI.Frontend;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri("http://localhost:5247") }); // Matches Server launchSettings
builder.Services.AddScoped<codeMRI.Frontend.Services.AppState>();
builder.Services.AddScoped<codeMRI.Frontend.Services.WikiApiClient>();

await builder.Build().RunAsync();
