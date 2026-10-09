/****************************************************
    文件：ItemView.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/9 21:51:22
	功能：
*****************************************************/

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class ItemView : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("配置")]
    public ItemShapeDefinition Def;
    public BackpackBoard Board;

    [Tooltip("单格像素尺寸，要和棋盘的 CellSize 一致")]
    public float CellSize = 95f;

    [Tooltip("格子之间的间距，要和 GridLayoutGroup 的 Spacing 一致")]
    public float Spacing = 5f;

    [Tooltip("每个小方块的预制体（挂 Image），留空则运行时自动创建")]
    public GameObject ShapeCellPrefab;

    [Header("拖拽表现")]
    public float DragAlpha = 0.85f;

    public bool IsGhost;// 幽灵/预览用，不参与棋盘登记

    // 运行时
    private RectTransform _rect;
    private CanvasGroup _canvasGroup;
    private readonly List<GameObject> _shapeCells = new List<GameObject>();

    private PlacedItem _item;
    private bool _isDragging;
    private Vector2Int _grabCellOffset;   // 抓取时鼠标格相对物品 origin 的偏移
    private Vector2Int _targetOrigin;
    private bool _targetValid;

    private readonly List<BackpackCell> _highlighted = new List<BackpackCell>();

    public PlacedItem Item => _item;

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();
        _rect.pivot = new Vector2(0f, 0f);
        _rect.anchorMin = new Vector2(0f, 0f);
        _rect.anchorMax = new Vector2(0f, 0f);

        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        _canvasGroup.blocksRaycasts = true;

        if (Board == null) Board = FindObjectOfType<BackpackBoard>();
    }

    private void Start()
    {
        if (Def != null && _shapeCells.Count == 0)
            Rebuild();
    }

    private void OnEnable()
    {
        if (!IsGhost && Board != null) Board.RegisterItem(this);
    }
    private void OnDisable()
    {
        if (!IsGhost && Board != null) Board.UnregisterItem(this);
    }

    /// <summary>外部初始化（拖到场景里做测试也可直接在 Inspector 填 Def）</summary>
    public void Init(ItemShapeDefinition def, BackpackBoard board,
                 int rotation = 0, Vector2Int? preferredOrigin = null)
    {
        Def = def;
        Board = board;
        if (Board != null) Board.RegisterItem(this);

        _item = new PlacedItem(def) { View = this, Rotation = rotation };
        Rebuild();

        // 决定落点
        Vector2Int origin;
        if (preferredOrigin.HasValue
            && Board.CanPlace(def, preferredOrigin.Value, rotation, _item))
        {
            origin = preferredOrigin.Value;
        }
        else if (!TryFindFreeOrigin(out origin))
        {
            Debug.LogWarning($"[ItemView] 没有空位放下 {def.Id}");
            return;
        }

        Board.PlaceItem(_item, origin, rotation);
    }

    /// <summary>根据当前形状重建小方块</summary>
    public void Rebuild()
    {
        EnsureInit();
        if (Def == null) return;
        if (_item == null) _item = new PlacedItem(Def) { View = this };

        var shape = Def.GetNormalizedShape(_item.Rotation);
        var size = Def.GetSize(_item.Rotation);

        // 物品整体尺寸
        float w = size.x * CellSize + Mathf.Max(0, size.x - 1) * Spacing;
        float h = size.y * CellSize + Mathf.Max(0, size.y - 1) * Spacing;
        _rect.sizeDelta = new Vector2(w, h);

        // 复用/补齐小方块
        while (_shapeCells.Count < shape.Length)
        {
            var go = ShapeCellPrefab != null
                ? Instantiate(ShapeCellPrefab, _rect)
                : CreateDefaultCell();

            var r = go.GetComponent<RectTransform>();
            r.pivot = new Vector2(0f, 0f);
            r.anchorMin = new Vector2(0f, 0f);
            r.anchorMax = new Vector2(0f, 0f);
            _shapeCells.Add(go);
        }

        for (int i = 0; i < _shapeCells.Count; i++)
        {
            var go = _shapeCells[i];
            if (i < shape.Length)
            {
                go.SetActive(true);
                var r = go.GetComponent<RectTransform>();
                r.sizeDelta = new Vector2(CellSize, CellSize);
                r.anchoredPosition = new Vector2(
                    shape[i].x * (CellSize + Spacing),
                    shape[i].y * (CellSize + Spacing));

                var img = go.GetComponent<Image>();
                if (img != null)
                {
                    img.color = Def.Color;
                    img.raycastTarget = true; // 让点击/拖拽冒泡到 ItemView
                }
            }
            else
            {
                go.SetActive(false);
            }
        }
    }

    private GameObject CreateDefaultCell()
    {
        var go = new GameObject("Cell", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(_rect, false);   // ← 关键：挂到 ItemView 下面
        return go;
    }

    public void ApplyPosition(Vector3 localPos)
    {
        _rect.localPosition = localPos;
    }

    public void SetRotationSilently(int rotation)
    {
        if (_item == null) _item = new PlacedItem(Def) { View = this };
        _item.Rotation = rotation;
        Rebuild();
    }

    // ---------------- 拖拽 ----------------

    public void OnBeginDrag(PointerEventData e)
    {
        if (Board == null) return;
        if (_item == null) _item = new PlacedItem(Def) { View = this };

        _isDragging = true;
        _canvasGroup.alpha = DragAlpha;
        _rect.SetAsLastSibling();

        // 记录抓取偏移：鼠标下的格子 - 当前 origin
        var cam = e.pressEventCamera;
        if (Board.ScreenToBoardCoord(e.position, cam, out var mouseCell))
            _grabCellOffset = mouseCell - _item.Origin;
        else
            _grabCellOffset = Vector2Int.zero;

        // 从棋盘数据里移除，让拖拽过程不阻挡自己
        Board.RemoveItem(_item);
    }

    public void OnDrag(PointerEventData e)
    {
        if (!_isDragging) return;
        var cam = e.pressEventCamera;

        if (Board.ScreenToBoardCoord(e.position, cam, out var mouseCell))
        {
            var origin = mouseCell - _grabCellOffset;
            _targetOrigin = origin;
            _targetValid = Board.CanPlace(Def, origin, _item.Rotation, _item);

            _rect.localPosition = Board.GetCellLocalPosition(origin);
            HighlightTarget(origin, _targetValid);
        }
        else
        {
            // 鼠标不在棋盘上 → 跟随鼠标
            var parentRect = _rect.parent as RectTransform;
            if (parentRect != null &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRect, e.position, cam, out var lp))
            {
                _rect.localPosition = lp;
            }

            _targetValid = false;
            ClearHighlight();
        }
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (!_isDragging) return;
        _isDragging = false;
        _canvasGroup.alpha = 1f;
        ClearHighlight();

        if (_targetValid)
            Board.PlaceItem(_item, _targetOrigin, _item.Rotation);
        else
            Board.PlaceItem(_item, _item.Origin, _item.Rotation); // 回原位
    }

    // ---------------- R 键旋转 ----------------

    private void Update()
    {
        if (!_isDragging) return;
        if (Def == null || !Def.CanRotate) return;

        if (Input.GetKeyDown(KeyCode.R))
        {
            _item.Rotation = (_item.Rotation + 90) % 360;
            Rebuild();

            // 旋转后重新以"当前鼠标"为锚点计算抓取偏移
            // 简单起见，旋转后把抓取偏移归零（左下角对齐鼠标）
            _grabCellOffset = Vector2Int.zero;
        }
    }

    // ---------------- 高亮 ----------------

    private void HighlightTarget(Vector2Int origin, bool valid)
    {
        ClearHighlight();
        var shape = Def.GetNormalizedShape(_item.Rotation);
        for (int i = 0; i < shape.Length; i++)
        {
            var c = origin + shape[i];
            var cell = Board.GetCell(c);
            if (cell == null || cell.Image == null) continue;

            var original = cell.Image.color;
            var tint = valid
                ? new Color(0.4f, 1f, 0.4f, original.a)
                : new Color(1f, 0.4f, 0.4f, original.a);
            cell.Image.color = tint;
            _highlighted.Add(cell);
        }
    }

    private void ClearHighlight()
    {
        for (int i = 0; i < _highlighted.Count; i++)
            if (_highlighted[i] != null) _highlighted[i].RefreshVisual();
        _highlighted.Clear();
    }

    private bool TryFindFreeOrigin(out Vector2Int origin)
    {
        // 从下往上、从左往右扫一遍，找第一个能放下的位置
        for (int y = 0; y < Board.Height; y++)
        {
            for (int x = 0; x < Board.Width; x++)
            {
                var candidate = new Vector2Int(x, y);
                if (Board.CanPlace(Def, candidate, _item.Rotation, _item))
                {
                    origin = candidate;
                    return true;
                }
            }
        }
        origin = default;
        return false;
    }
    /// <summary>
    /// 确保初始化
    /// </summary>
    private void EnsureInit()
    {
        if (_rect == null)
        {
            _rect = GetComponent<RectTransform>();
            _rect.pivot = new Vector2(0f, 0f);
            _rect.anchorMin = new Vector2(0f, 0f);
            _rect.anchorMax = new Vector2(0f, 0f);
        }
        if (_canvasGroup == null)
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.blocksRaycasts = true;
        }
    }

}