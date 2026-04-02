using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using HomeFinder.Models;

namespace HomeFinder.Services;

public class OpenAiReviewSummaryService : IAiReviewSummaryService
{
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromDays(1);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<OpenAiReviewSummaryService> _logger;
    private readonly ConcurrentDictionary<int, byte> _inProgress = new();

    public OpenAiReviewSummaryService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        ILogger<OpenAiReviewSummaryService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
    }

    public async Task<ReviewSummaryFetchResult> GetSummaryAsync(
        int apartmentId,
        IReadOnlyCollection<ReviewApartment> reviews,
        CancellationToken cancellationToken = default)
    {
        var sourceReviews = reviews
            .Where(r => !string.IsNullOrWhiteSpace(r.Comment) || r.Rating.HasValue)
            .OrderByDescending(r => r.CreatedAt ?? DateTime.MinValue)
            .ToList();

        if (sourceReviews.Count == 0)
        {
            return new ReviewSummaryFetchResult
            {
                Status = "no_data",
                Message = "No reviews yet."
            };
        }

        var cached = await ReadCacheAsync(apartmentId, cancellationToken);
        bool hasFreshCache = cached?.Summary != null && cached.GeneratedAtUtc >= DateTime.UtcNow.Subtract(CacheLifetime);

        if (hasFreshCache)
        {
            return new ReviewSummaryFetchResult
            {
                Status = "ready",
                Message = "Loaded from daily cache.",
                GeneratedAtUtc = cached!.GeneratedAtUtc,
                Summary = cached.Summary
            };
        }

        var apiKey = _configuration["OpenAI:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new ReviewSummaryFetchResult
            {
                Status = cached?.Summary != null ? "ready" : "disabled",
                Message = cached?.Summary != null
                    ? "Showing cached summary."
                    : "OpenAI API key is not configured.",
                GeneratedAtUtc = cached?.GeneratedAtUtc,
                Summary = cached?.Summary
            };
        }

        if (_inProgress.TryAdd(apartmentId, 0))
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var summary = await GenerateSummaryAsync(sourceReviews, apiKey, CancellationToken.None);
                    if (summary != null)
                    {
                        await WriteCacheAsync(apartmentId, new ReviewSummaryCacheEntry
                        {
                            GeneratedAtUtc = DateTime.UtcNow,
                            Summary = summary
                        }, CancellationToken.None);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to generate OpenAI review summary for apartment {ApartmentId}", apartmentId);
                }
                finally
                {
                    _inProgress.TryRemove(apartmentId, out _);
                }
            });
        }

        return new ReviewSummaryFetchResult
        {
            Status = cached?.Summary != null ? "processing" : "processing",
            Message = cached?.Summary != null
                ? "Refreshing summary in background."
                : "Generating summary in background.",
            GeneratedAtUtc = cached?.GeneratedAtUtc,
            Summary = cached?.Summary
        };
    }

    private async Task<ReviewSummaryViewModel?> GenerateSummaryAsync(
        IReadOnlyCollection<ReviewApartment> reviews,
        string apiKey,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(45);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var model = _configuration["OpenAI:Model"];
        if (string.IsNullOrWhiteSpace(model))
        {
            model = "gpt-4o-mini";
        }

        var reviewText = string.Join("\n\n", reviews.Select((review, index) =>
        {
            var date = review.CreatedAt?.ToString("dd.MM.yyyy") ?? "unknown date";
            var rating = review.Rating.HasValue ? $"{review.Rating.Value}/5" : "no rating";
            var comment = (review.Comment ?? string.Empty).Trim();
            return $"{index + 1}. Date: {date}; Rating: {rating}; Review: {comment}";
        }));

        var payload = new
        {
            model,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content =
                        "You summarize apartment reviews. Use only the provided reviews. Return valid JSON with keys: " +
                        "overview (string), recentTrend (string), ratingBreakdown (string), positiveHighlights (array of strings), negativeHighlights (array of strings). " +
                        "All text values must be in Russian. Keep it concise, factual, and do not hallucinate."
                },
                new
                {
                    role = "user",
                    content =
                        "Summarize the apartment reviews below.\n" +
                        "Rules:\n" +
                        "- overview: 2-4 short sentences\n" +
                        "- recentTrend: one short sentence or empty string\n" +
                        "- ratingBreakdown: compact text if ratings are available, otherwise empty string\n" +
                        "- positiveHighlights: up to 3 short phrases\n" +
                        "- negativeHighlights: up to 3 short phrases\n\n" +
                        $"Reviews:\n{reviewText}"
                }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        using var response = await client.SendAsync(request, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("OpenAI summary request failed: {StatusCode} {Body}", (int)response.StatusCode, raw);
            return null;
        }

        using var outerDoc = JsonDocument.Parse(raw);
        var content = outerDoc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        using var summaryDoc = JsonDocument.Parse(content);
        var root = summaryDoc.RootElement;

        return new ReviewSummaryViewModel
        {
            Overview = GetString(root, "overview"),
            RecentTrend = GetString(root, "recentTrend"),
            RatingBreakdown = GetString(root, "ratingBreakdown"),
            PositiveHighlights = GetStringArray(root, "positiveHighlights"),
            NegativeHighlights = GetStringArray(root, "negativeHighlights")
        };
    }

    private async Task<ReviewSummaryCacheEntry?> ReadCacheAsync(int apartmentId, CancellationToken cancellationToken)
    {
        var path = GetCachePath(apartmentId);
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<ReviewSummaryCacheEntry>(stream, JsonOptions, cancellationToken);
    }

    private async Task WriteCacheAsync(int apartmentId, ReviewSummaryCacheEntry entry, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(GetCachePath(apartmentId));
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(GetCachePath(apartmentId));
        await JsonSerializer.SerializeAsync(stream, entry, JsonOptions, cancellationToken);
    }

    private string GetCachePath(int apartmentId)
    {
        return Path.Combine(_environment.ContentRootPath, "App_Data", "review-summary-cache", $"apartment-{apartmentId}.json");
    }

    private static string GetString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }

    private static List<string> GetStringArray(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return new List<string>();
        }

        return value.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .ToList();
    }

    private sealed class ReviewSummaryCacheEntry
    {
        public DateTime GeneratedAtUtc { get; set; }

        public ReviewSummaryViewModel? Summary { get; set; }
    }
}

