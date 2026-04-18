using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DraftView.Domain.Interfaces.Services;
using Microsoft.Extensions.Configuration;

namespace DraftView.Application.Services;

/// <summary>
/// Generates one-line AI summaries for version changes using the Anthropic API.
/// Returns null on any failure so publishing can proceed without AI output.
/// </summary>
public class AiSummaryService(IConfiguration configuration, HttpClient httpClient) : IAiSummaryService
{
    private const string ApiKeyConfigPath = "Anthropic:ApiKey";
    private const string AnthropicVersionHeaderName = "anthropic-version";
    private const string AnthropicVersionHeaderValue = "2023-06-01";
    private static readonly string ApiUrl = "https://api.anthropic.com/v1/messages";
    private static readonly string ModelId = "claude-sonnet-4-20250514";
    private const int MaxSummaryTokens = 150;

    private const string FirstVersionPromptTemplate =
        "You are helping a fiction author communicate with their beta readers.\n\n" +
        "The author has just published a new section. Write a single sentence (maximum 2 sentences)\n" +
        "that tells the beta readers what this section introduces. Name the characters, locations,\n" +
        "and events that appear. Write in the author's voice — warm, direct, and specific.\n" +
        "Never write generic phrases like \"content was added\" or \"a new section is available\".\n\n" +
        "Section content:\n" +
        "{0}\n\n" +
        "Respond with only the summary sentence. No preamble, no explanation.";

    private const string RevisionPromptTemplate =
        "You are helping a fiction author communicate with their beta readers.\n\n" +
        "The author has revised a section. Write a single sentence (maximum 2 sentences)\n" +
        "that tells the beta readers what changed. Name the characters, locations, and events\n" +
        "that were added, changed, or removed. Write in the author's voice — warm, direct, and specific.\n" +
        "Never write generic phrases like \"content was updated\" or \"changes were made\".\n\n" +
        "Previous version:\n" +
        "{0}\n\n" +
        "Revised version:\n" +
        "{1}\n\n" +
        "Respond with only the summary sentence. No preamble, no explanation.";

    /// <summary>
    /// Generates a one-line summary for a section version.
    /// Returns null when AI generation fails or is skipped.
    /// </summary>
    public async Task<string?> GenerateSummaryAsync(
        string? previousHtml,
        string currentHtml,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(currentHtml))
                return null;

            var apiKey = configuration[ApiKeyConfigPath];
            if (string.IsNullOrWhiteSpace(apiKey))
                return null;

            var prompt = BuildPrompt(previousHtml, currentHtml);
            var bodyJson = JsonSerializer.Serialize(new
            {
                model = ModelId,
                max_tokens = MaxSummaryTokens,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = prompt
                    }
                }
            });

            using var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl)
            {
                Content = new StringContent(bodyJson, Encoding.UTF8, "application/json")
            };

            request.Headers.Add("x-api-key", apiKey);
            request.Headers.Add(AnthropicVersionHeaderName, AnthropicVersionHeaderValue);

            using var response = await httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                return null;

            var responseJson = await response.Content.ReadAsStringAsync(ct);
            using var document = JsonDocument.Parse(responseJson);

            if (!document.RootElement.TryGetProperty("content", out var contentArray))
                return null;

            if (contentArray.ValueKind != JsonValueKind.Array || contentArray.GetArrayLength() == 0)
                return null;

            var firstEntry = contentArray[0];
            if (!firstEntry.TryGetProperty("text", out var textElement))
                return null;

            var summary = textElement.GetString();
            return string.IsNullOrWhiteSpace(summary) ? null : summary.Trim();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Builds the AI prompt for first-version or revision summaries.
    /// </summary>
    private static string BuildPrompt(string? previousHtml, string currentHtml)
    {
        var strippedCurrent = StripHtml(currentHtml);

        if (string.IsNullOrWhiteSpace(previousHtml))
            return string.Format(FirstVersionPromptTemplate, strippedCurrent);

        var strippedPrevious = StripHtml(previousHtml);
        return string.Format(RevisionPromptTemplate, strippedPrevious, strippedCurrent);
    }

    /// <summary>
    /// Strips HTML tags from content before it is sent to AI summarization.
    /// </summary>
    private static string StripHtml(string html)
        => Regex.Replace(html, "<[^>]+>", " ")
            .Replace("  ", " ").Trim();
}
