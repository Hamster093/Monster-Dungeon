/****************************************************
    文件：Item.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026-09-21 15:52:16
	功能：物品类
*****************************************************/

using System;
using UnityEngine;

/// <summary>拾取请求的数据包</summary>
public class ItemPickupData
{
    public string itemName;
    public int quantity;
    public Sprite sprite;
    public string description;

    /// <summary>处理结果回调：参数为剩余数量，0 表示全部放入</summary>
    public Action<int> onResult;
}

public class Item : MonoBehaviour//目前不确定物品是否需要实例
{
    [SerializeField]
    public string _itemName;

    [SerializeField]
    public int _quantity;

    [SerializeField]
    public int _maxNumber;

    [SerializeField]
    public Sprite _sprite;

    [TextArea]
    [SerializeField]
    public string _itemDescription;

    /// <summary>
    /// 玩家拾取物品
    /// </summary>
    /// <param name="collision"></param>
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
            Pickup();
    }

    /// <summary>
    /// 玩家获得物品
    /// </summary>
    public void PlayerAddItem()
    {
        Pickup();
    }

    /// <summary>
    /// 拾取
    /// </summary>
    private void Pickup()
    {
        ItemPickupRequested.Trigger(new ItemPickupData
        {
            itemName = _itemName,
            quantity = _quantity,
            sprite = _sprite,
            description = _itemDescription,
            onResult = leftover =>
            {
                if (leftover <= 0)
                //Destroy(gameObject);       // 全部放进去了，销毁
                    return;
                else
                    _quantity = leftover;       // 背包满了，剩下的留在场景里
            }
        });
    }
}