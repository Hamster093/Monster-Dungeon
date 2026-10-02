/****************************************************
    文件：DialogueCallbackRegistry.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/2 18:54:47
	功能：对话回调注册表（纯C#单例），由业务注册回调逻辑，供 DialogueController 调用
*****************************************************/

using System;
using System.Collections.Generic;

public static class DialogueCallbackRegistry
{
    private static readonly Dictionary<string, Func<DialogueOption, DialogueOptionResult>> _callbacks
        = new Dictionary<string, Func<DialogueOption, DialogueOptionResult>>();

    public static void Register(string key, Func<DialogueOption, DialogueOptionResult> cb)
    {
        if (string.IsNullOrEmpty(key) || cb == null) return;
        _callbacks[key] = cb;
    }

    public static void Unregister(string key) => _callbacks.Remove(key);

    public static bool TryInvoke(string key, DialogueOption option, out DialogueOptionResult result)
    {
        result = null;
        return !string.IsNullOrEmpty(key)
            && _callbacks.TryGetValue(key, out var cb)
            && (result = cb(option)) != null;
    }
}

