using CloudNotes.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CloudNotes.Api.Data;

public class NotesDbContext : DbContext
{
    public NotesDbContext(DbContextOptions<NotesDbContext> options) : base(options) { }

    public DbSet<Note> Notes => Set<Note>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Note>(entity =>
        {
            entity.HasKey(n => n.Id);
            entity.HasIndex(n => n.CreatedAt);
        });
    }
}
