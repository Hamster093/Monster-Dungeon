/****************************************************
    文件：PartyModel.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/7 20:26:51
	功能：7 个魔物槽位的全局数据（跨面板共享）
*****************************************************/

using System;
using UnityEngine;

public class PartyModel
{
    public const int SlotCount = 7;

    public static PartyModel Instance { get; } = new PartyModel();

    private readonly int[] _monsterIds = new int[SlotCount];

    /// <summary>数据变化时触发，PreparationPanel 订阅刷新显示</summary>
    public event Action OnChanged;

    public int Get(int index)
        => (index >= 0 && index < SlotCount) ? _monsterIds[index] : 0;

    /// <summary>找第一个空格放入指定魔物；返回是否成功</summary>
    public bool TryAddMonster(int monsterId)
    {
        if (monsterId <= 0) return false;

        for (int i = 0; i < SlotCount; i++)
        {
            if (_monsterIds[i] <= 0)
            {
                _monsterIds[i] = monsterId;
                OnChanged?.Invoke();
                return true;
            }
        }
        return false;   // 满了
    }

    public void Swap(int a, int b)
    {
        if (a == b) return;
        if (a < 0 || a >= SlotCount || b < 0 || b >= SlotCount) return;

        (_monsterIds[a], _monsterIds[b]) = (_monsterIds[b], _monsterIds[a]);
        OnChanged?.Invoke();
    }

    public void Set(int index, int monsterId)
    {
        if (index < 0 || index >= SlotCount) return;
        _monsterIds[index] = monsterId;
        OnChanged?.Invoke();
    }

    public void ClearAll()
    {
        for (int i = 0; i < SlotCount; i++) _monsterIds[i] = 0;
        OnChanged?.Invoke();
    }
}
