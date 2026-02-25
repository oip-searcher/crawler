using System.Text.Json;
using Crawler.Options;
using HtmlAgilityPack;
using Microsoft.Extensions.Options;

namespace Crawler.Workers;

public sealed class CrawlerWorker : BackgroundService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<CrawlerOptions> _options;
    private readonly ILogger<CrawlerWorker> _logger;
    private readonly IHostApplicationLifetime _lifetime;

    public CrawlerWorker(
        IHttpClientFactory httpClientFactory,
        IOptions<CrawlerOptions> options,
        ILogger<CrawlerWorker> logger,
        IHostApplicationLifetime lifetime)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
        _lifetime = lifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opt = _options.Value;
        var client = _httpClientFactory.CreateClient("crawler");

        _logger.LogInformation("Starting crawler...");

        try
        {
            Directory.CreateDirectory(opt.OutputDir);

            var urls = LoadUrls(opt.UrlsFile);

            var indexLines = new List<string>(capacity: Math.Min(urls.Count, opt.MaxPages));
            var fileIndex = 1;

            foreach (var url in urls)
            {
                if (stoppingToken.IsCancellationRequested) break;
                if (fileIndex > opt.MaxPages) break;

                _logger.LogInformation("[{Current}/{Max}] Downloading {Url}", fileIndex, opt.MaxPages, url);

                try
                {
                    var html = await client.GetStringAsync(url, stoppingToken);

                    var cleanedHtml = CleanHtml(html);

                    var filePath = Path.Combine(opt.OutputDir, $"{fileIndex}.txt");
                    await File.WriteAllTextAsync(filePath, cleanedHtml, stoppingToken);

                    indexLines.Add($"{fileIndex}\t{url}");
                    fileIndex++;

                    if (opt.DelayMs > 0)
                        await Task.Delay(opt.DelayMs, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    _logger.LogWarning("Cancellation requested. Stopping.");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error on {Url}", url);
                }
            }

            await File.WriteAllLinesAsync(opt.IndexPath, indexLines, stoppingToken);

            _logger.LogInformation("Crawling completed. {Count} pages saved.", indexLines.Count);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogWarning("Cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Crawler failed");
        }
        finally
        {
            // Важно: worker по умолчанию "вечный".
            // Мы делаем один проход и завершаем приложение (как консольная версия).
            _lifetime.StopApplication();
        }
    }

    private static List<string> LoadUrls(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}");

        var json = File.ReadAllText(filePath);
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("urls", out var urlsElement))
            throw new Exception("JSON must contain 'urls' property");

        return urlsElement.EnumerateArray()
            .Select(item => item.GetString())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!.Trim())
            .ToList();
    }

    /// <summary>
    /// Удаляет script/style/link/noscript/iframe/meta, оставляет структуру HTML.
    /// </summary>
    private static string CleanHtml(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var tagsToRemove = new[] { "script", "style", "link", "noscript", "iframe", "meta" };

        foreach (var tagName in tagsToRemove)
        {
            var nodes = doc.DocumentNode.Descendants(tagName).ToList();
            foreach (var node in nodes)
                node.Remove();
        }

        var cleanedHtml = doc.DocumentNode.OuterHtml;

        var lines = cleanedHtml.Split('\n');
        var nonEmptyLines = lines
            .Select(l => l.TrimEnd('\r'))
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToArray();

        return string.Join("\n", nonEmptyLines);
    }
}