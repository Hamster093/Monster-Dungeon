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

    // 记录当前正在展示的物品 id，用于异步加载回来后校验
    private int _currentItemId = -1;

    public override bool IsModal => true;

    public override void OnInit()
    {
        if (_confirmButton != null)
            _confirmButton.onClick.AddListener(OnConfirmClick);
    }

    public override void OnOpen(object data = null)
    {
        base.OnOpen(data);

        if (data is not ItemData d)
        {
            ClearDisplay();
            return;
        }
        _itemNameText.text = d.itemName;
        _itemDescriptionText.text = d.description;

        // 图标异步加载
        _currentItemId = d.id;
        SetIconAsync(d.id, d.iconAddress);
    }

    public override void OnClose()
    {
        ClearDisplay();
        base.OnClose();
    }

    private void ClearDisplay()
    {
        _currentItemId = -1;
        _itemNameText.text = "";
        _itemDescriptionText.text = "";
        if (_itemImage != null)
        {
            _itemImage.sprite = null;
            _itemImage.enabled = false;
        }
    }

    private async void SetIconAsync(int itemId, string address)
    {
        if (_itemImage == null) return;

        if (string.IsNullOrEmpty(address))
        {
            _itemImage.sprite = null;
            _itemImage.enabled = false;
            return;
        }

        var sp = await SpriteLoader.LoadAsync(address);

        // await 回来后：对象可能已销毁，或面板已经切到别的物品了
        if (this == null || _itemImage == null) return;
        if (_currentItemId != itemId) return;

        _itemImage.sprite = sp;
        _itemImage.enabled = sp != null;
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
