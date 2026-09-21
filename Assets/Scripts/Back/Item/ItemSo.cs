/****************************************************
    文件：ItemSo.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026-09-21 19:06:18
	功能：消耗品配置
*****************************************************/

using UnityEngine;

[CreateAssetMenu(fileName = "ItemSo", menuName = "Inventory/Item", order = 0)]
public class ItemSo :ScriptableObject
{
    public string itemName;
    public StatToChange statToChange = new StatToChange();
    public int amountToChangeStat;

    public AttributesToChange attributes = new AttributesToChange();
    public int amountToChangeAttrinute;

    public void UseItem()
    {
        //可选 增加bool值判断是否可使用 
        if (statToChange==StatToChange.HP)
        {
            //查找玩家数据管理器 修改属性 todo 或发布生命值变化委托 Action<currHP,MaxHP>....
        }
    }


    /// <summary>
    /// 物品使用影响的属性值
    /// </summary>
    public enum StatToChange
    {
        NONE,
        HP,
        MP
    };
    /// <summary>
    /// 待修改的属性
    /// </summary>
    public enum AttributesToChange
    {
        None,
        Attack,

    };
}