/****************************************************
    文件：TypewriterText.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026-09-21 12:20:23
	功能：对话框打字机效果
*****************************************************/

using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class TypewriterTextLegacy : MonoBehaviour
{
    [Header("UI 引用")]
    public Text text;

    [Header("打字参数")]
    [Tooltip("每个字符显示的间隔时间（秒），值越小打字越快")]
    public float charInterval = 0.05f;

    private Coroutine typing;
    private string fullText;
    // 用 StringBuilder 追加字符，避免反复创建新字符串
    private StringBuilder sb = new StringBuilder();

    public bool IsTyping { get; private set; }
    
    public void Play(string content)
    {
        //防重入
        if (typing != null) StopCoroutine(typing);

        fullText = content ?? string.Empty;
        text.text = "";

        typing = StartCoroutine(TypeRoutine());
    }

    private IEnumerator TypeRoutine()
    {
        IsTyping = true;
        string snapshot = fullText;
        int len = snapshot.Length;
        sb.Clear();

        for (int i = 1; i <= len; i++)
        {
            sb.Append(snapshot[i - 1]);
            text.text = sb.ToString();
            yield return new WaitForSeconds(charInterval);
        }

        text.text = fullText ?? "";
        IsTyping = false;
        typing = null;
    }

    public void Skip()
    {
        if (!IsTyping) return;

        if (typing != null) StopCoroutine(typing);

        text.text = fullText;
        IsTyping = false;
        typing = null;
    }

    public void Clear()
    {
        if (typing != null) StopCoroutine(typing);

        text.text = "";
        fullText = "";
        IsTyping = false;
        typing = null;
    }

    private void OnDestroy()
    {
        if (typing != null) StopCoroutine(typing);
        typing = null;
        IsTyping = false;
    }
}