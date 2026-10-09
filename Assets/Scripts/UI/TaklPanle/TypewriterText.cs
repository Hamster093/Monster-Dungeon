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

public class TypewriterTextLegacy : MonoBehaviour, ITypewriterEffect
{
    [Header("UI 引用")]
    public Text text;

    [Header("打字参数")]
    [Tooltip("每个字符显示的间隔时间（秒），值越小打字越快")]
    public float charInterval = 0.05f;

    [Header("打字机开关")]
    [Tooltip("关闭后 Play 会直接显示整段文本，不播放逐字效果")]
    public bool enableTypewriter = true;

    private Coroutine typing;
    private string fullText;
    // 用 StringBuilder 追加字符，避免反复创建新字符串
    private StringBuilder sb = new StringBuilder();

    public bool IsTyping { get; private set; }

    /// <summary>运行时开关（推荐用这个，而不是直接改字段）</summary>
    public void SetEnableTypewriter(bool enable)
    {
        enableTypewriter = enable;
    }

    public void Play(string content)
    {
        //防重入
        if (typing != null) StopCoroutine(typing);

        fullText = content ?? string.Empty;
        text.text = "";

        // 开关关闭 → 直接显示全文
        if (!enableTypewriter)
        {
            text.text = fullText;
            IsTyping = false;
            typing = null;
            return;
        }

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