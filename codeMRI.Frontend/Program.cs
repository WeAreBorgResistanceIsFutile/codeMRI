using codeMRI.Frontend;
using codeMRI.Frontend.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient
    { BaseAddress = new Uri("http://localhost:5247") }); // Matches Server launchSettings
builder.Services.AddScoped<AppState>();
builder.Services.AddHttpClient<WikiApiClient>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5247");
    client.Timeout = TimeSpan.FromMinutes(30); // Long timeout for ingestion
});
builder.Services.AddScoped<WikiHubClient>();

await builder.Build().RunAsync();