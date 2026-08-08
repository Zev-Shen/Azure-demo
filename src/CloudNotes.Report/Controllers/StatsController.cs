using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Mvc;

namespace CloudNotes.Report.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StatsController : ControllerBase
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<StatsController> _logger;

    public StatsController(BlobServiceClient blobServiceClient, ILogger<StatsController> logger)
    {
        _blobServiceClient = blobServiceClient;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetStats()
    {
        try
        {
            var containers = new[] { "attachments", "thumbnails" };
            var stats = new Dictionary<string, object>();
            long totalAttachmentCount = 0;
            long totalAttachmentSize = 0;

            foreach (var containerName in containers)
            {
                try
                {
                    var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
                    if (!await containerClient.ExistsAsync())
                    {
                        stats[containerName] = new { exists = false, blobCount = 0, totalSize = 0 };
                        continue;
                    }

                    long blobCount = 0;
                    long containerSize = 0;

                    await foreach (var blobItem in containerClient.GetBlobsAsync())
                    {
                        blobCount++;
                        if (blobItem.Properties.ContentLength.HasValue)
                            containerSize += blobItem.Properties.ContentLength.Value;
                    }

                    stats[containerName] = new { exists = true, blobCount, totalSize = containerSize };

                    if (containerName == "attachments")
                    {
                        totalAttachmentCount = blobCount;
                        totalAttachmentSize = containerSize;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to read container: {ContainerName}", containerName);
                    stats[containerName] = new { exists = false, error = ex.Message };
                }
            }

            // Count unique notes (by folder prefix in attachments)
            long uniqueNotes = 0;
            try
            {
                var attContainer = _blobServiceClient.GetBlobContainerClient("attachments");
                if (await attContainer.ExistsAsync())
                {
                    var folders = new HashSet<string>();
                    await foreach (var blobItem in attContainer.GetBlobsByHierarchyAsync(BlobTraits.None, BlobStates.None, null, "/", CancellationToken.None))
                    {
                        if (blobItem.IsPrefix)
                            folders.Add(blobItem.Prefix);
                    }
                    uniqueNotes = folders.Count;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to count unique notes");
            }

            return Ok(new
            {
                timestamp = DateTime.UtcNow,
                uniqueNotes,
                totalAttachmentCount,
                totalAttachmentSize,
                totalAttachmentSizeFormatted = FormatSize(totalAttachmentSize),
                containers = stats
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stats endpoint failed");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            service = "CloudNotes.Report",
            runtime = Environment.Version.ToString()
        });
    }

    private static string FormatSize(long bytes) =>
        bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
            < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
            _ => $"{bytes / (1024.0 * 1024 * 1024):F1} GB"
        };
}




