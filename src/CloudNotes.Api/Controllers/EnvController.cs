using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.AspNetCore.Mvc;

namespace CloudNotes.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EnvController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EnvController> _logger;

    public EnvController(IConfiguration configuration, ILogger<EnvController> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult GetEnvironmentVariables()
    {
        var vars = new Dictionary<string, string?>();
        var excludeKeys = new[] { "CONNECTIONSTRING", "CONNECTION_STRING", "KEY", "SECRET", "PASSWORD", "TOKEN" };

        foreach (var key in _configuration.AsEnumerable())
        {
            if (key.Value is null) continue;
            var upperKey = key.Key.ToUpperInvariant();
            if (excludeKeys.Any(e => upperKey.Contains(e))) continue;

            vars[key.Key] = key.Value.Length > 50
                ? key.Value[..47] + "..."
                : key.Value;
        }

        return Ok(new
        {
            count = vars.Count,
            variables = vars.OrderBy(v => v.Key).ToList()
        });
    }

    [HttpGet("secrets")]
    public async Task<IActionResult> GetSecretNames()
    {
        var keyVaultUri = _configuration["KeyVaultUri"];
        if (string.IsNullOrEmpty(keyVaultUri) || !Uri.TryCreate(keyVaultUri, UriKind.Absolute, out var kvUri))
        {
            return Ok(new { message = "Key Vault is not configured. Set KeyVaultUri in env vars to enable.", secrets = Array.Empty<object>() });
        }

        try
        {
            var secretClient = new SecretClient(kvUri, new DefaultAzureCredential());
            var secrets = new List<object>();
            await foreach (var secretProps in secretClient.GetPropertiesOfSecretsAsync())
            {
                secrets.Add(new
                {
                    name = secretProps.Name,
                    enabled = secretProps.Enabled,
                    createdOn = secretProps.CreatedOn,
                    expiresOn = secretProps.ExpiresOn
                });
            }

            return Ok(new { count = secrets.Count, secrets });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to list Key Vault secrets");
            return Ok(new { message = $"Key Vault access failed: {ex.Message}", secrets = Array.Empty<object>() });
        }
    }
}
