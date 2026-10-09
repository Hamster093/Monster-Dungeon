/****************************************************
    文件：AbyssalAltarPanel.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/4 18:31:08
	功能：深渊祭坛面板
*****************************************************/

using System;
using UnityEngine;
using UnityEngine.UI;

public class AbyssalAltarPanel : PanelBase
{
    [Header("祭坛")]
    [SerializeField] private Button _newMonsterButton;
    [SerializeField] private Button _returnButton;

    [Header("地狱")]
    [SerializeField] private Button _decomposeButton;
    [SerializeField] private Image _decomposeDropZone;    // DecomposeBG，接收拖入
    [SerializeField] private GameObject _hellPanel;
    [SerializeField] private Button _closeHellPanelButton; // 关闭地狱面板按钮
    [SerializeField] private Button _closelButton; // 关闭面板按钮

    public override bool IsModal => false;

    public override void OnInit()
    {
        _newMonsterButton.onClick.AddListener(OnNewMonsterClick);
        _returnButton.onClick.AddListener(OnReturnClick);
        _decomposeButton.onClick.AddListener(OnDecomposeClick);

        if (_closeHellPanelButton != null)
        {
            _closeHellPanelButton.onClick.AddListener(OnCloseHellPanelClick);
        }
        if (_closelButton != null)
        {
            _closelButton.onClick.AddListener(OnClosePanelClick);
        }

    }

    public override void OnDestroy()
    {
        _newMonsterButton.onClick.RemoveListener(OnNewMonsterClick);
        _returnButton.onClick.RemoveListener(OnReturnClick);
        _decomposeButton.onClick.RemoveListener(OnDecomposeClick);
        if (_closeHellPanelButton != null)
            _closeHellPanelButton.onClick.RemoveListener(OnCloseHellPanelClick);
        if (_closelButton != null)
        {
            _closelButton.onClick.RemoveListener(OnClosePanelClick);
        }

        base.OnDestroy();
    }

    

    public override void OnOpen(object data = null)
    {
        base.OnOpen(data);

        if (_hellPanel != null)
        {
            _hellPanel.SetActive(false);
        }
    }

    public override void OnClose()
    {
        base.OnClose();
    }

    public override bool OnEscapePressed()
    {
        UIManager.Instance.Close(this);
        return true;
    }

    // ---------- 按钮回调 ----------

    /// <summary>左侧：新魔物</summary>
    private void OnNewMonsterClick()
    {
        int id = UnityEngine.Random.Range(1, 18);

        if (!PartyModel.Instance.TryAddMonster(id))
        {
            Debug.Log("[AbyssalAltarPanel] 魔物格子已满");
            return;
        }
    }

    /// <summary>左侧：归还深渊</summary>
    private void OnReturnClick()
    {
        Debug.Log("[AbyssalAltarPanel] 归还深渊");
        if (_hellPanel != null)
        {
            _hellPanel.SetActive(true);
        }
    }

    /// <summary>右侧：分解</summary>
    private void OnDecomposeClick()
    {
        Debug.Log("[AbyssalAltarPanel] 分解");
        // TODO: 检查拖入的物品 -> 执行分解 -> 发奖励 -> 刷新
    }

    /// <summary>关闭面板</summary>
    private void OnClosePanelClick()
    {
        UIManager.Instance.Close(this);
    }
    /// <summary>关闭地狱面板</summary>
    private void OnCloseHellPanelClick()
    {
        Debug.Log("[AbyssalAltarPanel] 关闭地狱面板");
        if (_hellPanel != null)
        {
            _hellPanel.SetActive(false);
        }
    }
}