using Microsoft.Extensions.Caching.Distributed;
using Newtonsoft.Json;
using Scadex.Core.Utils.CriticalData;
using Scadex.Core.Utils.ResultPattern;
using System.Text;

namespace Scadex.Core.Utils.Caching;

public class CacheService : ICacheService
{
    private readonly IDistributedCache _distributedCache;
    private readonly CacheSettings _cacheSettings;
    public CacheService(IDistributedCache distributedCache, CacheSettings cacheSettings)
    {
        _distributedCache = distributedCache;
        _cacheSettings = cacheSettings;
    }

    public Result<string> Get(string cacheKey)
    {
        byte[]? cachedData = _distributedCache.Get(cacheKey);
        if (cachedData != null)
        {
            var response = Encoding.UTF8.GetString(cachedData);
            if (string.IsNullOrWhiteSpace(response)) 
                return Result<string>.NotFound("Encoding result of the cached data empty");

            return Result<string>.Success(response);
        }
        else
        {
            return Result<string>.NotFound("Cached data empty");
        }
    }

    public Result Add<TData>(string cacheKey, TData data, string[]? cacheGroupKeys = default)
    {
        DistributedCacheEntryOptions cacheEntryOptions = new DistributedCacheEntryOptions()
        {
            SlidingExpiration = TimeSpan.FromMinutes(_cacheSettings.SlidingExpirationMinutes),
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_cacheSettings.AbsoluteExpirationMinutes)
        };

        string serializedData = JsonConvert.SerializeObject(data, new JsonSerializerSettings
        {
            Formatting = Formatting.None,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            MaxDepth = 7,
            ContractResolver = new IgnoreCriticalDataResolver()
        });
        var bytedData = Encoding.UTF8.GetBytes(serializedData);

        _distributedCache.Set(cacheKey, bytedData, cacheEntryOptions);
        if (cacheGroupKeys != null && cacheGroupKeys.Any())
        {
            return AddCacheKeyToGroups(cacheKey, cacheGroupKeys, cacheEntryOptions);
        }
        return Result.Success();
    }

    public Result Remove(string cacheKey)
    {
        if (string.IsNullOrWhiteSpace(cacheKey))
            return Result.Failure("Cache key argument is empty or null");

        _distributedCache.Remove(cacheKey);
        return Result.Success();
    }

    public Result RemoveCacheGroups(string[] cacheGroupKeyList)
    {
        foreach (string cacheGroupKey in cacheGroupKeyList)
        {
            byte[]? keyListFromCache = _distributedCache.Get(cacheGroupKey);
            _distributedCache.Remove(cacheGroupKey);

            if (keyListFromCache == null) 
                continue;

            string stringKeyList = Encoding.UTF8.GetString(keyListFromCache);
            HashSet<string>? keyListInGroup = JsonConvert.DeserializeObject<HashSet<string>>(stringKeyList);
            if (keyListInGroup != null)
            {
                foreach (var key in keyListInGroup)
                {
                    _distributedCache.Remove(key);
                }
            }
        }
        return Result.Success();
    }

    private Result AddCacheKeyToGroups(string cacheKey, string[] cacheGroupKeys, DistributedCacheEntryOptions groupCacheEntryOptions)
    {
        foreach (string cacheGroupKey in cacheGroupKeys)
        {
            HashSet<string>? keyListInGroup;
            byte[]? cachedGroupData = _distributedCache.Get(cacheGroupKey);
            if (cachedGroupData != null)
            {
                keyListInGroup = JsonConvert.DeserializeObject<HashSet<string>>(Encoding.UTF8.GetString(cachedGroupData));
                if (keyListInGroup != null && !keyListInGroup.Contains(cacheKey))
                {
                    keyListInGroup.Add(cacheKey);
                }
            }
            else
            {
                keyListInGroup = [cacheKey];
            }
            string serializedData = JsonConvert.SerializeObject(keyListInGroup, new JsonSerializerSettings
            {
                Formatting = Formatting.None,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                MaxDepth = 7,
                ContractResolver = new IgnoreCriticalDataResolver()
            });
            byte[]? bytedKeyList = Encoding.UTF8.GetBytes(serializedData);
            if (bytedKeyList != null)
                _distributedCache.Set(cacheGroupKey, bytedKeyList, groupCacheEntryOptions);
        }
        return Result.Success();
    }
}