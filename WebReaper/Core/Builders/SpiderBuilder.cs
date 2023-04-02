﻿using System.Net;
using WebReaper.Spider.Concrete;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using PuppeteerSharp.Input;
using WebReaper.ConfigStorage.Abstract;
using WebReaper.ConfigStorage.Concrete;
using WebReaper.CookieStorage.Abstract;
using WebReaper.CookieStorage.Concrete;
using WebReaper.Domain;
using WebReaper.HttpRequests.Concrete;
using WebReaper.LinkTracker.Abstract;
using WebReaper.LinkTracker.Concrete;
using WebReaper.Loaders.Abstract;
using WebReaper.Loaders.Concrete;
using WebReaper.Parser.Abstract;
using WebReaper.Parser.Concrete;
using WebReaper.Proxy.Abstract;
using WebReaper.Sinks.Abstract;
using WebReaper.Sinks.Concrete;
using WebReaper.Sinks.Models;
using WebReaper.Spider.Abstract;

namespace WebReaper.Core.Builders;

public class SpiderBuilder
{
    private Func<Metadata,JObject,Task> PostProcessor { get; set; }
    
    private List<IScraperSink> Sinks { get; } = new();

    private ILogger Logger { get; set; } = NullLogger.Instance;

    private ILinkParser LinkParser { get; set; } = new LinkParserByCssSelector();

    private IScraperConfigStorage ScraperConfigStorage { get; set; } = new InMemoryScraperConfigStorage();

    private IVisitedLinkTracker SiteLinkTracker { get; set; } = new InMemoryVisitedLinkTracker();

    private IContentParser? ContentParser { get; set; }

    private IStaticPageLoader? StaticPageLoader { get; set; }

    private IBrowserPageLoader? BrowserPageLoader { get; set; }

    private IProxyProvider? ProxyProvider { get; set; }

    private CookieContainer Cookies { get; set; } = new();

    private ICookiesStorage CookieStorage { get; set; } = new InMemoryCookieStorage();

    protected event Action<ParsedData> ScrapedData;

    private readonly List<string> _urlBlackList = new();

    public SpiderBuilder WithLogger(ILogger logger)
    {
        Logger = logger;
        return this;
    }

    public SpiderBuilder WithLinkTracker(IVisitedLinkTracker linkTracker)
    {
        SiteLinkTracker = linkTracker;
        return this;
    }
    
    public SpiderBuilder WithConfigStorage(IScraperConfigStorage scraperConfigStorage)
    {
        ScraperConfigStorage = scraperConfigStorage;
        return this;
    }
    
    public SpiderBuilder WithFileConfigStorage(string fileName)
    {
        ScraperConfigStorage = new FileScraperConfigStorage(fileName);
        return this;
    }
    
    public SpiderBuilder WithMongoDbConfigStorage(
        string connectionString,
        string databaseName,
        string collectionName,
        string configId,
        ILogger logger)
    {
        ScraperConfigStorage = new MongoDbScraperConfigStorage(connectionString, databaseName, collectionName, configId, logger);
        return this;
    }
    
    public SpiderBuilder WithRedisConfigStorage(string connectionString, string key)
    {
        ScraperConfigStorage = new RedisScraperConfigStorage(connectionString, key, Logger);
        return this;
    }

    public SpiderBuilder SetCookies(Action<CookieContainer> setCookies)
    {
        setCookies(Cookies);
        return this;
    }

    public SpiderBuilder AddSink(IScraperSink sink)
    {
        Sinks.Add(sink);
        return this;
    }
    
    public SpiderBuilder WriteToConsole() => AddSink(new ConsoleSink());

    public SpiderBuilder AddSubscription(Action<ParsedData> eventHandler)
    {
        ScrapedData += eventHandler;
        return this;
    }

    public SpiderBuilder WriteToJsonFile(string filePath) => AddSink(new JsonLinesFileSink(filePath));

    public SpiderBuilder WriteToCosmosDb(
        string endpointUrl,
        string authorizationKey,
        string databaseId,
        string containerId)
    {
        return AddSink(new CosmosSink(endpointUrl, authorizationKey, databaseId, containerId, Logger)); // possible NullLogger here
    }
    
    public SpiderBuilder WriteToRedis(string connectionString, string redisKey)
    {
        return AddSink(new RedisSink(connectionString, redisKey, Logger)); // possible NullLogger here
    }

    public SpiderBuilder WithStaticPageLoader(IStaticPageLoader staticPageLoader)
    {
        StaticPageLoader = staticPageLoader;
        return this;
    }

    public SpiderBuilder WithBrowserPageLoader(IBrowserPageLoader browserPageLoader)
    {
        BrowserPageLoader = browserPageLoader;
        return this;
    }

    public SpiderBuilder WithProxies(IProxyProvider proxyProvider)
    {
        ProxyProvider = proxyProvider;
        return this;
    }

    public SpiderBuilder WriteToCsvFile(string filePath) => AddSink(new CsvFileSink(filePath));
    
    public SpiderBuilder WithRedisCookieStorage(string connectionString, string redisKey)
    {
        CookieStorage = new RedisCookieStorage(connectionString, redisKey, Logger);
        return this;
    }
    
    public SpiderBuilder WithMongoDbCookieStorage(
        string connectionString,
        string databaseName,
        string collectionName,
        string cookieCollectionId,
        ILogger logger)
    {
        CookieStorage = new MongoDbCookieStorage(connectionString, databaseName, collectionName, cookieCollectionId, logger);
        return this;
    }
    
    public SpiderBuilder WithCookieStorage(ICookiesStorage cookiesStorage)
    {
        CookieStorage = cookiesStorage;
        return this;
    }
    
    public void PostProcess(Func<Metadata, JObject, Task> callback)
    {
        this.PostProcessor = callback;
    }

    public ISpider Build()
    {
        // default implementations
        ContentParser ??= new AngleSharpContentParser(Logger);

        if (ProxyProvider != null)
        {
            BrowserPageLoader ??= new PuppeteerPageLoaderWithProxies(Logger, ProxyProvider, CookieStorage);

            var pageRequester = new RotatingProxyPageRequester(ProxyProvider);

            StaticPageLoader ??= new HttpStaticPageLoader(pageRequester, CookieStorage, Logger);
        }
        else
        {
            var pageRequester = new PageRequester();
            
            StaticPageLoader ??= new HttpStaticPageLoader(pageRequester, CookieStorage, Logger);
            BrowserPageLoader ??= new PuppeteerPageLoader(Logger, CookieStorage);
        }

        CookieStorage.AddAsync(Cookies);

        var spider = new Spider.Concrete.Spider(
            Sinks,
            LinkParser,
            ContentParser,
            SiteLinkTracker,
            StaticPageLoader,
            BrowserPageLoader,
            ScraperConfigStorage,
            Logger);

        spider.ScrapedData += ScrapedData;
        spider.PostProcessor += PostProcessor;

        return spider;
    }
}