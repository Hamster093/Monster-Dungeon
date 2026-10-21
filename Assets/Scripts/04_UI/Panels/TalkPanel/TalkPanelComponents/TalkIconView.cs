/****************************************************
    文件：TalkIconView.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/8 15:50:59
	功能：TalkPanel立绘管理：加载 talk/idle 两张图、切换显示、释放
*****************************************************/

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TalkIconView
{
    private readonly MonoBehaviour _owner;
    private readonly Image _iconImage;
    private readonly string _talkSuffix;
    private readonly string _idleSuffix;

    private int _loadToken;
    private Sprite _talkSprite;
    private Sprite _idleSprite;
    private readonly HashSet<string> _usedAddresses = new();

    public Sprite TalkSprite => _talkSprite;
    public Sprite IdleSprite => _idleSprite;

    public TalkIconView(MonoBehaviour owner, Image iconImage, string talkSuffix, string idleSuffix)
    {
        _owner = owner;
        _iconImage = iconImage;
        _talkSuffix = talkSuffix;
        _idleSuffix = idleSuffix;
    }

    /// <summary>切换角色，异步加载两张图。加载完成后回调 onLoaded（由调用方决定显示 talk 还是 idle）。</summary>
    public void SetCharacter(string baseName, Action onLoaded = null)
    {
        if (string.IsNullOrEmpty(baseName))
        {
            Clear();
            return;
        }

        if (_iconImage == null) return;

        if (string.IsNullOrEmpty(baseName))
        {
            _talkSprite = null;
            _idleSprite = null;
            Show(null);
            onLoaded?.Invoke();
            return;
        }

        int token = ++_loadToken;
        string talkAddr = baseName + _talkSuffix;
        string idleAddr = baseName + _idleSuffix;
        _usedAddresses.Add(talkAddr);
        _usedAddresses.Add(idleAddr);

        LoadAsync(talkAddr, idleAddr, token, onLoaded);
    }

    private async void LoadAsync(string talkAddr, string idleAddr, int token, Action onLoaded)
    {
        var talk = await SpriteLoader.LoadAsync(talkAddr);
        var idle = await SpriteLoader.LoadAsync(idleAddr);

        if (_owner == null || _iconImage == null || token != _loadToken) return;

        _talkSprite = talk;
        _idleSprite = idle;
        onLoaded?.Invoke();
    }

    public void ShowTalk() => Show(_talkSprite);
    public void ShowIdle() => Show(_idleSprite);

    /// <summary>清空显示，不释放资源（用于 ResetState）</summary>
    public void Clear()
    {
        _talkSprite = null;
        _idleSprite = null;
        Show(null);
    }

    /// <summary>释放所有用过的 Addressables（用于 OnDestroy）</summary>
    public void ReleaseAll()
    {
        foreach (var addr in _usedAddresses)
            SpriteLoader.Release(addr);
        _usedAddresses.Clear();
        Clear();
    }

    private void Show(Sprite sprite)
    {
        if (_iconImage == null) return;
        _iconImage.sprite = sprite;
        _iconImage.enabled = sprite != null;
    }
}