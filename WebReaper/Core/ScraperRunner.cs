using Microsoft.Extensions.Logging;
using WebReaper.Domain;
using WebReaper.Exceptions;
using WebReaper.Infra;
using WebReaper.Scheduler.Abstract;
using WebReaper.Spider.Abstract;

namespace WebReaper.Core;

public class ScraperRunner
{
    public ScraperConfig Config { get; init; }
    public string GlobalId { get; }
    public IScheduler Scheduler { get; init; }
    public ISpider Spider { get; init; }
    public ILogger Logger { get; init; }
      
    public ScraperRunner(
        string globalId,
        ScraperConfig config,
        IScheduler jobScheduler,
        ISpider spider,
        ILogger logger)
    {
        GlobalId = globalId;
        Scheduler = jobScheduler;
        Config = config;
        Spider = spider;
        Logger = logger;
    }

    public async Task Run(int parallelismDegree, CancellationToken cancellationToken = default)
    {
        await Scheduler.AddAsync(new Job(
            GlobalId,
            Config.ParsingScheme!,
            Config.StartUrl!,
            Config.LinkPathSelectors,
            Config.StartPageType,
            Config.Script), cancellationToken);

        var options = new ParallelOptions { MaxDegreeOfParallelism = parallelismDegree };

        try
        {
            await Parallel.ForEachAsync(Scheduler.GetAllAsync(cancellationToken), options, async (job, token) =>
            {
                try
                {
                    var newJobs = await Executor.RetryAsync(() => Spider.CrawlAsync(job, cancellationToken));
                    await Scheduler.AddAsync(newJobs, cancellationToken);

                    int[] ids = new[] { 1, 2, 3, 4, 5 };
                    await Parallel.ForEachAsync(ids, async (i,token) => await Task.Delay(1));
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed during scraping {job}", job.ToString());
                }
            });
        }
        catch (PageCrawlLimitException ex)
        {
            Logger.LogWarning(ex, "Shutting down due to page crawl limit {limit}", ex.PageCrawlLimit);
            return;
        }
        catch (TaskCanceledException ex)
        {
            Logger.LogWarning(ex, "Shutting down due to cancellation");
            return;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Shutting down due to unhandled exception");
            throw;
        }
    }
}