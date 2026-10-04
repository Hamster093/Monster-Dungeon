/****************************************************
    文件：ItemStack.cs
    功能：单个背包格子的数据
*****************************************************/

using System;
using UnityEngine;

// 背包格子：物品 + 数量
[Serializable]
public class ItemStack
{
    public ItemData data;
    public int quantity;

    public bool IsEmpty => data == null;

    // 便捷转发 .data.sprite
    public string itemName => data?.itemName;
    public Sprite sprite => data?.sprite;
    public string description => data?.description;

    public void Clear()
    {
        data = null;
        quantity = 0;
    }
}