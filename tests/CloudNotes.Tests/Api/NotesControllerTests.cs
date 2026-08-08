using System.Text;
using Azure.Storage.Blobs;
using CloudNotes.Api.Controllers;
using CloudNotes.Api.Data;
using CloudNotes.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace CloudNotes.Tests.Api;

public class NotesControllerTests : IDisposable
{
    private readonly NotesDbContext _db;
    private readonly NotesController _controller;
    private readonly Mock<BlobServiceClient> _blobMock;
    private readonly Mock<ILogger<NotesController>> _loggerMock;

    public NotesControllerTests()
    {
        var options = new DbContextOptionsBuilder<NotesDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        _db = new NotesDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _blobMock = new Mock<BlobServiceClient>();
        _loggerMock = new Mock<ILogger<NotesController>>();
        _controller = new NotesController(_db, _blobMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GetNotes_ReturnsEmptyList_WhenNoNotes()
    {
        var result = await _controller.GetNotes();
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var notes = Assert.IsType<List<Note>>(okResult.Value);
        Assert.Empty(notes);
    }

    [Fact]
    public async Task CreateNote_WithTitle_ReturnsCreatedNote()
    {
        var result = await _controller.CreateNote("Test Title", "Test Content", null);
        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var note = Assert.IsType<Note>(created.Value);
        Assert.Equal("Test Title", note.Title);
        Assert.NotEqual(Guid.Empty, note.Id);
    }

    [Fact]
    public async Task GetNote_NonExistentId_ReturnsNotFound()
    {
        var result = await _controller.GetNote(Guid.NewGuid());
        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task CreateAndGetNote_RoundTrip()
    {
        var created = await _controller.CreateNote("Roundtrip", "Content", null);
        var createdResult = Assert.IsType<CreatedAtActionResult>(created.Result);
        var createdNote = Assert.IsType<Note>(createdResult.Value);

        var getResult = await _controller.GetNote(createdNote.Id);
        var okGetResult = Assert.IsType<OkObjectResult>(getResult.Result);
        var note = Assert.IsType<Note>(okGetResult.Value);
        Assert.Equal("Roundtrip", note.Title);
    }

    [Fact]
    public async Task DeleteNote_RemovesFromDb()
    {
        var created = await _controller.CreateNote("To Delete", "Content", null);
        var createdResult = Assert.IsType<CreatedAtActionResult>(created.Result);
        var note = Assert.IsType<Note>(createdResult.Value);

        var deleteResult = await _controller.DeleteNote(note.Id);
        Assert.IsType<NoContentResult>(deleteResult);

        var getResult = await _controller.GetNote(note.Id);
        Assert.IsType<NotFoundResult>(getResult.Result);
    }

    [Fact]
    public async Task GetNotes_Pagination_ReturnsCorrectPage()
    {
        for (int i = 1; i <= 25; i++)
            await _controller.CreateNote($"Note {i}", "Content", null);

        var result = await _controller.GetNotes(page: 2, pageSize: 10);
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var notes = Assert.IsType<List<Note>>(okResult.Value);
        Assert.Equal(10, notes.Count);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }
}



