using UnityEngine;
using UnityEngine.UI;

public class DecomposePanel : PanelBase
{
    [Header("槽位")]
    [SerializeField] private ItemSlot _slot;
    [SerializeField] private Sprite _emptySprite;

    [Header("按钮")]
    [SerializeField] private Button _decomposeButton;
    [SerializeField] private Button _closeButton;

    private PlacedItem _pendingItem;
    private ItemStack _pendingStack;

    public override bool IsModal => false;

    public override void OnInit()
    {
        _slot.Bind(0, _emptySprite, _ => _pendingStack);

        // 左键：不响应
        _slot.OnLeftClickedOverride = _ => { };
        // 右键：退回棋盘
        _slot.OnRightClickedOverride = _ => ReturnToBoard();

        _decomposeButton.onClick.AddListener(OnDecompose);
        _closeButton.onClick.AddListener(() => UIManager.Instance.Close(this));

        Clear();
    }

    public override void OnOpen(object data = null)
    {
        base.OnOpen(data);
        Clear();
    }

    public override bool OnEscapePressed()
    {
        UIManager.Instance.Close(this);
        return true;
    }

    public bool IsPointerOverSlot(Vector2 screenPos, Camera cam)
    {
        var rt = _slot.transform as RectTransform;
        return rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, screenPos, cam);
    }

    public void Accept(PlacedItem item)
    {
        _pendingItem = item;

        if (!int.TryParse(item.Def.Id, out int id))
        {
            Debug.LogWarning($"[DecomposePanel] Def.Id 非数字：{item.Def.Id}");
            _slot.Refresh(null, _emptySprite);
            _slot.SetSubText("");
            return;
        }

        if (!ConfigDatabase.Instance.Items.TryGetValue(id, out var cfg))
        {
            Debug.LogWarning($"[DecomposePanel] Items 表找不到 id={id}");
            _slot.Refresh(null, _emptySprite);
            _slot.SetSubText("");
            return;
        }

        var data = ItemData.FromConfig(cfg);
        _pendingStack = new ItemStack { data = data, quantity = 1 };

        _slot.Refresh(_pendingStack, _emptySprite);
        _slot.SetSubText(cfg.materialValue > 0 ? $"{cfg.materialValue}G" : "");
        LoadIconAsync(cfg.iconAddress);
    }

    private async void LoadIconAsync(string addr)
    {
        if (string.IsNullOrEmpty(addr)) return;
        var sp = await SpriteLoader.LoadAsync(addr);
        if (this == null || _slot == null) return;
        if (sp != null) _slot.SetIcon(sp);
    }

    private void OnDecompose()
    {
        if (_pendingItem == null) return;

        if (int.TryParse(_pendingItem.Def.Id, out int id)
            && ConfigDatabase.Instance.Items.TryGetValue(id, out var cfg))
        {
            SystemManager.Instance.Economy.AddCash(cfg.materialValue);
        }

        if (_pendingItem.View != null)
            Destroy(_pendingItem.View.gameObject);

        Clear();
    }

    private void ReturnToBoard()
    {
        if (_pendingItem == null || _pendingItem.View == null)
        {
            Clear();
            return;
        }

        var view = _pendingItem.View;
        var board = view.Board;

        if (board != null
            && !board.CanPlace(_pendingItem.Def, _pendingItem.Origin, _pendingItem.Rotation, _pendingItem))
        {
            if (!TryFindFreeOriginFor(board, out var freeOrigin))
            {
                Debug.LogWarning("[DecomposePanel] 退回失败：棋盘没有空位");
                Clear();
                return;
            }
            _pendingItem.Origin = freeOrigin;
        }

        view.gameObject.SetActive(true);
        board?.PlaceItem(_pendingItem, _pendingItem.Origin, _pendingItem.Rotation);

        Clear();
    }

    /// <summary>
    /// 在棋盘上扫描第一个能让整个形状放下的位置（考虑多格、障碍、已有占用）
    /// </summary>
    private bool TryFindFreeOriginFor(BackpackBoard board, out Vector2Int origin)
    {
        var def = _pendingItem.Def;
        var rot = _pendingItem.Rotation;

        for (int y = 0; y < board.Height; y++)
        {
            for (int x = 0; x < board.Width; x++)
            {
                var candidate = new Vector2Int(x, y);
                if (board.CanPlace(def, candidate, rot, _pendingItem))
                {
                    origin = candidate;
                    return true;
                }
            }
        }

        origin = default;
        return false;
    }

    private void Clear()
    {
        _pendingItem = null;
        _pendingStack = null;

        if (_slot != null)
        {
            _slot.Refresh(null, _emptySprite);
            _slot.SetSubText("");
            _slot.SetIcon(_emptySprite);
        }
    }

    public override void OnClose()
    {
        ReturnToBoard();
        base.OnClose();
    }
}