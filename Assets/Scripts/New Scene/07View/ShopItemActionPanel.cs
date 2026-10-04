/****************************************************
    文件：ShopItemActionPanel.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/4 18:00:15
	功能：商店右键操作面板
*****************************************************/

using UnityEngine;
using UnityEngine.UI;

public class ShopItemActionPanel : PanelBase
{
    [SerializeField] private Button _examineButton;
    [SerializeField] private Button _buyButton;
    [SerializeField] private Vector2 _buttonOffset;

    public override bool IsModal => true;

    private ShopActionData _currentData;

    public override void OnInit()
    {
        _examineButton.onClick.AddListener(OnViewClick);
        _buyButton.onClick.AddListener(OnBuyClick);
    }

    public override void OnDestroy()
    {
        _examineButton.onClick.RemoveListener(OnViewClick);
        _buyButton.onClick.RemoveListener(OnBuyClick);
        base.OnDestroy();
    }

    public override void OnOpen(object data = null)
    {
        base.OnOpen(data);
        if (data is ShopActionData d) _currentData = d;

        _buyButton.interactable = _currentData != null
            && SystemManager.Instance.Economy.Cash >= _currentData.price;

        FollowMouse();
    }

    public override void OnRefresh(object data = null)
    {
        base.OnRefresh(data);
        if (data is ShopActionData d) _currentData = d;
        FollowMouse();
    }

    public override void OnClose()
    {
        _currentData = null;
        base.OnClose();
    }

    public override bool OnEscapePressed()
    {
        UIManager.Instance.Close(this);
        return true;
    }

    // ---------- 按钮 ----------

    private void OnViewClick()
    {
        if (_currentData?.data == null) return;

        UIManager.Instance.Open<DescriptionPanel>(_currentData.data);
        UIManager.Instance.Close(this);
    }

    private void OnBuyClick()
    {
        _currentData?.onBuy?.Invoke();
        UIManager.Instance.Close(this);
    }

    // ---------- 定位 ----------

    private void FollowMouse()
    {
        var canvas = GetComponentInParent<Canvas>();
        Vector2 mousePos = Input.mousePosition;

        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            RectTransformUtility.ScreenPointToWorldPointInRectangle(
                canvas.transform as RectTransform,
                mousePos,
                canvas.worldCamera,
                out Vector3 worldPos);
            transform.position = worldPos;
        }
        else
        {
            transform.position = (Vector3)mousePos + (Vector3)_buttonOffset;
        }
    }
}