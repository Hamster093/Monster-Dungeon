/****************************************************
    文件：DialogueCallbackBootstrap.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/2 19:00:30
	功能：对话回调注册器（挂在场景中，Awake 时注册回调，OnDestroy 时注销回调）
*****************************************************/

using System.Collections.Generic;
using UnityEngine;

public class DialogueCallbackBootstrap : MonoBehaviour
{
    void Awake()
    {
        DialogueCallbackRegistry.Register("check_reduce_attribute", OnCheckReduce);
    }
    void OnDestroy()
    {
        DialogueCallbackRegistry.Unregister("check_reduce_attribute");
    }

    private DialogueOptionResult OnCheckReduce(DialogueOption option)
    {
        bool success = UnityEngine.Random.value < 0.5f;
        var result = new DialogueOptionResult();

        result.extraLines = new List<DialogueLine>
        {
            new DialogueLine
            {
                speakerName = "系统",
                iconName    = "liuming", // 或 null，会走 SetIcon 清空
                content     = success
                    ? "【检定成功】刘明的一个主要属性被削减了。"
                    : "【检定失败】削减的是一个无关属性……"
            }
        };

        return result;
    }
}