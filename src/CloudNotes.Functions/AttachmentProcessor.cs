using System.Text.RegularExpressions;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace CloudNotes.Functions;

public class AttachmentProcessor
{
    private readonly ILogger<AttachmentProcessor> _logger;

    public AttachmentProcessor(ILogger<AttachmentProcessor> logger)
    {
        _logger = logger;
    }

    [Function(nameof(AttachmentProcessor))]
    public async Task Run(
        [BlobTrigger("attachments/{noteId}/{filename}")] Stream blobStream,
        string noteId,
        string filename,
        FunctionContext context)
    {
        _logger.LogInformation("Processing blob: attachments/{NoteId}/{Filename}, Size: {Size} bytes",
            noteId, filename, blobStream.Length);

        // Only process image files
        var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };
        var ext = Path.GetExtension(filename).ToLowerInvariant();

        if (!imageExtensions.Contains(ext))
        {
            _logger.LogInformation("Skipping non-image file: {Filename}", filename);
            return;
        }

        try
        {
            // Generate thumbnail
            using var image = await Image.LoadAsync(blobStream);
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(200, 200),
                Mode = ResizeMode.Max
            }));

            using var thumbnailStream = new MemoryStream();
            await image.SaveAsJpegAsync(thumbnailStream);
            thumbnailStream.Position = 0;

            // Upload thumbnail to Blob Storage
            var connectionString = Environment.GetEnvironmentVariable("BlobConnectionString")
                ?? Environment.GetEnvironmentVariable("AzureWebJobsStorage")
                ?? "UseDevelopmentStorage=true";

            var blobServiceClient = new BlobServiceClient(connectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient("thumbnails");
            await containerClient.CreateIfNotExistsAsync();

            var thumbnailBlobName = $"{noteId}/{filename}";
            var blobClient = containerClient.GetBlobClient(thumbnailBlobName);
            await blobClient.UploadAsync(thumbnailStream, overwrite: true);

            _logger.LogInformation("Thumbnail uploaded: thumbnails/{BlobName}", thumbnailBlobName);

            // Update SQLite: set ThumbnailUrl
            var dbPath = Environment.GetEnvironmentVariable("DatabasePath");
            if (string.IsNullOrEmpty(dbPath))
            {
                var home = Environment.GetEnvironmentVariable("HOME") ?? Path.GetTempPath();
                dbPath = Path.Combine(home, "data", "cloudnotes.db");
            }

            var connection = new SqliteConnection($"Data Source={dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE Notes
                SET ThumbnailUrl = @thumbnailUrl, UpdatedAt = @updatedAt
                WHERE Id = @noteId";
            command.Parameters.AddWithValue("@thumbnailUrl", blobClient.Uri.ToString());
            command.Parameters.AddWithValue("@updatedAt", DateTime.UtcNow.ToString("O"));
            command.Parameters.AddWithValue("@noteId", noteId);

            var rowsAffected = await command.ExecuteNonQueryAsync();
            _logger.LogInformation("SQLite updated: {Rows} rows affected for note {NoteId}", rowsAffected, noteId);

            await connection.CloseAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process blob: attachments/{NoteId}/{Filename}", noteId, filename);
        }
    }
}
