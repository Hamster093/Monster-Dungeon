/****************************************************
    文件：BackpackBoard.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/9 21:51:02
	功能：棋盘，管理格子和物品的占用关系
*****************************************************/

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BackpackBoard : MonoBehaviour
{
    public static BackpackBoard Instance { get; private set; }

    [Header("拖拽层（留空则运行时自动创建 DragLayer_Runtime）")]
    public RectTransform DragLayer;

    [Header("引用")]
    [Tooltip("挂着 GridLayoutGroup 的节点，里面有 49 个 BackpackCell")]
    public RectTransform GridRoot;

    [Tooltip("物品层，所有 ItemView 的父物体")]
    public RectTransform ItemsRoot;

    [Header("棋盘尺寸")]
    public int Width = 7;
    public int Height = 7;

    private BackpackCell[,] _cells;
    private readonly List<ItemView> _items = new List<ItemView>();

    public IReadOnlyList<ItemView> Items => _items;
    public bool IsEditing => ShopSlotDragger.IsAnyDragging || System.Array.Exists(FindObjectsOfType<ItemView>(),v=>!v.IsGhost&&v.IsPlacing);
    public void CancelPlacement(){foreach(var v in FindObjectsOfType<ItemView>())if(!v.IsGhost)v.CancelPlacement();}
    [SerializeField] private Text _editHint;
    public const string IdleHint = "左键点击查看魔物详情，长按拖动改变魔物位置。";
    public const string DragHint = "按R旋转";
    private void Update()
    {
        if(!_editHint)return;
        bool visible=SystemManager.Instance?.Narrative?.IsPreparing!=false&&!RouteSelectionPanel.IsShowing;
        _editHint.gameObject.SetActive(visible);
        if(visible)_editHint.text=IsEditing?DragHint:IdleHint;
    }
    /// <summary>只在松手成功改变棋盘后调用；初始化／读档的PlaceItem不会触发保存。</summary>
    public void CommitEdit()
    {
        AudioManager.Instance?.PlayCue("place");
        BackpackChanged.Trigger();
        SystemManager.Instance?.Narrative?.Save();
    }


    [Header("收益配置")]
    [SerializeField] private TileAffinityConfig _tileConfig;
    [SerializeField] private MonsterIncomeConfig _incomeConfig;

    //  暴露给外部
    public TileAffinityConfig TileConfig => _tileConfig;
    public MonsterIncomeConfig IncomeConfig => _incomeConfig;

    private void Awake()
    {
        Instance = this;

        BuildCellMap();
        GetOrCreateDragLayer();
        Canvas.willRenderCanvases += AlignItemVisuals;
    }

    private void OnDestroy()
    {
        Canvas.willRenderCanvases -= AlignItemVisuals;
        if (Instance == this) Instance = null;
    }
    private void AlignItemVisuals()
    {
        // 画布缩放、布局或读档后，显示位置按真实格子重新对齐，不修改占格数据。
        foreach (var view in _items)
            if (view != null && view.Item != null && view.transform.parent == ItemsRoot)
                view.ApplyPosition(GetCellLocalPosition(view.Item.Origin));
    }

    private void BuildCellMap()
    {
        _cells = new BackpackCell[Width, Height];

        // 按 GridLayoutGroup 子物体顺序推断坐标
        // GridLayoutGroup 从 UpperLeft 开始水平排列：
        //   index 0 → 左上 (0, Height-1)
        //   index 6 → 右上 (Width-1, Height-1)
        //   index 7 → 下一行最左
        var cells = GridRoot.GetComponentsInChildren<BackpackCell>();
        for (int i = 0; i < cells.Length; i++)
        {
            int col = i % Width;
            int row = i / Width;             // 从上往下
            int y = Height - 1 - row;        // 转成从下往上
            cells[i].Coordinate = new Vector2Int(col, y);
            if (col >= 0 && col < Width && y >= 0 && y < Height)
                _cells[col, y] = cells[i];
        }
    }

    public BackpackCell GetCell(int x, int y)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height) return null;
        return _cells[x, y];
    }
    public BackpackCell GetCell(Vector2Int c) => GetCell(c.x, c.y);

    /// <summary>能否放置</summary>
    public bool CanPlace(ItemShapeDefinition def, Vector2Int origin, int rotation, PlacedItem ignore = null)
    {
        int other=0;foreach(var v in _items)if(v!=null&&!v.IsGhost&&v.Item!=null&&v.Item!=ignore)other++;
        if(other>=8)return false;
        var shape = def.GetNormalizedShape(rotation);
        foreach (var offset in shape)
        {
            var c = origin + offset;
            var cell = GetCell(c);
            if (cell == null) return false;
            if (cell.IsObstacle) return false;
            if (cell.Occupant != null && cell.Occupant != ignore) return false;
        }
        return true;
    }

    /// <summary>放置到棋盘（会清掉旧占用）</summary>
    public void PlaceItem(PlacedItem item, Vector2Int origin, int rotation)
    {
        // 先清除旧的占用
        foreach (var c in item.GetCells())
        {
            var old = GetCell(c);
            if (old != null && old.Occupant == item) old.Occupant = null;
        }

        item.Origin = origin;
        item.Rotation = rotation;

        foreach (var c in item.GetCells())
        {
            var cell = GetCell(c);
            if (cell != null) cell.Occupant = item;
        }

        if (item.View != null)
        {
            RegisterItem(item.View);
            item.View.SetRotationSilently(rotation);
            item.View.ApplyPosition(GetCellLocalPosition(origin));
        }
    }

    public void RemoveItem(PlacedItem item)
    {
        foreach (var c in item.GetCells())
        {
            var cell = GetCell(c);
            if (cell != null && cell.Occupant == item) cell.Occupant = null;
        }

        if (item.View != null) UnregisterItem(item.View);   // ★ 同步移除
    }

    /// <summary>格子 (x,y) 左下角在 ItemsRoot 局部坐标中的位置</summary>
    public Vector3 GetCellLocalPosition(Vector2Int coord)
    {
        var cell = GetCell(coord);
        if (cell == null || ItemsRoot == null) return Vector3.zero;

        var corners = new Vector3[4];
        cell.Rect.GetWorldCorners(corners);
        Vector3 worldBL = corners[0]; // 左下角
        return ItemsRoot.InverseTransformPoint(worldBL);
    }

    /// <summary>屏幕坐标 → 棋盘坐标</summary>
    public bool ScreenToBoardCoord(Vector2 screenPos, Camera cam, out Vector2Int coord)
    {
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                var cell = _cells[x, y];
                if (cell == null) continue;
                if (RectTransformUtility.RectangleContainsScreenPoint(cell.Rect, screenPos, cam))
                {
                    coord = new Vector2Int(x, y);
                    return true;
                }
            }
        }
        coord = default;
        return false;
    }

    public void RegisterItem(ItemView v)
    {
        if (v == null) return;
        if(v.IsGhost)return;
        if (_items.Contains(v)) return;   // 幂等
        _items.Add(v);
    }

    public void UnregisterItem(ItemView v)
    {
        if (v == null) return;
        _items.Remove(v);
    }

    /// <summary>在棋盘上创建一个 ItemView（用于商店购买后落位）</summary>
    public ItemView CreateItemOnBoard(ItemShapeDefinition def, Vector2Int origin,
                                      int rotation, ItemView prefab)
    {
        if (!CanPlace(def, origin, rotation)) return null;

        var view = Instantiate(prefab, ItemsRoot);
        view.Init(def, this, rotation, origin);
        return view;
    }

    public RectTransform GetOrCreateDragLayer()
    {
        if (DragLayer != null) return DragLayer;

        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return null;

        var t = canvas.transform.Find("DragLayer_Runtime");
        if (t != null) { DragLayer = t as RectTransform; return DragLayer; }

        var go = new GameObject("DragLayer_Runtime", typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(canvas.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.SetAsLastSibling();
        DragLayer = rect;
        return DragLayer;
    }
    public void CleanupDestroyedItems()
    {
        _items.RemoveAll(v => v == null);
    }
}
