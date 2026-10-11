/****************************************************
    文件：StageTileLayoutConfig.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/12 0:00:27
	功能：分阶段的特殊地块布局配置 配置五个阶段的特殊地块布局
*****************************************************/


using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "StageTileLayoutConfig", menuName = "Backpack/StageTileLayoutConfig")]
public class StageTileLayoutConfig : ScriptableObject
{
    [Serializable]
    public class TilePlacement
    {
        [Tooltip("棋盘坐标：x=列(A=0)，y=行(从下往上)")]
        public Vector2Int coord;
        public SpecialTileType type;
    }

    [Serializable]
    public class StageLayout
    {
        public int stage;
        public List<TilePlacement> tiles = new();
    }

    [SerializeField] private List<StageLayout> _stages = new();

    public StageLayout GetStage(int stage)
    {
        foreach (var s in _stages)
            if (s.stage == stage) return s;
        return null;
    }

    /// <summary>把 "B2" 转成棋盘坐标（1 为最上面一行）</summary>
    public static Vector2Int Parse(string cell, int boardHeight = 7)
    {
        if (string.IsNullOrEmpty(cell) || cell.Length < 2) return Vector2Int.zero;
        int x = char.ToUpper(cell[0]) - 'A';
        int row = int.Parse(cell.Substring(1));
        int y = boardHeight - row;
        return new Vector2Int(x, y);
    }

    // ---------- 默认 5 阶段数据（右键菜单调用） ----------

    [ContextMenu("Reset to Defaults")]
    private void ResetToDefaults()
    {
        _stages = new List<StageLayout>
        {
            MakeStage(1,
                P("B2", SpecialTileType.OreBlock), P("C2", SpecialTileType.OreBlock), P("G2", SpecialTileType.OreBlock),
                P("E5", SpecialTileType.HumusPool), P("F5", SpecialTileType.HumusPool),
                P("E6", SpecialTileType.HumusPool), P("F6", SpecialTileType.HumusPool)),

            MakeStage(2,
                P("B1", SpecialTileType.OreBlock), P("C1", SpecialTileType.OreBlock), P("B6", SpecialTileType.OreBlock),
                P("E1", SpecialTileType.PrisonBlock), P("E2", SpecialTileType.PrisonBlock), P("F2", SpecialTileType.PrisonBlock),
                P("D5", SpecialTileType.HumusPool), P("E5", SpecialTileType.HumusPool),
                P("D6", SpecialTileType.HumusPool), P("E6", SpecialTileType.HumusPool)),

            MakeStage(3,
                P("B1", SpecialTileType.OreBlock), P("C1", SpecialTileType.OreBlock), P("B6", SpecialTileType.OreBlock),
                P("E1", SpecialTileType.PrisonBlock), P("E2", SpecialTileType.PrisonBlock), P("F2", SpecialTileType.PrisonBlock),
                P("B3", SpecialTileType.RiftBlock), P("C3", SpecialTileType.RiftBlock), P("C4", SpecialTileType.RiftBlock),
                P("E5", SpecialTileType.HumusPool), P("F5", SpecialTileType.HumusPool),
                P("E6", SpecialTileType.HumusPool), P("F6", SpecialTileType.HumusPool)),

            MakeStage(4,
                P("B1", SpecialTileType.OreBlock), P("C1", SpecialTileType.OreBlock), P("B6", SpecialTileType.OreBlock),
                P("E1", SpecialTileType.PrisonBlock), P("E2", SpecialTileType.PrisonBlock), P("F2", SpecialTileType.PrisonBlock),
                P("F4", SpecialTileType.PrisonBlock), P("G4", SpecialTileType.PrisonBlock), P("G5", SpecialTileType.PrisonBlock),
                P("B3", SpecialTileType.RiftBlock), P("C3", SpecialTileType.RiftBlock), P("C4", SpecialTileType.RiftBlock),
                P("D6", SpecialTileType.HumusPool), P("E6", SpecialTileType.HumusPool),
                P("D7", SpecialTileType.HumusPool), P("E7", SpecialTileType.HumusPool)),

            MakeStage(5,
                P("B2", SpecialTileType.OreBlock), P("G2", SpecialTileType.OreBlock), P("G3", SpecialTileType.OreBlock),
                P("E2", SpecialTileType.PrisonBlock), P("D3", SpecialTileType.PrisonBlock), P("E3", SpecialTileType.PrisonBlock),
                P("C7", SpecialTileType.PrisonBlock), P("D6", SpecialTileType.PrisonBlock), P("D7", SpecialTileType.PrisonBlock),
                P("F5", SpecialTileType.RiftBlock), P("G5", SpecialTileType.RiftBlock), P("F6", SpecialTileType.RiftBlock),
                P("A4", SpecialTileType.HumusPool), P("B4", SpecialTileType.HumusPool),
                P("A5", SpecialTileType.HumusPool), P("B5", SpecialTileType.HumusPool)),
        };
    }

    private static StageLayout MakeStage(int stage, params TilePlacement[] tiles)
    {
        return new StageLayout { stage = stage, tiles = new List<TilePlacement>(tiles) };
    }

    private static TilePlacement P(string cell, SpecialTileType type)
    {
        return new TilePlacement { coord = Parse(cell), type = type };
    }
}