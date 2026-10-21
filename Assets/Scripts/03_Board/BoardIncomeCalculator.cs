/****************************************************
    文件：BoardIncomeCalculator.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/11 19:32:26
	功能：收益系统计算器，计算背包板上所有魔物的总收益
*****************************************************/

using System.Collections.Generic;
using UnityEngine;

public static class BoardIncomeCalculator
{
    // 相邻魔物收益
    private const int IncomeSameType = 1;
    private const int IncomeDiffType = 3;
    // 地块收益固定 6（放在 TileAffinityConfig 里）

    public static int ComputeTotal(BackpackBoard board,TileAffinityConfig tileConfig,MonsterIncomeConfig incomeConfig)
    {
        board.CleanupDestroyedItems();
        int total = 0;
        foreach (var view in board.Items)
            total += ComputeSingle(view, board, tileConfig, incomeConfig);
        return total;
    }

    private static int ComputeSingle(ItemView view, BackpackBoard board,
                                     TileAffinityConfig tileConfig,
                                     MonsterIncomeConfig incomeConfig)
    {
        if (view == null || view.Item == null || view.Item.Def == null) return 0;
        if (!int.TryParse(view.Item.Def.Id, out int monsterId)) return 0;

        // ① 基础收益
        var entry = incomeConfig.Get(monsterId);
        if (entry == null) return 0;

        MonsterType selfType = GetMonsterType(monsterId);
        int income = view.Item.Evolved ? entry.evolvedIncome : entry.baseIncome;   // 进化后换成 evolvedIncome

        // ② 相邻魔物收益（可反复计算：每个邻居都独立加一次）
        foreach (var neighbor in GetAdjacentMonsters(view.Item, board))
        {
            if (!int.TryParse(neighbor.Def.Id, out int nid)) continue;
            MonsterType nType = GetMonsterType(nid);
            income += (nType == selfType) ? IncomeSameType : IncomeDiffType;
        }

        // ③ 同类特殊地块按四向连通组计费；多格接触同一组不重复，不相连的两组各算一次。
        var seenTiles = new HashSet<string>();
        foreach (var tile in GetAdjacentSpecialTiles(view.Item, board))
        {
            if (!seenTiles.Add(tile.key)) continue;          // 该连通组已计费
            if (tileConfig.IsAdapted(tile.type, selfType))
                income += tileConfig.GetAdaptedIncome(tile.type);
        }

        return income;
    }

    // ---------- 相邻查询 ----------

    /// <summary>获取与该物品相邻的所有魔物（去重）</summary>
    private static HashSet<ItemView> GetAdjacentMonsters(PlacedItem item, BackpackBoard board)
    {
        var result = new HashSet<ItemView>();
        foreach (var cell in GetAdjacentCells(item))
        {
            var occupant = board.GetCell(cell)?.Occupant;
            if (occupant == null) continue;
            if (occupant == item) continue;              // 自己不算
            if (occupant.View == null) continue;
            result.Add(occupant.View);
        }
        return result;
    }

    /// <summary>获取与该物品相邻的所有特殊地块类型</summary>
    private static IEnumerable<(SpecialTileType type,string key)> GetAdjacentSpecialTiles(PlacedItem item, BackpackBoard board)
    {
        foreach (var cell in GetAdjacentCells(item))
        {
            var c = board.GetCell(cell);
            if (c != null && c.SpecialType != SpecialTileType.None)
                yield return (c.SpecialType,TileGroupKey(cell,c.SpecialType,board));
        }
    }

    /// <summary>物品所有占用格 4 邻域中、不属于该物品自身的格子</summary>
    private static IEnumerable<Vector2Int> GetAdjacentCells(PlacedItem item)
    {
        var occupied = new HashSet<Vector2Int>(item.GetCells());
        var yielded = new HashSet<Vector2Int>();
        foreach (var c in occupied)
        {
            foreach (var d in Directions4)
            {
                var n = c + d;
                if (occupied.Contains(n)) continue;
                if (yielded.Add(n))
                    yield return n;
            }
        }
    }

    private static string TileGroupKey(Vector2Int start,SpecialTileType type,BackpackBoard board)
    {
        var seen=new HashSet<Vector2Int>{start};var queue=new Queue<Vector2Int>();queue.Enqueue(start);var minimum=start;
        while(queue.Count>0){var p=queue.Dequeue();if(p.y<minimum.y||p.y==minimum.y&&p.x<minimum.x)minimum=p;foreach(var d in Directions4){var n=p+d;if(board.GetCell(n)?.SpecialType==type&&seen.Add(n))queue.Enqueue(n);}}
        return type+":"+minimum.x+":"+minimum.y;
    }
    private static readonly Vector2Int[] Directions4 =
    {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
    };

    private static MonsterType GetMonsterType(int monsterId)
    {
        return ConfigDatabase.Instance.Monsters.TryGetValue(monsterId, out var cfg)
            ? cfg.monsterType
            : MonsterType.Slime;   // 兜底
    }
}
