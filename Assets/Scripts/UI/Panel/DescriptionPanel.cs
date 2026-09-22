/****************************************************
    文件：GamePassEvent.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026-09-22 13:19:04
	功能：描述面板
*****************************************************/
using UnityEngine;
using UnityEngine.UI;
public class DescriptionPanel : PanelBase
{
    [Header("显示组件")]
    [SerializeField] private Image _itemImage;
    [SerializeField] private Text _itemNameText;
    [SerializeField] private Text _itemDescriptionText;

    [Header("按钮")]
    [SerializeField] private Button _confirmButton;

    public override bool IsModal => true;

    public override void OnInit()
    {
        if (_confirmButton != null)
            _confirmButton.onClick.AddListener(OnConfirmClick);
    }

    public override void OnOpen(object data = null)
    {
        base.OnOpen(data);

        if (data is ItemDescriptionData d)
        {
            _itemNameText.text = d.itemName;
            _itemDescriptionText.text = d.itemDescription;
            _itemImage.sprite = d.itemSprite != null ? d.itemSprite : d.emptySprite;
        }
        else
        {
            // 没传数据时清空
            _itemNameText.text = "";
            _itemDescriptionText.text = "";
        }
    }

    public override void OnClose()
    {
        base.OnClose();
    }

    // 点确认关闭
    private void OnConfirmClick()
    {
        UIManager.Instance.Close(this);
    }

    // ESC 也关闭，并且消费 ESC
    public override bool OnEscapePressed()
    {
        UIManager.Instance.Close(this);
        return true;
    }
}
