/****************************************************
    文件：TileAffinityConfig.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/11 19:30:34
	功能：地块适配配置，定义哪些怪物类型适配哪些特殊地块
*****************************************************/
using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "TileAffinityConfig", menuName = "Backpack/TileAffinityConfig")]
public class TileAffinityConfig : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public SpecialTileType tileType;
        public List<MonsterType> adaptedTypes = new();
    }

    [SerializeField] private List<Entry> _entries = new();

    private Dictionary<SpecialTileType, HashSet<MonsterType>> _map;

    public bool IsAdapted(SpecialTileType tile, MonsterType monster)
    {
        EnsureMap();
        return _map.TryGetValue(tile, out var set) && set.Contains(monster);
    }

    public int GetAdaptedIncome(SpecialTileType tile) => 6;   // 固定收益，可扩展

    private void EnsureMap()
    {
        if (_map != null) return;
        _map = new Dictionary<SpecialTileType, HashSet<MonsterType>>();
        foreach (var e in _entries)
            _map[e.tileType] = new HashSet<MonsterType>(e.adaptedTypes);
    }

    private void OnValidate() { _map = null; }
}