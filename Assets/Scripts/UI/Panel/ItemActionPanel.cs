/****************************************************
    文件：ItemActionPanel.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026-09-22 13:19:04
	功能：背包内右键点击物品后弹出的操作面板
*****************************************************/
using UnityEngine;
using UnityEngine.UI;

public class ItemActionPanel : PanelBase
{
    [SerializeField] private Button _examineButton;
    [SerializeField] private Button _dropButton;
    [SerializeField] private Vector2 _buttonOffset;
    [SerializeField] private Vector2 _fixedPosition = new Vector2(624f, 745f);

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

        ApplyFixedPosition();
    }
    /// <summary>
    /// 刷新
    /// </summary>
    /// <param name="data"></param>
    public override void OnRefresh(object data = null)
    {
        if (data is ItemActionData d)
            _currentData = d;

        ApplyFixedPosition();
    }

    public override void OnClose()
    {
        _currentData = null; // 清掉引用，避免持有旧回调
        base.OnClose();
    }

    public override bool OnEscapePressed()
    {
        AudioManager.Instance.PlaySFX(AudioKeys.SFX_Back); // ★ 丢弃物品音效（可用返回声替代）
        UIManager.Instance.Close(this);
        return true;
    }

    #region 按钮

    private void OnViewClick()
    {
        if (_currentData == null) return;
        AudioManager.Instance.PlaySFX(AudioKeys.SFX_Click); // ★ 打开详情音效
        UIManager.Instance.Open<DescriptionPanel>(_currentData.data);

        UIManager.Instance.Close(this); // 查看后关掉操作面板
    }

    private void OnDropClick()
    {
        AudioManager.Instance.PlaySFX(AudioKeys.SFX_Back); // ★ 丢弃物品音效（可用返回声替代）
        _currentData?.onDrop?.Invoke();
        UIManager.Instance.Close(this);
    }

    #endregion

    #region 定位

    private void ApplyFixedPosition()
    {
        var canvas = GetComponentInParent<Canvas>();

        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            RectTransformUtility.ScreenPointToWorldPointInRectangle(
                canvas.transform as RectTransform,
                _fixedPosition,
                canvas.worldCamera,
                out Vector3 worldPos);
            transform.position = worldPos;
        }
        else
        {
            transform.position = (Vector3)_fixedPosition;
        }
    }

    #endregion
}
