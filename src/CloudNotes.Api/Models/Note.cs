using System.ComponentModel.DataAnnotations;

namespace CloudNotes.Api.Models;

public class Note
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(5000)]
    public string Content { get; set; } = string.Empty;

    [StringLength(1024)]
    public string? AttachmentUrl { get; set; }

    [StringLength(1024)]
    public string? ThumbnailUrl { get; set; }

    public string? OriginalFileName { get; set; }

    public long? FileSize { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
