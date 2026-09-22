/****************************************************
    文件：GamePassEvent.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026-09-22 13:19:04
	功能：Nothing
*****************************************************/
using UnityEngine;
using UnityEngine.UI;

public class ItemActionPanel : PanelBase
{
    [SerializeField] private Button _examineButton;
    [SerializeField] private Button _dropButton;

    // 模态，点遮罩可关闭
    public override bool IsModal => true;

    private ItemActionData _currentData;

    public override void OnInit()
    {
        _examineButton.onClick.AddListener(OnViewClick);
        _dropButton.onClick.AddListener(OnDropClick);
    }
    /// <summary>
    /// 打开
    /// </summary>
    /// <param name="data"></param>
    public override void OnOpen(object data = null)
    {
        base.OnOpen(data);

        if (data is ItemActionData d)
            _currentData = d;

        FollowMouse(); // 打开时出现在鼠标位置
    }
    /// <summary>
    /// 刷新
    /// </summary>
    /// <param name="data"></param>
    public override void OnRefresh(object data = null)
    {
        if (data is ItemActionData d)
            _currentData = d;

        FollowMouse();
    }

    public override void OnClose()
    {
        _currentData = null; // 清掉引用，避免持有旧回调
        base.OnClose();
    }

    public override bool OnEscapePressed()
    {
        UIManager.Instance.Close(this);
        return true;
    }

    #region 按钮

    private void OnViewClick()
    {
        if (_currentData == null) return;

        UIManager.Instance.Open<DescriptionPanel>(new ItemDescriptionData
        {
            itemName = _currentData.itemName,
            itemDescription = _currentData.itemDescription,
            itemSprite = _currentData.itemSprite,
            emptySprite = _currentData.emptySprite
        });

        UIManager.Instance.Close(this); // 查看后关掉操作面板
    }

    private void OnDropClick()
    {
        _currentData?.onDrop?.Invoke();
        UIManager.Instance.Close(this);
    }

    #endregion

    #region 定位

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
            transform.position = mousePos+new Vector2(10f, -10f); ;
        }
    }

    #endregion
}
