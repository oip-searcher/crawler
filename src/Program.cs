using Crawler.Options;
using Crawler.Workers;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<CrawlerOptions>(
    builder.Configuration.GetSection(CrawlerOptions.SectionName));

builder.Services.AddHttpClient("crawler")
    .ConfigureHttpClient((sp, client) =>
    {
        var opt = sp.GetRequiredService<IOptions<CrawlerOptions>>().Value;
        client.Timeout = TimeSpan.FromSeconds(opt.HttpTimeoutSeconds);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(opt.UserAgent);
    });

builder.Services.AddHostedService<CrawlerWorker>();

var app = builder.Build();

app.Run();