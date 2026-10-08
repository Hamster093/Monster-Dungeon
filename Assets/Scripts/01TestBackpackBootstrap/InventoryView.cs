/****************************************************
    文件：InventoryView.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/8 0:30:07
	功能：
*****************************************************/

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventoryView : MonoBehaviour,
    IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public int GridWidth = 7;
    public int GridHeight = 7;
    public float CellSize = 64f;

    public Color CellColor = new Color(1f, 1f, 1f, 0.08f);
    public Color ValidColor = new Color(0.3f, 1f, 0.3f, 0.55f);
    public Color InvalidColor = new Color(1f, 0.3f, 0.3f, 0.55f);

    private InventoryGrid _grid;
    private RectTransform _gridRoot;
    private RectTransform _itemsRoot;
    private Image[] _cellImages;
    private Font _font;

    private readonly Dictionary<PlacedItem, ItemView> _views = new Dictionary<PlacedItem, ItemView>();
    private readonly List<PlacedItem> _items = new List<PlacedItem>();

    private PlacedItem _dragging;
    private int _grabCellX, _grabCellY;
    private Vector2Int _originalOrigin;
    private int _originalRotation;
    private Vector2Int _currentTarget;
    private Vector2Int _lastMouseCell;
    private bool _hasLastMouseCell;

    public InventoryGrid Grid => _grid;
    public IReadOnlyList<PlacedItem> Items => _items;

    // ---------------- 构建 ----------------

    public void Build(Transform canvasTransform, Font font)
    {
        _font = font;
        _grid = new InventoryGrid(GridWidth, GridHeight);

        // GridRoot：pivot 左下角，作为全局坐标系
        var gridGo = new GameObject("GridRoot", typeof(RectTransform));
        _gridRoot = (RectTransform)gridGo.transform;
        _gridRoot.SetParent(canvasTransform, false);
        _gridRoot.anchorMin = new Vector2(0.5f, 0.5f);
        _gridRoot.anchorMax = new Vector2(0.5f, 0.5f);
        _gridRoot.pivot = new Vector2(0f, 0f);
        _gridRoot.sizeDelta = new Vector2(GridWidth * CellSize, GridHeight * CellSize);
        _gridRoot.anchoredPosition = new Vector2(
            -GridWidth * CellSize * 0.5f,
            -GridHeight * CellSize * 0.5f);

        // 背景格子
        _cellImages = new Image[GridWidth * GridHeight];
        for (int y = 0; y < GridHeight; y++)
        {
            for (int x = 0; x < GridWidth; x++)
            {
                var cellGo = new GameObject($"Cell_{x}_{y}", typeof(RectTransform), typeof(Image));
                var r = (RectTransform)cellGo.transform;
                r.SetParent(_gridRoot, false);
                r.pivot = new Vector2(0f, 0f);
                r.anchorMin = new Vector2(0f, 0f);
                r.anchorMax = new Vector2(0f, 0f);
                r.sizeDelta = new Vector2(CellSize - 2f, CellSize - 2f);
                r.anchoredPosition = new Vector2(x * CellSize + 1f, y * CellSize + 1f);

                var img = cellGo.GetComponent<Image>();
                img.color = CellColor;
                img.raycastTarget = false;
                _cellImages[y * GridWidth + x] = img;
            }
        }

        // 物品层
        var itemsGo = new GameObject("ItemsRoot", typeof(RectTransform));
        _itemsRoot = (RectTransform)itemsGo.transform;
        _itemsRoot.SetParent(_gridRoot, false);
        _itemsRoot.anchorMin = new Vector2(0f, 0f);
        _itemsRoot.anchorMax = new Vector2(0f, 0f);
        _itemsRoot.pivot = new Vector2(0f, 0f);
        _itemsRoot.sizeDelta = _gridRoot.sizeDelta;
        _itemsRoot.anchoredPosition = Vector2.zero;
    }

    // ---------------- 对外 API ----------------

    public PlacedItem AddItem(ItemDefinition def, Vector2Int origin, int rotation = 0)
    {
        if (!_grid.CanPlace(def, origin, rotation)) return null;

        var item = new PlacedItem(def);
        _grid.Place(item, origin, rotation);

        var view = ItemView.Create(item, _itemsRoot, CellSize, _font);
        _views[item] = view;
        _items.Add(item);
        return item;
    }

    public void RemoveItem(PlacedItem item)
    {
        if (item == null) return;
        _grid.Remove(item);

        if (_views.TryGetValue(item, out var view) && view != null)
            Destroy(view.gameObject);

        _views.Remove(item);
        _items.Remove(item);
    }

    // ---------------- 拖拽 ----------------

    public void OnPointerDown(PointerEventData e)
    {
        if (_dragging != null) return;
        if (!ScreenToCell(e.position, e.pressEventCamera, out var mouseCell)) return;

        var item = _grid.Get(mouseCell);
        if (item == null) return;

        _dragging = item;
        _originalOrigin = item.Origin;
        _originalRotation = item.Rotation;
        _grabCellX = mouseCell.x - item.Origin.x;
        _grabCellY = mouseCell.y - item.Origin.y;
        _lastMouseCell = mouseCell;
        _hasLastMouseCell = true;
        _currentTarget = item.Origin;

        _grid.Remove(item);
        _views[item].SetDragging(true);
        RevalidateDragTarget();
    }

    public void OnDrag(PointerEventData e)
    {
        if (_dragging == null) return;
        if (!ScreenToCell(e.position, e.pressEventCamera, out var mouseCell)) return;

        _lastMouseCell = mouseCell;
        _hasLastMouseCell = true;
        RevalidateDragTarget();
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (_dragging == null) return;

        bool valid = _grid.CanPlace(_dragging.Def, _currentTarget, _dragging.Rotation, _dragging);
        if (valid)
            _grid.Place(_dragging, _currentTarget, _dragging.Rotation);
        else
            _grid.Place(_dragging, _originalOrigin, _originalRotation);

        _dragging.View.Rebuild();
        _dragging.View.SetDragging(false);

        _dragging = null;
        _hasLastMouseCell = false;
        ClearHighlight();
    }

    private void Update()
    {
        if (_dragging == null) return;

        if (Input.GetKeyDown(KeyCode.R) && _dragging.Def.canRotate)
        {
            _dragging.Rotation = (_dragging.Rotation + 90) % 360;
            _dragging.View.Rebuild();

            // 保持鼠标下的格子不变
            if (_hasLastMouseCell)
            {
                _grabCellX = _lastMouseCell.x - _currentTarget.x;
                _grabCellY = _lastMouseCell.y - _currentTarget.y;
            }
            RevalidateDragTarget();
        }
    }

    private void RevalidateDragTarget()
    {
        Vector2Int target;
        if (_hasLastMouseCell)
        {
            target = new Vector2Int(
                _lastMouseCell.x - _grabCellX,
                _lastMouseCell.y - _grabCellY);
        }
        else
        {
            target = _currentTarget;
        }

        _currentTarget = target;

        bool valid = _grid.CanPlace(_dragging.Def, target, _dragging.Rotation, _dragging);

        _dragging.View.Rect.anchoredPosition = new Vector2(
            target.x * CellSize,
            target.y * CellSize);

        HighlightCells(target, _dragging.Rotation, valid);
    }

    // ---------------- 视觉辅助 ----------------

    private void HighlightCells(Vector2Int origin, int rotation, bool valid)
    {
        ClearHighlight();

        var color = valid ? ValidColor : InvalidColor;
        var shape = _dragging.Def.GetShape(rotation);

        for (int i = 0; i < shape.Length; i++)
        {
            var c = origin + shape[i];
            if (c.x < 0 || c.y < 0 || c.x >= GridWidth || c.y >= GridHeight) continue;
            _cellImages[c.y * GridWidth + c.x].color = color;
        }
    }

    private void ClearHighlight()
    {
        for (int i = 0; i < _cellImages.Length; i++)
            _cellImages[i].color = CellColor;
    }

    // ---------------- 坐标转换 ----------------

    private bool ScreenToCell(Vector2 screenPos, Camera cam, out Vector2Int cell)
    {
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _gridRoot, screenPos, cam, out var local))
        {
            cell = new Vector2Int(
                Mathf.FloorToInt(local.x / CellSize),
                Mathf.FloorToInt(local.y / CellSize));
            return true;
        }

        cell = default;
        return false;
    }
}