/****************************************************
    文件：CharacterArchivePanel.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/2 12:22:31
	功能：
*****************************************************/

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CharacterArchivePanel : PanelBase
{

    [Header("按钮")]
    [SerializeField] private Button _closeButton;
    [SerializeField] private Button _basicInfo; //基本信息
    [SerializeField] private Button _dialogueLog;//对话记录
    [SerializeField] private Button _validInfo;//有效信息
    [SerializeField] private Button _investigationLog;//调查记录


    [Header("人物信息")]
    [SerializeField] private Text _Introduction; //简要介绍
    [SerializeField] private Text _name; //冒险者名称
    [SerializeField] private Image _iconImage;//冒险者头像

    public override bool IsModal => false;

    public override void OnInit()
    {
        _closeButton.onClick.AddListener(OnCloseClick);
    }

    public override void OnDestroy()
    {
        _closeButton.onClick.RemoveListener(OnCloseClick);
    }

    private void OnCloseClick()
    {
        UIManager.Instance.Close(this);
    }
}