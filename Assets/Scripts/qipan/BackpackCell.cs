/****************************************************
    文件：BackpackCell.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/9 21:50:28
	功能：棋盘格子类
*****************************************************/

using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class BackpackCell : MonoBehaviour
{
    [Tooltip("该格在棋盘中的坐标，运行时会自动填充，也可以手动设置")]
    public Vector2Int Coordinate;

    [Tooltip("勾选后该格为障碍物，不能放任何物品")]
    public bool IsObstacle;

    [Header("颜色配置")]
    [Tooltip("正常状态（可放置）的格子颜色")]
    public Color NormalColor = new Color(1f, 1f, 1f, 0.1f);

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
        Image.color = IsObstacle ? ObstacleColor : NormalColor;
    }

    private void OnValidate()
    {
        if (Image == null) Image = GetComponent<Image>();
        ApplyColor();
    }
}