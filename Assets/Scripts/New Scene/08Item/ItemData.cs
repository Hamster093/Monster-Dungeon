/****************************************************
    文件：ItemData.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/4 14:59:48
	功能：物品类和 ItemData 工厂
*****************************************************/

using System;
using UnityEngine;

[Serializable]
public class ItemData
{
    public int id;
    public string itemName;
    public string description;
    public string iconAddress;   // Addressables 地址，运行时按需加载

    public bool IsValid => !string.IsNullOrEmpty(itemName);


    public static ItemData FromConfig(ItemConfig cfg)
    {
        return new ItemData
        {
            id = cfg.id,
            itemName = cfg.name,
            description = cfg.description,
            iconAddress = cfg.iconAddress,

        };
    }
}



