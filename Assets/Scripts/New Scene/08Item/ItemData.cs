/****************************************************
    文件：ItemData.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/4 14:59:48
	功能：物品类
*****************************************************/

using System;
using UnityEngine;

[Serializable]
public class ItemData
{
    public int id;
    public string itemName;
    public string description;
    public Sprite sprite;

    public bool IsValid => !string.IsNullOrEmpty(itemName);
}

