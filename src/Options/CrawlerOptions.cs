namespace Crawler.Options;

public sealed class CrawlerOptions
{
    public const string SectionName = "Crawler";

    public string UrlsFile { get; set; } = "urls.json";
    public string IndexPath { get; set; } = "index.txt";
    public string OutputDir { get; set; } = "pages";

    public int MaxPages { get; set; } = 100;
    public int DelayMs { get; set; } = 50;

    public int HttpTimeoutSeconds { get; set; } = 10;
    public string UserAgent { get; set; } =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36";
}