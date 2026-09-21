/****************************************************
    文件：TalkPanle.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026-09-21 12:20:23
	功能：Nothing
*****************************************************/

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[Serializable]
public class DialogueLine
{
    public string iconName;  
    public string speakerName;
    public string content;
}

public class UIClickable : MonoBehaviour, IPointerClickHandler
{
    public TypewriterTextLegacy _ttl;
    public Text _speakerNameText;
    public Image _iconImage;     // 当前物体上的头像 Image

    [Header("对话列表")]
    public List<DialogueLine> _dialogueList = new List<DialogueLine>();

    private int currentIndex = 0;
    private string _currentSpeaker = null;
    private string _currentIcon = null;

    // 资源缓存：iconName -> Sprite
    private Dictionary<string, Sprite> _iconCache = new Dictionary<string, Sprite>();

    /// <summary>
    /// 初始化方法，传入对话列表 预加载图标
    /// </summary>
    /// <param name="dialogueList"></param>
    public void Init(List<DialogueLine> dialogueList)
    {
        _dialogueList = dialogueList;
        currentIndex = 0;
        _currentSpeaker = null;
        _currentIcon = null;

        PreloadIcons();
    }

    private void PreloadIcons()
    {
        _iconCache.Clear();

        for (int i = 0; i < _dialogueList.Count; i++)
        {
            string iconName = _dialogueList[i].iconName;
            if (string.IsNullOrEmpty(iconName)) continue;
            if (_iconCache.ContainsKey(iconName)) continue;

            Sprite sp = Resources.Load<Sprite>("Icons/" + iconName);

            if (sp == null)
            {
                Debug.LogWarning($"图标加载失败: Icons/{iconName}");
                continue;
            }

            _iconCache.Add(iconName, sp);
        }
    }

    /// <summary>
    /// 点击事件处理方法
    /// </summary>
    /// <param name="eventData"></param>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (_ttl.IsTyping)
        {
            _ttl.Skip();
            return;
        }

        if (currentIndex >= _dialogueList.Count)
        {
            Debug.Log("对话结束");
            return;
        }

        DialogueLine line = _dialogueList[currentIndex];

        if (_currentSpeaker != line.speakerName)
        {
            _currentSpeaker = line.speakerName;
            _speakerNameText.text = line.speakerName;
        }

        if (_currentIcon != line.iconName)
        {
            _currentIcon = line.iconName;
            SetIcon(line.iconName);
        }

        _ttl.Play(line.content);
        currentIndex++;
    }

    private void SetIcon(string iconName)
    {
        if (_iconImage == null) return;

        if (string.IsNullOrEmpty(iconName))
        {
            _iconImage.sprite = null;
            return;
        }

        if (_iconCache.TryGetValue(iconName, out Sprite sp))
        {
            _iconImage.sprite = sp;
            _iconImage.enabled = true;
        }
        else
        {
            // 没预加载到，临时加载一次
            Sprite loaded = Resources.Load<Sprite>("Icons/" + iconName);
            if (loaded != null)
            {
                _iconCache[iconName] = loaded;
                _iconImage.sprite = loaded;
                _iconImage.enabled = true;
            }
            else
            {
                Debug.LogWarning($"图标缺失: Icons/{iconName}");
                _iconImage.enabled = false;
            }
        }
    }

    /// <summary>
    /// 重置对话状态，清空当前索引和文本
    /// </summary>
    public void ResetDialogue()
    {
        currentIndex = 0;
        _currentSpeaker = null;
        _currentIcon = null;
        _speakerNameText.text = "";
        _ttl.Clear();
    }
}