using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Azure.Storage.Blobs;
using CloudNotes.Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---- Configuration: Env Vars + Key Vault ----
var keyVaultUri = builder.Configuration["KeyVaultUri"];
if (!string.IsNullOrEmpty(keyVaultUri) && Uri.TryCreate(keyVaultUri, UriKind.Absolute, out var kvUri))
{
    builder.Configuration.AddAzureKeyVault(kvUri, new DefaultAzureCredential());
    Console.WriteLine($"[Config] Key Vault configured: {kvUri}");
}
else
{
    Console.WriteLine("[Config] Key Vault NOT configured (set KeyVaultUri env var to enable)");
}

// ---- Database: SQLite ----
var dbPath = builder.Configuration["DatabasePath"];
if (string.IsNullOrEmpty(dbPath))
{
    var home = Environment.GetEnvironmentVariable("HOME") ?? Path.GetTempPath();
    dbPath = Path.Combine(home, "data", "cloudnotes.db");
}
var dbDirectory = Path.GetDirectoryName(dbPath);
if (!string.IsNullOrEmpty(dbDirectory) && !Directory.Exists(dbDirectory))
{
    Directory.CreateDirectory(dbDirectory);
}

builder.Services.AddDbContext<NotesDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

Console.WriteLine($"[Config] SQLite database path: {dbPath}");

// ---- Blob Storage ----
var blobConnectionString = builder.Configuration["BlobConnectionString"] ?? "UseDevelopmentStorage=true";
builder.Services.AddSingleton(_ => new BlobServiceClient(blobConnectionString));

// ---- Key Vault Secret Client (for /api/secrets endpoint) ----
if (!string.IsNullOrEmpty(keyVaultUri) && Uri.TryCreate(keyVaultUri, UriKind.Absolute, out var secretUri))
{
    builder.Services.AddSingleton(new SecretClient(secretUri, new DefaultAzureCredential()));
}

// ---- HttpClient for Report Service (Container Apps) ----
builder.Services.AddHttpClient("ReportService", client =>
{
    client.Timeout = TimeSpan.FromSeconds(5);
});

// ---- MVC + Razor Pages ----
builder.Services.AddControllers();
builder.Services.AddRazorPages();

// ---- CORS (dev-friendly) ----
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NotesDbContext>();
    db.Database.EnsureCreated();
}

app.UseCors();
app.MapControllers();
app.MapRazorPages();

app.Run();



