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
    public string shapeId;
    public ItemShapeDefinition shape;
    // 只有具体实例才有当天状态，商店里的共享商品不伪造精神状态。
    public bool hasMonsterInstance, monsterResting;

    public bool IsValid => !string.IsNullOrEmpty(itemName);


    public static ItemData FromConfig(ItemConfig cfg)
    {
        var data = new ItemData
        {
            id = cfg.id,
            itemName = cfg.name,
            description = cfg.description,
            iconAddress = cfg.iconAddress,
            shapeId = cfg.shapeId,
        };
        // 从 ConfigDatabase 里拿 ShapeDatabase（见下）
        if (ConfigDatabase.Instance != null && ConfigDatabase.Instance.ShapeDb != null)
            data.shape = ConfigDatabase.Instance.ShapeDb.Get(cfg.shapeId);
        if(cfg.type==ItemType.Monster&&ConfigDatabase.Instance.Monsters.TryGetValue(cfg.id,out var monster))
            data.description+="\n\n等级："+monster.level+"\n种类："+monster.monsterType;
        return data;
    }
    public static ItemData FromMonster(PlacedItem item)
    {
        var data=FromConfig(ConfigDatabase.Instance.Items[int.Parse(item.Def.Id)]);
        data.hasMonsterInstance=true;data.monsterResting=item.IsResting;
        data.itemName=EvolutionPanel.MonsterName(data.itemName,item.EvolutionKind);
        var monster=ConfigDatabase.Instance.Monsters[data.id];
        string[] types={"史莱姆","哥布林","犬类","植物类","构造类","龙类"};
        data.description="等级："+monster.level+"　种类："+types[(int)monster.monsterType]+"\n魔物状态："+(item.IsResting?"疲惫":"精神充沛");
        if(item.Evolved)data.description+="\n"+EvolutionPanel.Effect(item.EvolutionKind);
        data.description+="\n"+ConfigDatabase.Instance.Items[data.id].description;
        return data;
    }
    public static ItemData FromMonster(MonsterInstance instance)
    {
        var item=new PlacedItem(ConfigDatabase.Instance.ShapeDb.Get(instance.monsterId.ToString())){Uid=instance.uid,Evolved=instance.evolved,EvolutionKind=instance.evolutionKind,UsedDay=instance.usedDay};
        return FromMonster(item);
    }
}



