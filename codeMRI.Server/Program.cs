using codeMRI.Core.Interfaces;
using codeMRI.Infrastructure;
using codeMRI.Infrastructure.Configuration;
using codeMRI.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configuration
builder.Services.Configure<OllamaSettings>(builder.Configuration.GetSection("Ollama"));
builder.Services.Configure<ASTServiceSettings>(builder.Configuration.GetSection("ASTService"));

// Infrastructure
builder.Services.AddHttpClient();
builder.Services.AddSingleton<ILLMClient, OllamaLLMService>();
builder.Services.AddSingleton<IASTServiceClient, ASTServiceClient>();

// Wire up core application services
WireUp.Registered(builder.Services);

builder.Services.AddSingleton<IWikiRepository>(sp =>
{
    var connectionString = builder.Configuration.GetConnectionString("WikiDb")
                           ?? "Data Source=../data/sqlite/codemri.db";
    return new SqliteWikiRepository(connectionString);
});


// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        b => b.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader());
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.MapHub<codeMRI.Server.Hubs.WikiHub>("/wikiHub");

app.Run();

public partial class Program
{
}