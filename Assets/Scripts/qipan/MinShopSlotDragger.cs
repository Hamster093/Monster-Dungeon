/****************************************************
    文件：MinShopSlotDragger.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/10 0:38:17
	功能：最小商店测试
*****************************************************/

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MinShopSlotDragger : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("金币（模拟）")]
    public static int Money = 500;

    [Header("商品")]
    public ItemShapeDefinition Def;
    public int Price = 100;

    [Header("引用（Inspector 拖）")]
    public BackpackBoard Board;
    public RectTransform DragLayer;
    public ItemView ItemViewPrefab;

    [Header("尺寸（和棋盘一致）")]
    public float CellSize = 95f;
    public float Spacing = 5f;

    [Header("高亮色")]
    public Color ValidColor = new Color(0.4f, 1f, 0.4f, 0.9f);
    public Color InvalidColor = new Color(1f, 0.4f, 0.4f, 0.9f);

    private ItemView _ghost;
    private int _rotation;
    private Vector2Int _targetOrigin;
    private bool _targetValid;

    private Vector2 _lastMouseLocal;
    private bool _hasMouse;

    // ---------------- 拖拽 ----------------

    public void OnBeginDrag(PointerEventData e)
    {
        if (Def == null || Board == null || ItemViewPrefab == null)
        {
            Debug.LogWarning("[MinShopSlotDragger] 引用没配全");
            return;
        }
        if (Money < Price)
        {
            Debug.Log("钱不够");
            return;
        }

        _rotation = 0;
        _ghost = CreateGhost();
    }

    public void OnDrag(PointerEventData e)
    {
        if (_ghost == null) return;

        var parentRect = _ghost.transform.parent as RectTransform;
        if (parentRect != null &&
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect, e.position, e.pressEventCamera, out var lp))
        {
            // 让幽灵中心对齐鼠标
            var size = Def.GetSize(_rotation);
            float w = size.x * CellSize + Mathf.Max(0, size.x - 1) * Spacing;
            float h = size.y * CellSize + Mathf.Max(0, size.y - 1) * Spacing;
            _ghost.transform.localPosition = lp - new Vector2(w / 2f, h / 2f);

            _lastMouseLocal = lp;   // 保存一下
            _hasMouse = true;
        }

        // 判断落点：鼠标格 - 包围盒中心偏移 = origin
        if (Board.ScreenToBoardCoord(e.position, e.pressEventCamera, out var mouseCell))
        {
            var size = Def.GetSize(_rotation);
            var centerOffset = new Vector2Int(size.x / 2, size.y / 2);
            _targetOrigin = mouseCell - centerOffset;
            _targetValid = Board.CanPlace(Def, _targetOrigin, _rotation);
        }
        else
        {
            _targetValid = false;
        }

        RefreshHighlight();


    }

    public void OnEndDrag(PointerEventData e)
    {
        if (_ghost == null) return;

        bool valid = _targetValid;
        var origin = _targetOrigin;
        var rot = _rotation;

        Destroy(_ghost.gameObject);
        _ghost = null;
        ClearHighlight();

        if (!valid) return;
        if (Money < Price) return;

        // 先创建，成功再扣钱
        var item = Board.CreateItemOnBoard(Def, origin, rot, ItemViewPrefab);
        if (item == null) return;

        Money -= Price;
        Debug.Log($"购买成功：{Def.DisplayName}，花费 {Price}，剩余 {Money}");
    }

    // ---------------- R 旋转 ----------------

    private void Update()
    {
        if (_ghost == null) return;
        if (Def == null || !Def.CanRotate) return;
        if (Input.GetKeyDown(KeyCode.R))
        {
            _rotation = (_rotation + 90) % 360;
            _ghost.SetRotationSilently(_rotation);
        }

        // 旋转后重算幽灵位置，保持鼠标在中心
        if (_hasMouse)
        {
            var size = Def.GetSize(_rotation);
            float w = size.x * CellSize + Mathf.Max(0, size.x - 1) * Spacing;
            float h = size.y * CellSize + Mathf.Max(0, size.y - 1) * Spacing;
            _ghost.transform.localPosition = _lastMouseLocal - new Vector2(w / 2f, h / 2f);
        }
    }

    // ---------------- 幽灵 ----------------

    private ItemView CreateGhost()
    {
        var go = new GameObject("Ghost_" + Def.Id, typeof(RectTransform));

        // 先禁用，防止 OnEnable 在字段设好之前触发
        go.SetActive(false);

        var rect = (RectTransform)go.transform;
        rect.SetParent(DragLayer != null ? DragLayer : transform.root, false);
        rect.pivot = new Vector2(0f, 0f);
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);

        var cg = go.AddComponent<CanvasGroup>();
        cg.alpha = 0.8f;
        cg.blocksRaycasts = false;

        var view = go.AddComponent<ItemView>();
        view.IsGhost = true;         // ← 关键：不注册到棋盘
        view.Def = Def;
        view.Board = Board;
        view.CellSize = CellSize;
        view.Spacing = Spacing;
        view.SetRotationSilently(_rotation);

        go.SetActive(true);
        return view;
    }

    // ---------------- 高亮 ----------------

    private void RefreshHighlight()
    {
        ClearHighlight();

        var shape = Def.GetNormalizedShape(_rotation);
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