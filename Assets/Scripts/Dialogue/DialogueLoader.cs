/****************************************************
    文件：DialogueLoader.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/9/29 18:44:49
	功能：对话数据加载器
*****************************************************/

using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public static class DialogueLoader
{
    /// <summary>
    /// 异步加载并解析对话配置 (支持 Addressables)
    /// </summary>
    public static async Task<DialogueData> LoadDialogueData(string addressableKey)
    {
        var handle = Addressables.LoadAssetAsync<TextAsset>(addressableKey);
        await handle.Task;

        if (handle.Status != AsyncOperationStatus.Succeeded)
        {
            Debug.LogError($"[DialogueLoader] 加载失败: {addressableKey}");
            return null;
        }

        try
        {
            DialogueData data = JsonUtility.FromJson<DialogueData>(handle.Result.text);
            Addressables.Release(handle);
            return data;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[DialogueLoader] 解析 JSON 失败: {addressableKey}\n{e}");
            Addressables.Release(handle);
            return null;
        }
    }
}
