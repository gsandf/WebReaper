﻿namespace Exoscan.ConfigStorage.Abstract;

public interface IScraperConfigStorage
{
    Task CreateConfigAsync(ScraperConfig config);
    
    Task<ScraperConfig> GetConfigAsync();
}