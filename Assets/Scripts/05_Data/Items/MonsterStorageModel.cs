/****************************************************
    文件：MonsterStorageModel.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/12 13:44:30
	功能：魔物存储模型类
*****************************************************/

using System;
using System.Collections.Generic;

[Serializable]
public class MonsterInstance
{
    public string uid;        // 唯一 id
    public bool evolved;
    public int usedDay = -1;
    public string evolutionKind = "";
    public int monsterId;     // items.json 的 id（101~117）

    public MonsterInstance() { }

    public MonsterInstance(int monsterId)
    {
        this.uid = Guid.NewGuid().ToString("N");
        this.monsterId = monsterId;
    }
}

public class MonsterStorageModel
{
    public static MonsterStorageModel Instance { get; } = new();

    private readonly List<MonsterInstance> _items = new();
    public event Action OnChanged;

    public IReadOnlyList<MonsterInstance> Items => _items;
    public int Count => _items.Count;

    /// <summary>添加一只新魔物（内部生成实例）</summary>
    public MonsterInstance Add(int monsterId, string uid = null, bool evolved = false, int usedDay = -1, string evolutionKind = "")
    {
        var inst = new MonsterInstance(monsterId) { evolved = evolved, usedDay = usedDay, evolutionKind = evolutionKind };
        if (!string.IsNullOrEmpty(uid)) inst.uid = uid;
        _items.Add(inst);
        OnChanged?.Invoke();
        return inst;
    }

    /// <summary>按 uid 精确移除</summary>
    public bool RemoveByUid(string uid)
    {
        int idx = _items.FindIndex(m => m.uid == uid);
        if (idx < 0) return false;

        _items.RemoveAt(idx);
        OnChanged?.Invoke();
        return true;
    }

    public void Clear()
    {
        _items.Clear();
        OnChanged?.Invoke();
    }
}