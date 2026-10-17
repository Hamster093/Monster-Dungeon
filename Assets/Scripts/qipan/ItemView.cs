/****************************************************
    文件：ItemView.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/9 21:51:22
	功能：棋盘上的物品视图，负责显示和拖拽
*****************************************************/

using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class ItemView : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
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

    [Header("交互遮罩")]
    [Tooltip("遮罩小方块的预制体，留空则用半透黑 Image 自动创建")]
    public GameObject MaskCellPrefab;

    [Tooltip("遮罩颜色")]
    public Color MaskColor = new Color(0f, 0f, 0f, 0.55f);

    private readonly List<GameObject> _maskCells = new List<GameObject>();
    private bool _isInteracted;


    private int _originalRotation;   // 拖拽开始时的旋转


    public bool IsGhost;// 幽灵/预览用，不参与棋盘登记

    private Transform _originalParent;
    private Vector2 _grabPixelOffset; //抓取点在物品内的像素偏移（左下角为原点）

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

        if (!IsGhost && Board == null)
            Board = FindObjectOfType<BackpackBoard>();
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
                 int rotation = 0, Vector2Int? preferredOrigin = null,
                 MonsterInstance instance = null,     
                 bool interacted = false)
    {

        Def = def;
        Board = board;
        if (Board != null) Board.RegisterItem(this);

        _item = new PlacedItem(def)
        {
            View = this,
            Rotation = rotation,
            Instance = instance,       
        };
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
                    img.raycastTarget = true;

                    // 判断是否进化
                    bool isEvolved = _item != null && _item.Instance != null && _item.Instance.isEvolved;

                    if (isEvolved)
                    {
                        // 进化状态
                        if (Def.EvolvedSprite != null)
                        {
                            img.sprite = Def.EvolvedSprite;
                            img.color = Color.white;
                        }
                        else
                        {
                            // 没配进化图，就兜底用普通图
                            img.sprite = Def.NormalSprite;
                            img.color = Def.NormalSprite != null ? Color.white : Def.Color;
                        }
                    }
                    else
                    {
                        // 未进化状态（默认也用图片）
                        if (Def.NormalSprite != null)
                        {
                            img.sprite = Def.NormalSprite;
                            img.color = Color.white;
                        }
                        else
                        {
                            // 没配图，用纯色兜底
                            img.sprite = null;
                            img.color = Def.Color;
                        }
                    }
                }
            }
            else
            {
                go.SetActive(false);
            }
            RefreshMask();
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

        if (IsGhost) return;
        if (Board == null || !Board.AllowDrag) return;

        if (Board == null) return;
        if (_item == null) _item = new PlacedItem(Def) { View = this };

        _isDragging = true;
        _canvasGroup.alpha = DragAlpha;

        _originalRotation = _item.Rotation;

        var cam = e.pressEventCamera;

        // 1. 记录抓取偏移（用棋盘坐标算）
        if (Board.ScreenToBoardCoord(e.position, cam, out var mouseCell))
            _grabCellOffset = mouseCell - _item.Origin;
        else
            _grabCellOffset = Vector2Int.zero;

        _grabPixelOffset = new Vector2(
            _grabCellOffset.x * (CellSize + Spacing),
            _grabCellOffset.y * (CellSize + Spacing));

        // 2. 移到 DragLayer 顶层
        var dragLayer = Board.GetOrCreateDragLayer();
        if (dragLayer != null)
        {
            _originalParent = _rect.parent;
            _rect.SetParent(dragLayer, true);
            _rect.SetAsLastSibling();
        }

        // 3. 立即把物品对齐到鼠标（不然会先闪一下原位）
        FollowMouseToLayer(e);

        // 4. 从棋盘数据中移除（拖拽期间不阻挡自己）
        Board.RemoveItem(_item);
    }

    public void OnDrag(PointerEventData e)
    {
        if (!_isDragging) return;
        var cam = e.pressEventCamera;

        // 幽灵跟随鼠标（在 DragLayer 坐标系下）
        FollowMouseToLayer(e);

        // 判断棋盘落点
        if (Board.ScreenToBoardCoord(e.position, cam, out var mouseCell))
        {
            var origin = mouseCell - _grabCellOffset;
            _targetOrigin = origin;
            _targetValid = Board.CanPlace(Def, origin, _item.Rotation, _item);

            HighlightTarget(origin, _targetValid);
        }
        else
        {
            _targetValid = false;
        }
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (!_isDragging) return;
        _isDragging = false;
        _canvasGroup.alpha = 1f;
        ClearHighlight();

        // 回到原父节点（ItemsRoot）
        if (_originalParent != null)
        {
            _rect.SetParent(_originalParent, false);
            _originalParent = null;
        }

        // 先判断是否落在分解窗口上
        var decompose = UIManager.Instance.Get<DecomposePanel>();
       
        if (decompose != null && decompose.IsOpen
            && decompose.IsPointerOverSlot(e.position, e.pressEventCamera))
        {
            decompose.Accept(_item);
            gameObject.SetActive(false);   // 本体隐藏，等拆完/取消再处理
            return;
        }

        //  拖回魔物仓库
        var shop = UIManager.Instance.Get<ShopPanel>();
        if (shop != null && shop.IsOpen && shop.IsStorageMode
            && shop.IsPointerOverAnySlot(e.position, e.pressEventCamera, out _))
        {
            if (shop.IsStorageFull)
            {
                Debug.LogWarning("[ItemView] 仓库已满，无法退回");
                if (_targetValid)
                    Board.PlaceItem(_item, _targetOrigin, _item.Rotation);
                else
                    Board.PlaceItem(_item, _item.Origin, _originalRotation);
                return;
            }

            // 把原来的实例塞回去
            if (_item.Instance != null)
            {
                MonsterStorageModel.Instance.Reclaim(_item.Instance);
            }
            else
            {
                // 兜底：老物品没实例的情况
                if (int.TryParse(_item.Def.Id, out int id))
                    MonsterStorageModel.Instance.Add(id);
            }

            Board.RemoveItem(_item);
            shop.SwitchToStorage();
            Destroy(gameObject);
            return;
        }
        // 处理落点
        if (_targetValid)
            Board.PlaceItem(_item, _targetOrigin, _item.Rotation);
        else
            Board.PlaceItem(_item, _item.Origin, _originalRotation);
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
            _grabCellOffset = Vector2Int.zero;
            _grabPixelOffset = Vector2.zero;
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

            // 透明度直接写死 0.8f
            var tint = valid
                ? new Color(0.4f, 1f, 0.4f, 0.8f)
                : new Color(1f, 0.4f, 0.4f, 0.8f);
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

    /// <summary>让物品跟着鼠标，且抓取点保持在鼠标下</summary>
    private void FollowMouseToLayer(PointerEventData e)
    {
        var parentRect = _rect.parent as RectTransform;
        if (parentRect == null) return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect, e.position, e.pressEventCamera, out var lp))
        {
            _rect.localPosition = lp - _grabPixelOffset;
        }
    }
    /// <summary>
    /// 点击的失散代码
    /// </summary>
    /// <param name="e"></param>
    public void OnPointerClick(PointerEventData e)
    {
        if (Vector2.Distance(e.pressPosition, e.position) > 10f) return;
        if (IsGhost) return;

        // 拦截右键点击
        if (e.button == PointerEventData.InputButton.Right)
        {
            // 通知棋盘，我右键了一个魔物
            BackpackBoard.Instance.NotifyItemRightClicked(_item);
            return;
        }
        //左键点击逻辑
        if (Board == null || !Board.AllowClick) return;
        if (_isInteracted) return;

        var inst = _item.Instance;
        if (inst == null)
        {
            Debug.Log($"[点击] {_item.Def.DisplayName}（无运行时实例）");
            Board.NotifyItemClicked(_item);
            return;
        }

        var db = ConfigDatabase.Instance;
        MonsterConfig cfg = null;
        ItemConfig icfg = null;
        if (db != null)
        {
            db.Monsters?.TryGetValue(inst.monsterId, out cfg);
            db.Items?.TryGetValue(inst.monsterId, out icfg);
        }

        // 用工厂从 ItemConfig 生成展示用 ItemData
        ItemData display;
        if (icfg != null)
        {
            display = ItemData.FromConfig(icfg);
        }
        else
        {
            // 兜底：没有 Items 配置时不崩，名字用运行时数据
            display = new ItemData
            {
                id = inst.monsterId,
                itemName = cfg != null ? cfg.name : _item.Def.DisplayName,
                description = "（无描述）",
                iconAddress = null,
                shapeId = null,
            };
        }

        if (cfg != null && !string.IsNullOrEmpty(cfg.name))
            display.itemName = cfg.name;

        var args = new DescriptionPanelArgs
        {
            Item = display,
            Level = cfg != null ? cfg.level.ToString() : null,

            // 如果已经进化过，就不显示按钮了
            ShowUseButton = !inst.isEvolved,

            OnUse = () =>
            {
                // 1. 拦截已进化
                if (inst.isEvolved)
                {
                    Debug.LogWarning("[进化] 该魔物已经进化过了");
                    return;
                }

                // 2. 尝试消耗进化道具（id=7）
                if (!BackpackPanel.TryConsumeItem(7))
                {
                    Debug.LogWarning("[进化] 缺少进化道具(id=7)，进化失败");
                    return;
                }

                // 3. 消耗成功，开始进化
                // 随机获取一个非 None 的进化方向
                var dirs = (EvolutionDirection[])System.Enum.GetValues(typeof(EvolutionDirection));
                EvolutionDirection chosenDir = EvolutionDirection.Aggressive; // 默认兜底
                if (dirs.Length > 1)
                {
                    // 跳过下标 0（None），随机选一个方向
                    chosenDir = dirs[Random.Range(1, dirs.Length)];
                }

                // 4. 修改实例状态并发提升
                if (inst.Evolve(chosenDir))
                {
                    ToastManager.Show($"[进化成功] 魔物ID {inst.monsterId} 进化方向：{chosenDir}");
                    Rebuild();

                    // 顺便把面板关掉，免得玩家重复点击
                    UIManager.Instance.Close<DescriptionPanel>();
                }
            },

            UseButtonInteractable = true,
        };

        if (!UIManager.Instance.IsOpen<DescriptionPanel>())
            UIManager.Instance.Open<DescriptionPanel>(args);

        Board.NotifyItemClicked(_item);
    }


    public bool IsInteracted => _isInteracted;

    private void RefreshMask()
    {
        if (Def == null) return;
        if (_item == null) _item = new PlacedItem(Def) { View = this };

        var shape = Def.GetNormalizedShape(_item.Rotation);

        // 补齐遮罩格子
        while (_maskCells.Count < shape.Length)
        {
            GameObject go;
            if (MaskCellPrefab != null)
            {
                go = Instantiate(MaskCellPrefab, _rect);
            }
            else
            {
                go = new GameObject("MaskCell", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(_rect, false);
                var img = go.GetComponent<Image>();
                img.color = MaskColor;
                img.raycastTarget = false;
            }

            var r = go.GetComponent<RectTransform>();
            r.pivot = new Vector2(0f, 0f);
            r.anchorMin = new Vector2(0f, 0f);
            r.anchorMax = new Vector2(0f, 0f);

            _maskCells.Add(go);
        }

        for (int i = 0; i < _maskCells.Count; i++)
        {
            var go = _maskCells[i];
            if (i < shape.Length && _isInteracted)
            {
                go.SetActive(true);
                go.transform.SetAsLastSibling();   // 保证盖在 shapeCells 上面
                var r = go.GetComponent<RectTransform>();
                r.sizeDelta = new Vector2(CellSize, CellSize);
                r.anchoredPosition = new Vector2(
                    shape[i].x * (CellSize + Spacing),
                    shape[i].y * (CellSize + Spacing));
            }
            else
            {
                go.SetActive(false);
            }
        }
    }

}