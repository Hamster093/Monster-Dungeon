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
    public int monsterId;     // items.json 的 id（101~117）

    public bool isEvolved;                         // 是否已进化
    public EvolutionDirection evolutionDirection;  // 进化方向

    public MonsterInstance() { }

    public MonsterInstance(int monsterId)
    {
        this.uid = Guid.NewGuid().ToString("N");
        this.monsterId = monsterId;
        this.isEvolved = false;
        this.evolutionDirection = EvolutionDirection.None;
    }

    /// <summary>尝试进化。已进化返回 false。</summary>
    public bool Evolve(EvolutionDirection dir)
    {
        if (isEvolved) return false;

        isEvolved = true;
        evolutionDirection = dir;

        // TODO: 在这里发提升，比如最大生命值、攻击力等
        // 如果是挂载组件的话，可以把魔物实例包裹一层，或者在这里加上属性字典
        // Debug.Log($"[MonsterInstance] {uid} 进化方向：{dir}，数值提升中...");

        return true;
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
    public MonsterInstance Add(int monsterId)
    {
        var inst = new MonsterInstance(monsterId);
        _items.Add(inst);
        OnChanged?.Invoke();
        SaveSystem.Save(this);
        return inst;
    }
    //魔物回收方法
    public void Reclaim(MonsterInstance inst)
    {
        if (inst == null) return;

        // 防止重复添加
        if (_items.Exists(m => m.uid == inst.uid)) return;

        _items.Add(inst);
        SaveSystem.Save(this);
        OnChanged?.Invoke();
    }

    /// <summary>按 uid 精确移除</summary>
    public bool RemoveByUid(string uid)
    {
        int idx = _items.FindIndex(m => m.uid == uid);
        if (idx < 0) return false;

        _items.RemoveAt(idx);
        OnChanged?.Invoke();
        SaveSystem.Save(this);
        return true;
    }

    public void Clear()
    {
        _items.Clear();
        OnChanged?.Invoke();
    }
}