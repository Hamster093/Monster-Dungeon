/****************************************************
    文件：BackpackCell.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/9 21:50:28
	功能：棋盘格子类
*****************************************************/

using UnityEngine;
using UnityEngine.UI;

public enum SpecialTileType
{
    None = 0,        // 普通地块
    OreBlock,        // 矿石块
    HumusPool,       // 腐殖池
    PrisonBlock,     // 监牢块
    RiftBlock,       // 裂口块
}

[RequireComponent(typeof(RectTransform))]
public class BackpackCell : MonoBehaviour
{
    [Tooltip("该格在棋盘中的坐标，运行时会自动填充，也可以手动设置")]
    public Vector2Int Coordinate;

    [Header("特殊地块")]
    [SerializeField] private SpecialTileType _specialType = SpecialTileType.None;
    public SpecialTileType SpecialType => _specialType;

    [Tooltip("勾选后该格为障碍物，不能放任何物品")]
    public bool IsObstacle => _specialType != SpecialTileType.None;

    [Header("颜色配置")]
    [Tooltip("普通地块颜色")]
    public Color NormalColor = new Color(1f, 1f, 1f, 0.1f);

    [Tooltip("矿石块")]
    public Color OreBlockColor = new Color(0xC9 / 255f, 0x9B / 255f, 0x48 / 255f, 1f);

    [Tooltip("腐殖池")]
    public Color HumusPoolColor = new Color(0x63 / 255f, 0x95 / 255f, 0x79 / 255f, 1f);

    [Tooltip("监牢块")]
    public Color PrisonBlockColor = new Color(0x94 / 255f, 0x78 / 255f, 0xAB / 255f, 1f);

    [Tooltip("裂口块")]
    public Color RiftBlockColor = new Color(0xBD / 255f, 0x6C / 255f, 0x71 / 255f, 1f);

    [Tooltip("障碍物状态的颜色")]
    public Color ObstacleColor = new Color(0.15f, 0.15f, 0.15f, 1f);

    public Image Image;
    public RectTransform Rect { get; private set; }

    [HideInInspector] public PlacedItem Occupant;

    private void Awake()
    {
        Rect = GetComponent<RectTransform>();
        if (Image == null) Image = GetComponent<Image>();
        ApplyColor();
    }

    /// <summary>恢复该格子的默认视觉（按 IsObstacle 决定用哪个颜色）</summary>
    public void RefreshVisual()
    {
        ApplyColor();
    }

    private void ApplyColor()
    {
        if (Image == null) return;
        Image.color = GetColorFor(_specialType);
    }

    private void OnValidate()
    {
        if (Image == null) Image = GetComponent<Image>();
        ApplyColor();
    }
    /// <summary>运行时切换地块类型（棋盘阶段刷新用）</summary>
    public void SetSpecialType(SpecialTileType type)
    {
        if (_specialType == type) return;
        _specialType = type;
        ApplyColor();   // 你原本的着色方法
    }
    private Color GetColorFor(SpecialTileType type)
    {
        switch (type)
        {
            case SpecialTileType.OreBlock: return OreBlockColor;
            case SpecialTileType.HumusPool: return HumusPoolColor;
            case SpecialTileType.PrisonBlock: return PrisonBlockColor;
            case SpecialTileType.RiftBlock: return RiftBlockColor;
            default: return NormalColor;
        }
    }
}