/****************************************************
    文件：ItemStack.cs
    功能：单个背包格子的数据
*****************************************************/

using UnityEngine;

[System.Serializable]
public class ItemStack
{
    public string itemName;
    public int quantity;
    public Sprite sprite;
    public string description;

    public bool IsEmpty => quantity <= 0 || string.IsNullOrEmpty(itemName);

    public void Clear()
    {
        itemName = null;
        quantity = 0;
        sprite = null;
        description = null;
    }
}