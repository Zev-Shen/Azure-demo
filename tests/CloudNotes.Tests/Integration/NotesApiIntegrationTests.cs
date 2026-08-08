using System.Net.Http.Json;
using CloudNotes.Api.Models;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CloudNotes.Tests.Integration;

public class NotesApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public NotesApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetNotes_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/notes");
        response.EnsureSuccessStatusCode();
        var notes = await response.Content.ReadFromJsonAsync<List<Note>>();
        Assert.NotNull(notes);
    }

    [Fact]
    public async Task CreateNote_WithoutAttachment_ReturnsCreated()
    {
        using var form = new MultipartFormDataContent
        {
            { new StringContent("Integration Test"), "title" },
            { new StringContent("Test Content"), "content" }
        };

        var response = await _client.PostAsync("/api/notes", form);
        response.EnsureSuccessStatusCode();

        var note = await response.Content.ReadFromJsonAsync<Note>();
        Assert.NotNull(note);
        Assert.Equal("Integration Test", note!.Title);
    }

    [Fact]
    public async Task GetEnv_ReturnsVariables()
    {
        var response = await _client.GetAsync("/api/env");
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task GetHealth_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/health");
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task HomePage_ReturnsHtml()
    {
        var response = await _client.GetAsync("/");
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("CloudNotes", content);
    }

    [Fact]
    public async Task GetNonExistentNote_Returns404()
    {
        var response = await _client.GetAsync($"/api/notes/{Guid.NewGuid()}");
        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }
}
