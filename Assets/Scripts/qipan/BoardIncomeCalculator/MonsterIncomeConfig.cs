/****************************************************
    文件：MonsterIncomeConfig.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/11 19:31:18
	功能：魔物收益配置，定义不同怪物类型在不同地块上的收益
*****************************************************/

using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "MonsterIncomeConfig", menuName = "Backpack/MonsterIncomeConfig")]
public class MonsterIncomeConfig : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public int monsterId;         // 对应 items.json 的 id（101~117）
        public string displayName;    // 只为方便看，不参与逻辑
        public int baseIncome;        // 未进化收益/天
        public int evolvedIncome;     // 进化后收益/天
    }

    [SerializeField] private List<Entry> _entries = new();

    private Dictionary<int, Entry> _map;
    public Entry Get(int monsterId)
    {
        if (_map == null)
        {
            _map = new Dictionary<int, Entry>();
            foreach (var e in _entries) _map[e.monsterId] = e;
        }
        return _map.TryGetValue(monsterId, out var v) ? v : null;
    }
    private void OnValidate() { _map = null; }
}
