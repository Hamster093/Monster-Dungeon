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
    // 缓存加载结果 Task（并发安全，同地址只加载一次）
    private static readonly Dictionary<string, Task<Sprite>> _tasks = new();
    // 保留 handle 引用，用于 Release
    private static readonly Dictionary<string, AsyncOperationHandle<Sprite>> _handles = new();

    public static Task<Sprite> LoadAsync(string address)
    {
        if (string.IsNullOrEmpty(address))
            return Task.FromResult<Sprite>(null);

        if (_tasks.TryGetValue(address, out var existing))
            return existing;

        var task = LoadInternal(address);
        _tasks[address] = task;
        return task;
    }

    private static async Task<Sprite> LoadInternal(string address)
    {
        try
        {
            var handle = Addressables.LoadAssetAsync<Sprite>(address);
            _handles[address] = handle;
            await handle.Task;

            if (handle.Status == AsyncOperationStatus.Succeeded)
                return handle.Result;

            Debug.LogWarning($"[SpriteLoader] 加载失败: {address}");
            return null;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SpriteLoader] 加载异常: {address}\n{e}");
            return null;
        }
    }

    public static void Release(string address)
    {
        if (string.IsNullOrEmpty(address)) return;

        if (_handles.TryGetValue(address, out var h))
        {
            if (h.IsValid()) Addressables.Release(h);
            _handles.Remove(address);
        }
        _tasks.Remove(address);
    }

    public static void ReleaseAll()
    {
        foreach (var h in _handles.Values)
            if (h.IsValid()) Addressables.Release(h);
        _handles.Clear();
        _tasks.Clear();
    }
}