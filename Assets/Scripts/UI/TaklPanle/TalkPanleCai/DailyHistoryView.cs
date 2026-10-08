/****************************************************
    文件：DailyHistoryView.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/8 15:51:40
	功能：当日操作日志：右侧 ScrollView，跨天自动清空。由 TalkPanel 持有，不挂在 GameObject 上。
*****************************************************/

using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 当日操作日志：右侧 ScrollView，跨天自动清空。
/// 由 TalkPanel 持有，不挂在 GameObject 上。
/// </summary>
public class DailyHistoryView
{
    private readonly Transform _content;
    private readonly GameObject _itemPrefab;
    private string _lastText;//quick fix: 记录上一次的文本，避免重复添加

    private int _lastRecordedIndex = -1;
    private int _currentDay = -1;

    public DailyHistoryView(Transform content, GameObject itemPrefab)
    {
        _content = content;
        _itemPrefab = itemPrefab;
    }

    /// <summary>新的一段对话开始，重置本段的去重索引（不清内容）</summary>
    public void BeginSegment()
    {
        _lastRecordedIndex = -1;
    }

    public void RecordLineIfNeeded(int index, DialogueLine line)
    {
        if (index == _lastRecordedIndex) return;
        if (line == null) return;
        _lastRecordedIndex = index;
        string text = $"{line.speakerName}: {line.content}";
        if (text == _lastText) return;   // ← 跟紧邻的上一条完全相同，不重复记

        Append(text);
    }

    public void AppendChoice(string buttonText)
    {
        string text = $"你：选择'{buttonText}'";
        Append(text);
    }

    /// <summary>跨天则清空，同一天内不动</summary>
    public void CheckDayChangedAndClear(int currentDay)
    {
        if (_currentDay == currentDay) return;
        _currentDay = currentDay;
        Clear();
    }

    public void Clear()
    {
        _lastRecordedIndex = -1;
        if (_content == null) return;
        for (int i = _content.childCount - 1; i >= 0; i--)
            Object.Destroy(_content.GetChild(i).gameObject);
    }

    private void Append(string text)
    {
        if (_content == null || _itemPrefab == null)
        {
            Debug.LogWarning("[DailyHistoryView] Content 或 ItemPrefab 未配置");
            return;
        }

        var go = Object.Instantiate(_itemPrefab, _content);
        go.SetActive(true);

        var txt = go.GetComponentInChildren<Text>();
        if (txt != null) txt.text = text;

        LayoutRebuilder.ForceRebuildLayoutImmediate(_content as RectTransform);
        Canvas.ForceUpdateCanvases();

        var scroll = _content.GetComponentInParent<ScrollRect>();
        if (scroll != null) scroll.verticalNormalizedPosition = 0f;
        _lastText = text;
    }
}
