/****************************************************
    文件：ShopSlotDragger.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/9 23:43:01
	功能：商店槽位拖拽器
*****************************************************/

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class ShopSlotDragger : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("运行时注入")]
    public int SlotIndex;
    public ShopPanel Panel;
    public BackpackBoard Board;

    [Header("拖拽参数")]
    public RectTransform DragLayer;       // 拖拽时幽灵的父节点（Canvas 顶层）
    public ItemView ItemViewPrefab;        // 你的 ItemView 预制体
    public float CellSize = 115f;
    public float Spacing = 5f;

    private ItemView _ghost;
    private ItemView _real;                // 落位后的真实物品
    private bool _targetValid;
    private Vector2Int _targetOrigin;
    private int _rotation;
    private ScrollRect _scrollRect;
    private PointerEventData _pointer;
    public static bool IsAnyDragging => System.Array.Exists(FindObjectsOfType<ShopSlotDragger>(),d=>d._ghost!=null);

    [Header("高亮色")]
    public Color ValidColor = new Color(0.4f, 1f, 0.4f, 0.9f);
    public Color InvalidColor = new Color(1f, 0.4f, 0.4f, 0.9f);

    private void Awake()
    {
        _scrollRect = GetComponentInParent<ScrollRect>();
    }
    /// <summary>商店可能早于整备棋盘初始化；拖动时再绑定当前唯一真实棋盘，不创建替身。</summary>
    public bool ResolveDragContext()
    {
        if(BackpackBoard.Instance)Board=BackpackBoard.Instance;
        if(!Panel)Panel=GetComponentInParent<ShopPanel>();
        if(!DragLayer&&Board)DragLayer=Board.GetOrCreateDragLayer();
        if(!_scrollRect)_scrollRect=GetComponentInParent<ScrollRect>();
        return Panel&&Board&&DragLayer&&ItemViewPrefab;
    }
    public void OnBeginDrag(PointerEventData e)
    {
        if (e.button!=PointerEventData.InputButton.Left || GuideHelpOverlay.BlocksInput || PlayerBuildPanel.IsShowing || PresentationEffects.IsBusy || RouteSelectionPanel.IsShowing||EvolutionPanel.IsShowing) return;
        bool ready=ResolveDragContext();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if(Application.productName.EndsWith("_NarrativeValidation"))Debug.Log("[BoardNativeDrag] begin slot="+SlotIndex+" ready="+ready+" board="+(Board!=null));
#endif
        if(!ready||Board.IsEditing)return;

        var data = Panel.GetSlotData(SlotIndex);      

        if (data == null) { Debug.LogWarning("data 为空，GetSlotData 返回 null"); return; }
        if (data.shape == null) { Debug.LogWarning("shape 为空，检查 JSON 的 shapeId 和 ShapeDatabase"); return; }

        if (!Panel.CanBuy(SlotIndex)) { Debug.LogWarning("CanBuy 为 false，库存不足"); return; }
        if (_scrollRect != null) _scrollRect.enabled = false;
        _targetValid=false;_pointer=e;e.eligibleForClick=false;

        _rotation = 0;

        // 创建幽灵
        _ghost = CreateGhost(data.shape);
        var source = Panel.GetSlotInstance(SlotIndex);
        if (source != null) _ghost.SetInstanceState(source.uid, source.evolved,source.usedDay,source.evolutionKind);
        _ghost.transform.SetParent(DragLayer, false);
        OnDrag(e);
    }

    public void OnDrag(PointerEventData e)
    {

        if (_ghost == null || GuideHelpOverlay.BlocksInput || PlayerBuildPanel.IsShowing || PresentationEffects.IsBusy) return;
        _pointer=e;

        // 幽灵跟随鼠标
        FollowMouse(e);

        // 判断目标格
        var cam = e.pressEventCamera;
        if (Board.ScreenToBoardCoord(e.position, cam, out var mouseCell))
        {
            // 以左下角为抓取点，简单起见用抓取偏移 0
            _targetOrigin = mouseCell;
            _targetValid = Board.CanPlace(_ghost.Def, _targetOrigin, _rotation);
        }
        else
        {
            _targetValid = false;
        }

        RefreshHighlight();
    }

    public void OnEndDrag(PointerEventData e)
    {

        try
        {
            if (_ghost == null) return;
            // 模态引导出现时丢弃预览；原仓库实例尚未扣除，不在遮罩后偷偷落位。
            if(GuideHelpOverlay.BlocksInput || PlayerBuildPanel.IsShowing || PresentationEffects.IsBusy)return;
            OnDrag(e);
            bool valid = _targetValid;
            Destroy(_ghost.gameObject);
            _ghost = null;
            ClearHighlight();

            if (!valid) return;

            // 1. 先生成到棋盘
            var sourceInstance = Panel.GetSlotInstance(SlotIndex);
            var shape = Panel.GetSlotShape(SlotIndex);
            var newItem = Board.CreateItemOnBoard(shape, _targetOrigin, _rotation, ItemViewPrefab);
            if (newItem == null) return;
            if (sourceInstance != null) newItem.SetInstanceState(sourceInstance.uid, sourceInstance.evolved,sourceInstance.usedDay,sourceInstance.evolutionKind);

            // 2. 生成成功再扣钱；扣钱失败则回滚
            if (!Panel.TryBuyForDrag(SlotIndex))
            {
                Board.RemoveItem(newItem.Item);
                Destroy(newItem.gameObject);
                return;
            }

            _real = newItem;
            _real.StageFromStorage();
        }
        finally
        {
            if (_scrollRect != null) _scrollRect.enabled = true;
            if (_ghost != null) { Destroy(_ghost.gameObject); _ghost = null; }
            ClearHighlight();
        }
        
    }
    private void OnDisable()
    {
        if(_ghost){Destroy(_ghost.gameObject);_ghost=null;}
        if(_scrollRect)_scrollRect.enabled=true;
        ClearHighlight();_targetValid=false;
    }

    private void Update()
    {
        if(GuideHelpOverlay.BlocksInput || PlayerBuildPanel.IsShowing || PresentationEffects.IsBusy)return;
        if (_ghost != null && Input.GetKeyDown(KeyCode.R)
            && _ghost.Def != null && _ghost.Def.CanRotate)
        {
            _rotation = (_rotation + 90) % 360;
            _ghost.SetRotationSilently(_rotation);
            if(_pointer!=null)OnDrag(_pointer);
        }
    }

    // ---------- 辅助 ----------

    private ItemView CreateGhost(ItemShapeDefinition def)
    {
        var go = new GameObject("Ghost_" + def.Id,
            typeof(RectTransform), typeof(CanvasGroup));

        go.SetActive(false);   // ★ 先关，防止 AddComponent 立刻触发 OnEnable

        var rect = (RectTransform)go.transform;
        rect.SetParent(DragLayer != null ? DragLayer : transform.root, false);

        var view = go.AddComponent<ItemView>();
        view.IsGhost = true;              // ★ 现在安全
        view.Def = def;
        view.CellSize = CellSize;
        view.Spacing = Spacing;
        view.SetRotationSilently(_rotation);

        var cg = go.GetComponent<CanvasGroup>();
        cg.alpha = 0.75f;
        cg.blocksRaycasts = false;

        go.SetActive(true);    // ★ 激活，OnEnable 此时看到 IsGhost=true，跳过注册

        return view;
    }

    private void FollowMouse(PointerEventData e)
    {
        var parentRect = _ghost.transform.parent as RectTransform;
        if (parentRect == null) return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect, e.position, e.pressEventCamera, out var lp))
        {
            _ghost.transform.localPosition = lp;
        }
    }

    private void RefreshHighlight()
    {
        ClearHighlight();

        if (_ghost == null || _ghost.Def == null || Board == null) return;

        var shape = _ghost.Def.GetNormalizedShape(_rotation);
        var tint = _targetValid ? ValidColor : InvalidColor;

        foreach (var offset in shape)
        {
            var c = _targetOrigin + offset;
            var cell = Board.GetCell(c);
            if (cell == null || cell.Image == null) continue;
            cell.Image.color = tint;
        }
    }

    private void ClearHighlight()
    {
        if (Board == null) return;
        for (int y = 0; y < Board.Height; y++)
        {
            for (int x = 0; x < Board.Width; x++)
            {
                var cell = Board.GetCell(x, y);
                if (cell != null) cell.RefreshVisual();
            }
        }
    }
}
