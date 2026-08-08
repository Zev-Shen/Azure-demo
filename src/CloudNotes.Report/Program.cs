using Azure.Identity;
using Azure.Storage.Blobs;

var builder = WebApplication.CreateBuilder(args);

// ---- Configuration: Env Vars + Key Vault ----
var keyVaultUri = builder.Configuration["KeyVaultUri"];
if (!string.IsNullOrEmpty(keyVaultUri) && Uri.TryCreate(keyVaultUri, UriKind.Absolute, out var kvUri))
{
    builder.Configuration.AddAzureKeyVault(kvUri, new DefaultAzureCredential());
    Console.WriteLine($"[Config] Key Vault configured: {kvUri}");
}

// ---- Blob Storage ----
var blobConnectionString = builder.Configuration["BlobConnectionString"] ?? "UseDevelopmentStorage=true";
builder.Services.AddSingleton(_ => new BlobServiceClient(blobConnectionString));

// ---- Controllers only (no Razor Pages) ----
builder.Services.AddControllers();

var app = builder.Build();

app.MapControllers();

app.Run();
