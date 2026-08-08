using Azure.Storage.Blobs;
using CloudNotes.Api.Data;
using CloudNotes.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CloudNotes.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NotesController : ControllerBase
{
    private readonly NotesDbContext _db;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<NotesController> _logger;
    private const string ContainerName = "attachments";

    public NotesController(NotesDbContext db, BlobServiceClient blobServiceClient, ILogger<NotesController> logger)
    {
        _db = db;
        _blobServiceClient = blobServiceClient;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<List<Note>>> GetNotes([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var notes = await _db.Notes
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        return Ok(notes);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Note>> GetNote(Guid id)
    {
        var note = await _db.Notes.FindAsync(id);
        if (note is null) return NotFound();
        return Ok(note);
    }

    [HttpPost]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10MB
    public async Task<ActionResult<Note>> CreateNote(
        [FromForm] string title,
        [FromForm] string content,
        IFormFile? attachment)
    {
        var note = new Note
        {
            Title = title,
            Content = content
        };

        if (attachment is { Length: > 0 })
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
            await containerClient.CreateIfNotExistsAsync();

            var blobName = $"{note.Id}/{attachment.FileName}";
            var blobClient = containerClient.GetBlobClient(blobName);

            await using var stream = attachment.OpenReadStream();
            await blobClient.UploadAsync(stream, overwrite: true);

            note.AttachmentUrl = blobClient.Uri.ToString();
            note.OriginalFileName = attachment.FileName;
            note.FileSize = attachment.Length;
        }

        _db.Notes.Add(note);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Note {NoteId} created successfully", note.Id);

        return CreatedAtAction(nameof(GetNote), new { id = note.Id }, note);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteNote(Guid id)
    {
        var note = await _db.Notes.FindAsync(id);
        if (note is null) return NotFound();

        // Delete blobs if exist
        if (!string.IsNullOrEmpty(note.AttachmentUrl))
        {
            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
                var blobName = $"{note.Id}/{note.OriginalFileName}";
                await containerClient.DeleteBlobIfExistsAsync(blobName);
                await containerClient.DeleteBlobIfExistsAsync($"{note.Id}/thumb_{note.OriginalFileName}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete blobs for note {NoteId}", id);
            }
        }

        _db.Notes.Remove(note);
        await _db.SaveChangesAsync();

        return NoContent();
    }
}
