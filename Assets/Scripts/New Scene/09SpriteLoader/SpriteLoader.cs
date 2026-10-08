/****************************************************
    文件：SpriteLoader.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/7 15:09:34
	功能：图标加载服务（Addressables + 缓存 + 异步）+ Sprite 释放
*****************************************************/

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public static class SpriteLoader
{
    private static readonly Dictionary<string, AsyncOperationHandle<Sprite>> _cache = new();

    /// <summary>
    /// 异步加载 Sprite，命中缓存直接返回；失败或地址为空返回 null。
    /// </summary>
    public static async Task<Sprite> LoadAsync(string address)
    {
        if (string.IsNullOrEmpty(address)) return null;

        // 1. 命中缓存
        if (_cache.TryGetValue(address, out var cached)
            && cached.Status == AsyncOperationStatus.Succeeded)
            return cached.Result;

        try
        {
            // 2. 旧句柄未完成/已失败，先释放再重来
            if (cached.IsValid()) Addressables.Release(cached);

            var handle = Addressables.LoadAssetAsync<Sprite>(address);
            _cache[address] = handle;
            await handle.Task;

            if (handle.Status == AsyncOperationStatus.Succeeded)
                return handle.Result;

            Debug.LogWarning($"[SpriteLoader] 加载失败: {address}");
            _cache.Remove(address);
            if (handle.IsValid()) Addressables.Release(handle);
            return null;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SpriteLoader] 加载异常: {address}\n{e}");
            _cache.Remove(address);
            return null;
        }
    }

    /// <summary>释放单个地址</summary>
    public static void Release(string address)
    {
        if (string.IsNullOrEmpty(address)) return;
        if (_cache.TryGetValue(address, out var h))
        {
            if (h.IsValid()) Addressables.Release(h);
            _cache.Remove(address);
        }
    }

    /// <summary>释放全部（切场景 / 退出游戏时调用）</summary>
    public static void ReleaseAll()
    {
        foreach (var h in _cache.Values)
            if (h.IsValid()) Addressables.Release(h);
        _cache.Clear();
    }
}
