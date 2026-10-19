/****************************************************
    文件：ToastItem.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/14 15:33:29
	功能：单条提示：负责自身动画与文案刷新
*****************************************************/

// ToastItem.cs
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 单条提示：负责自身动画与文案刷新
/// 由 ToastManager 在运行时创建，不需要预制体
/// </summary>
public class ToastItem : MonoBehaviour
{
    public string Key { get; private set; }

    RectTransform rect;
    CanvasGroup group;
    Image bg;
    Text label;

    string rawText;
    int count = 1;

    float duration;
    float fadeInRatio;
    float fadeOutRatio;
    float startOffsetY;
    float riseDistance;

    static Font cachedFont;
    static Sprite cachedSprite;

    // ------------------------------------------------------------
    // 创建
    // ------------------------------------------------------------
    public static ToastItem Create(Transform parent, ToastStyle style)
    {
        var go = new GameObject("Toast",
            typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(ToastItem));
        go.transform.SetParent(parent, false);

        var item = go.GetComponent<ToastItem>();

        item.rect = go.GetComponent<RectTransform>();
        item.rect.anchorMin = new Vector2(0.5f, 0.5f);
        item.rect.anchorMax = new Vector2(0.5f, 0.5f);
        item.rect.pivot = new Vector2(0.5f, 0.5f);

        item.group = go.GetComponent<CanvasGroup>();
        item.group.alpha = 0f;
        item.group.interactable = false;
        item.group.blocksRaycasts = false;

        // 背景
        item.bg = go.GetComponent<Image>();
        item.bg.color = style.backgroundColor;
        item.bg.raycastTarget = false;
        var sprite = style.backgroundSprite != null
    ? style.backgroundSprite
    : GetDefaultSprite();   // 兜底：程序生成圆角

        item.bg.sprite = sprite;
        item.bg.type = Image.Type.Sliced;
        item.bg.color = style.backgroundColor;

        // 根据文字自动撑开尺寸
        var layout = go.AddComponent<HorizontalLayoutGroup>();
        int px = Mathf.RoundToInt(style.padding.x);
        int py = Mathf.RoundToInt(style.padding.y);
        layout.padding = new RectOffset(px, px, py, py);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var fitter = go.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        item.rect.sizeDelta = new Vector2(720, 64);
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 文本
        var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(go.transform, false);

        item.label = textGo.GetComponent<Text>();
        item.label.font = style.font != null ? style.font : GetDefaultFont();
        item.label.fontSize = style.fontSize;
        item.label.color = style.textColor;
        item.label.alignment = TextAnchor.MiddleCenter;
        item.label.horizontalOverflow = HorizontalWrapMode.Wrap;
        item.label.verticalOverflow = VerticalWrapMode.Overflow;
        item.label.raycastTarget = false;
        var width = textGo.AddComponent<LayoutElement>(); width.preferredWidth = 640; width.flexibleWidth = 1;

        if (item.label.font == null)
            Debug.LogWarning("[Toast] 没有可用字体，请给 ToastManager 指定 Font（中文需指定中文字体）");

        return item;
    }

    // ------------------------------------------------------------
    // 初始化 / 合并
    // ------------------------------------------------------------
    public void Init(string key, string text, int count, float duration,
                  float fadeInRatio, float fadeOutRatio,
                  float startOffsetY, float riseDistance)
    {
        Key = key;
        rawText = text;
        this.count = count;
        this.duration = duration;
        this.fadeInRatio = fadeInRatio;
        this.fadeOutRatio = fadeOutRatio;   // 新增
        this.startOffsetY = startOffsetY;
        this.riseDistance = riseDistance;

        RefreshLabel();
        ApplyProgress(0f);
    }

    /// <summary>相同 key 再次触发时调用：计数 +1，动画进度保持不变</summary>
    public void Merge(string text)
    {
        count++;
        rawText = text;
        RefreshLabel();
    }

    // ------------------------------------------------------------
    // 动画
    // ------------------------------------------------------------
    public IEnumerator Play()
    {
        if (duration <= 0f)
        {
            ApplyProgress(1f);
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            // 对象可能被外部销毁
            if (this == null || rect == null || group == null) yield break;

            t += Time.unscaledDeltaTime;   // 用 unscaled，暂停游戏时也能正常播放
            ApplyProgress(Mathf.Clamp01(t / duration));
            yield return null;
        }

        if (this == null || rect == null || group == null) yield break;
        ApplyProgress(1f);
    }

    void ApplyProgress(float t)
    {
        float fadeIn = Mathf.Max(0.0001f, fadeInRatio);
        float fadeOut = Mathf.Clamp(fadeOutRatio, 0.01f, 1f - fadeIn);
        float holdEnd = 1f - fadeOut;   // 到这之前保持完全不透明

        float y;
        float a;

        if (t < fadeIn)
        {
            // 阶段一：快速加深 + 从下方归位
            float k = t / fadeIn;
            float ease = 1f - (1f - k) * (1f - k);   // easeOutQuad
            y = Mathf.Lerp(startOffsetY, 0f, ease);
            a = k;
        }
        else
        {
            // 上飘贯穿后半程
            float k = (t - fadeIn) / (1f - fadeIn);
            y = Mathf.Lerp(0f, riseDistance, k);

            if (t < holdEnd)
            {
                a = 1f;                 // 完全不透明，保持
            }
            else
            {
                float kOut = Mathf.Clamp01((t - holdEnd) / fadeOut);
                a = 1f - kOut;          // 线性淡出
            }
        }

        rect.anchoredPosition = new Vector2(0f, y);
        group.alpha = Mathf.Clamp01(a);
    }

    void RefreshLabel()
    {
        if (label == null) return;
        label.text = count > 1 ? rawText + " ×" + count : rawText;
    }

    // ------------------------------------------------------------
    // 内置资源
    // ------------------------------------------------------------
    static Font GetDefaultFont()
    {
        if (cachedFont != null) return cachedFont;

        try { cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
        if (cachedFont == null)
        {
            try { cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
        }
        return cachedFont;
    }


    static Sprite GetDefaultSprite()
    {
        if (cachedSprite != null) return cachedSprite;

        const int size = 64;
        const int radius = 24;

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };

        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = 0f, dy = 0f;
                if (x < radius) dx = radius - x;
                else if (x > size - 1 - radius) dx = x - (size - 1 - radius);
                if (y < radius) dy = radius - y;
                else if (y > size - 1 - radius) dy = y - (size - 1 - radius);

                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01(radius - dist + 0.5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();

        var border = new Vector4(radius, radius, radius, radius);
        cachedSprite = Sprite.Create(
            tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
            100f, 0, SpriteMeshType.FullRect, border);

        cachedSprite.hideFlags = HideFlags.HideAndDontSave;
        return cachedSprite;
    }
}