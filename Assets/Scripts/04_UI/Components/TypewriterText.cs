/****************************************************
    文件：TypewriterText.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026-09-21 12:20:23
	功能：对话框打字机效果
*****************************************************/

using System.Collections;
using System.Text;
using System.Globalization;
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

        // 新台词从滚动区顶部开始，不能沿用上一段长文的滚动位置。
        var reading = text != null ? text.GetComponentInParent<ScrollRect>() : null;
        if (reading != null && reading.content == text.rectTransform)
            reading.content.anchoredPosition = Vector2.zero;

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

        for (int i = 0; i < len;)
        {
            // 引导和短转场暂停剩余计时，结束后从原字符继续，不让正文在遮罩下自行打完。
            while(PresentationEffects.IsBusy||GuideHelpOverlay.BlocksInput)yield return null;
            if(text.supportRichText&&TryReadTag(snapshot,i,out int tagEnd)) {
                sb.Append(snapshot,i,tagEnd-i+1);i=tagEnd+1;
                continue;
            }
            string element=StringInfo.GetNextTextElement(snapshot,i);i+=element.Length;
            sb.Append(element);
            text.text = sb.ToString();
            float remaining=Mathf.Max(0,charInterval);
            while(remaining>0) {
                if(!PresentationEffects.IsBusy&&!GuideHelpOverlay.BlocksInput)remaining-=Time.unscaledDeltaTime;
                yield return null;
            }
        }

        text.text = fullText ?? "";
        IsTyping = false;
        typing = null;
    }

    public void Skip()
    {
        if(PresentationEffects.IsBusy||GuideHelpOverlay.BlocksInput)return;
        if (!IsTyping) return;
        if (typing != null) StopCoroutine(typing);
        text.text = fullText;
        IsTyping = false;
        typing = null;
    }

    // 富文本标签一次写入，避免把<size=…>等控制字符逐字暴露给读者。
    private static bool TryReadTag(string content,int start,out int end)
    {
        end=-1;if(content[start]!='<')return false;end=content.IndexOf('>',start+1);if(end<0)return false;
        string name=content.Substring(start+1,end-start-1).TrimStart('/');
        return name=="b"||name=="i"||name=="/b"||name=="/i"||name.StartsWith("size")||name.StartsWith("color")||name.StartsWith("material");
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
