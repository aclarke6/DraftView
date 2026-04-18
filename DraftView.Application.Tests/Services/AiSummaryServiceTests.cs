using System.Net;
using System.Net.Http;
using System.Text;
using DraftView.Application.Services;
using Microsoft.Extensions.Configuration;
using Moq;

namespace DraftView.Application.Tests.Services;

/// <summary>
/// Tests for AiSummaryService summary generation orchestration.
/// Covers prompt selection, API request/response handling, and null-on-failure behaviour.
/// Excludes publish-flow integration, which is covered in VersioningService integration phases.
/// </summary>
public class AiSummaryServiceTests
{
    private const string ApiKeyConfigPath = "Anthropic:ApiKey";

    [Fact]
    public async Task GenerateSummaryAsync_WithMissingApiKey_ReturnsNull()
    {
        var configuration = CreateConfiguration(null);
        using var httpClient = new HttpClient(new MockHttpMessageHandler(HttpStatusCode.OK, "{}"));
        var sut = new AiSummaryService(configuration.Object, httpClient);

        var result = await sut.GenerateSummaryAsync("<p>Old</p>", "<p>New</p>");

        Assert.Null(result);
    }

    [Fact]
    public async Task GenerateSummaryAsync_WithEmptyCurrentHtml_ReturnsNull()
    {
        var configuration = CreateConfiguration("test-api-key");
        using var httpClient = new HttpClient(new MockHttpMessageHandler(HttpStatusCode.OK, "{}"));
        var sut = new AiSummaryService(configuration.Object, httpClient);

        var result = await sut.GenerateSummaryAsync("<p>Old</p>", string.Empty);

        Assert.Null(result);
    }

    [Fact]
    public async Task GenerateSummaryAsync_WhenApiThrows_ReturnsNull()
    {
        var configuration = CreateConfiguration("test-api-key");
        using var httpClient = new HttpClient(new ThrowingHttpMessageHandler());
        var sut = new AiSummaryService(configuration.Object, httpClient);

        var result = await sut.GenerateSummaryAsync("<p>Old</p>", "<p>New</p>");

        Assert.Null(result);
    }

    [Fact]
    public async Task GenerateSummaryAsync_WhenApiReturnsError_ReturnsNull()
    {
        var configuration = CreateConfiguration("test-api-key");
        using var httpClient = new HttpClient(new MockHttpMessageHandler(HttpStatusCode.BadRequest, "{\"error\":\"bad_request\"}"));
        var sut = new AiSummaryService(configuration.Object, httpClient);

        var result = await sut.GenerateSummaryAsync("<p>Old</p>", "<p>New</p>");

        Assert.Null(result);
    }

    [Fact]
    public async Task GenerateSummaryAsync_WithNoPreviousHtml_UsesFirstVersionPrompt()
    {
        var configuration = CreateConfiguration("test-api-key");
        var capturingHandler = new CapturingHttpMessageHandler(HttpStatusCode.OK,
            "{\"content\":[{\"text\":\"A summary\"}]}");
        using var httpClient = new HttpClient(capturingHandler);
        var sut = new AiSummaryService(configuration.Object, httpClient);

        _ = await sut.GenerateSummaryAsync(null, "<p>Aria arrives at <strong>Blackmere</strong>.</p>");

        var requestBody = await capturingHandler.GetRequestBodyAsync();
        Assert.Contains("The author has just published a new section.", requestBody);
        Assert.Contains("Section content:", requestBody);
        Assert.Contains("Aria arrives at", requestBody);
        Assert.Contains("Blackmere", requestBody);
        Assert.DoesNotContain("<strong>", requestBody);
        Assert.DoesNotContain("Previous version:", requestBody);
    }

    [Fact]
    public async Task GenerateSummaryAsync_WithPreviousHtml_UsesRevisionPrompt()
    {
        var configuration = CreateConfiguration("test-api-key");
        var capturingHandler = new CapturingHttpMessageHandler(HttpStatusCode.OK,
            "{\"content\":[{\"text\":\"A summary\"}]}");
        using var httpClient = new HttpClient(capturingHandler);
        var sut = new AiSummaryService(configuration.Object, httpClient);

        _ = await sut.GenerateSummaryAsync(
            "<p>Aria enters the city.</p>",
            "<p>Aria and Tomas enter the city square.</p>");

        var requestBody = await capturingHandler.GetRequestBodyAsync();
        Assert.Contains("The author has revised a section.", requestBody);
        Assert.Contains("Previous version:", requestBody);
        Assert.Contains("Revised version:", requestBody);
        Assert.Contains("Aria enters the city.", requestBody);
        Assert.Contains("Aria and Tomas enter the city square.", requestBody);
    }

    [Fact]
    public async Task GenerateSummaryAsync_OnSuccess_ReturnsSummaryText()
    {
        var configuration = CreateConfiguration("test-api-key");
        using var httpClient = new HttpClient(
            new MockHttpMessageHandler(HttpStatusCode.OK,
                "{\"content\":[{\"text\":\"Aria and Tomas confront the fire in Blackmere Square.\"}]}"));
        var sut = new AiSummaryService(configuration.Object, httpClient);

        var result = await sut.GenerateSummaryAsync("<p>Old</p>", "<p>New</p>");

        Assert.Equal("Aria and Tomas confront the fire in Blackmere Square.", result);
    }

    private static Mock<IConfiguration> CreateConfiguration(string? apiKey)
    {
        var configuration = new Mock<IConfiguration>();
        configuration.Setup(c => c[ApiKeyConfigPath]).Returns(apiKey);
        return configuration;
    }

    /// <summary>
    /// Simple HTTP handler that always returns a fixed status code and content.
    /// </summary>
    public class MockHttpMessageHandler(HttpStatusCode statusCode, string content)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            });
    }

    /// <summary>
    /// HTTP handler that records request content and returns a fixed response.
    /// </summary>
    private sealed class CapturingHttpMessageHandler(HttpStatusCode statusCode, string content)
        : HttpMessageHandler
    {
        private readonly string _content = content;
        private readonly HttpStatusCode _statusCode = statusCode;
        private string _requestBody = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _requestBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_content, Encoding.UTF8, "application/json")
            };
        }

        public Task<string> GetRequestBodyAsync() => Task.FromResult(_requestBody);
    }

    /// <summary>
    /// HTTP handler that throws to simulate transport failures.
    /// </summary>
    private sealed class ThrowingHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("Simulated transport failure.");
    }
}
