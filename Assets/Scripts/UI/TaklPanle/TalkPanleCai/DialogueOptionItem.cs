/****************************************************
    文件：DialogueOptionItem.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/9 15:44:04
	功能：文本按钮预制体
*****************************************************/

using Ink.Runtime;
using System;
using UnityEngine;
using UnityEngine.UI;

public class DialogueOptionItem : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private Text _text;

    private Action _onClick;

    public void SetData(string content, Action onClick)
    {
        if (_text != null) _text.text = content;
        _onClick = onClick;

        if (_button != null)
        {
            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(HandleClick);
        }
    }

    internal void SetInteractable(bool value)
    {
        _button.interactable = value;
    }

    private void HandleClick()
    {
        var cb = _onClick;
        _onClick = null;
        cb?.Invoke();
    }
}