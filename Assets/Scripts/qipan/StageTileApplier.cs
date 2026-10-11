/****************************************************
    文件：StageTileApplier.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/12 0:01:19
	功能：把某一阶段的特殊地块刷到棋盘上
*****************************************************/

using UnityEngine;

public static class StageTileApplier
{
    /// <summary>
    /// 清空棋盘上所有特殊地块，然后按指定阶段重新刷。
    /// </summary>
    public static void Apply(BackpackBoard board, StageTileLayoutConfig config, int stage)
    {
        if (board == null) { Debug.LogError("[StageTileApplier] board 为空"); return; }
        if (config == null) { Debug.LogError("[StageTileApplier] config 为空"); return; }

        // 1. 全部清空为普通格
        for (int y = 0; y < board.Height; y++)
        {
            for (int x = 0; x < board.Width; x++)
            {
                var cell = board.GetCell(x, y);
                if (cell != null) cell.SetSpecialType(SpecialTileType.None);
            }
        }

        // 2. 取阶段布局
        var layout = config.GetStage(stage);
        if (layout == null)
        {
            Debug.LogWarning($"[StageTileApplier] 没有阶段 {stage} 的配置");
            return;
        }

        // 3. 刷格子
        foreach (var t in layout.tiles)
        {
            var cell = board.GetCell(t.coord);
            if (cell == null)
            {
                Debug.LogWarning($"[StageTileApplier] 坐标 {t.coord} 越界，跳过");
                continue;
            }
            cell.SetSpecialType(t.type);
        }

        Debug.Log($"[StageTileApplier] 应用阶段 {stage}，特殊格 {layout.tiles.Count} 个");
    }
}