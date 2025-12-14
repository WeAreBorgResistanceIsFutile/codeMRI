using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using codeMRI.Frontend;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri("http://localhost:5247") }); // Matches Server launchSettings
builder.Services.AddScoped<codeMRI.Frontend.Services.AppState>();
builder.Services.AddHttpClient<codeMRI.Frontend.Services.WikiApiClient>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5247");
    client.Timeout = TimeSpan.FromMinutes(30); // Long timeout for ingestion
});
builder.Services.AddScoped<codeMRI.Frontend.Services.WikiHubClient>();

await builder.Build().RunAsync();
