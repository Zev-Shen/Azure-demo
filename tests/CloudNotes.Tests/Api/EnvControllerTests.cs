using CloudNotes.Api.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;

namespace CloudNotes.Tests.Api;

public class EnvControllerTests
{
    [Fact]
    public void GetEnvironmentVariables_ReturnsFilteredList()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "AppName", "CloudNotes" },
                { "Environment", "Test" },
                { "ASPNETCORE_ENVIRONMENT", "Development" },
                { "MySecretKey", "secret-value" },
                { "ConnectionString", "conn-str-value" }
            })
            .Build();

        var loggerMock = new Mock<ILogger<EnvController>>();
        var controller = new EnvController(config, loggerMock.Object);

        var result = controller.GetEnvironmentVariables();
        var okResult = Assert.IsType<OkObjectResult>(result);

        var json = JsonSerializer.Serialize(okResult.Value);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.GetProperty("count").GetInt32() > 0);
        var vars = root.GetProperty("variables").EnumerateArray();
        bool hasAppName = false;
        bool hasSecretKey = false;
        bool hasConnString = false;
        foreach (var v in vars)
        {
            var key = v.GetProperty("Key").GetString();
            if (key == "AppName") hasAppName = true;
            if (key == "MySecretKey") hasSecretKey = true;
            if (key == "ConnectionString") hasConnString = true;
        }
        Assert.True(hasAppName);
        Assert.False(hasSecretKey);
        Assert.False(hasConnString);
    }

    [Fact]
    public void GetEnvironmentVariables_TruncatesLongValues()
    {
        var longValue = new string('x', 100);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { { "LongVar", longValue } })
            .Build();

        var loggerMock = new Mock<ILogger<EnvController>>();
        var controller = new EnvController(config, loggerMock.Object);

        var result = controller.GetEnvironmentVariables();
        var okResult = Assert.IsType<OkObjectResult>(result);

        var json = JsonSerializer.Serialize(okResult.Value);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var vars = root.GetProperty("variables").EnumerateArray();

        string? longVarValue = null;
        foreach (var v in vars)
        {
            if (v.GetProperty("Key").GetString() == "LongVar")
            {
                longVarValue = v.GetProperty("Value").GetString();
                break;
            }
        }
        Assert.NotNull(longVarValue);
        Assert.EndsWith("...", longVarValue);
        Assert.Equal(50, longVarValue.Length);
    }

    [Fact]
    public async Task GetSecretNames_WithoutKeyVault_ReturnsMessage()
    {
        var config = new ConfigurationBuilder().Build();
        var loggerMock = new Mock<ILogger<EnvController>>();
        var controller = new EnvController(config, loggerMock.Object);

        var result = await controller.GetSecretNames();
        var okResult = Assert.IsType<OkObjectResult>(result);

        var json = JsonSerializer.Serialize(okResult.Value);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var msg = root.GetProperty("message").GetString();
        Assert.Contains("not configured", msg!);
    }
}
