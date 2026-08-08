using Microsoft.AspNetCore.Mvc;

namespace CloudNotes.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<HealthController> _logger;

    public HealthController(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<HealthController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetHealth()
    {
        var result = new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            appName = _configuration["AppName"] ?? "CloudNotes",
            environment = _configuration["Environment"] ?? "Local",
            reportService = "not_configured"
        };

        // Try calling Report Service (Container Apps) if URL is configured
        var reportUrl = _configuration["ReportServiceUrl"];
        if (!string.IsNullOrEmpty(reportUrl))
        {
            try
            {
                var client = _httpClientFactory.CreateClient("ReportService");
                var response = await client.GetAsync($"{reportUrl}/health");
                result = new
                {
                    status = "healthy",
                    timestamp = DateTime.UtcNow,
                    appName = _configuration["AppName"] ?? "CloudNotes",
                    environment = _configuration["Environment"] ?? "Local",
                    reportService = response.IsSuccessStatusCode ? "healthy" : "unhealthy"
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Report Service health check failed");
                result = new
                {
                    status = "degraded",
                    timestamp = DateTime.UtcNow,
                    appName = _configuration["AppName"] ?? "CloudNotes",
                    environment = _configuration["Environment"] ?? "Local",
                    reportService = "unreachable"
                };
            }
        }

        return Ok(result);
    }
}
