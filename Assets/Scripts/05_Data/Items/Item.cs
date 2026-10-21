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
    public ItemData data;   
    public int quantity;
    public Action<int> onResult;
}

public class Item : MonoBehaviour//目前不确定物品是否需要实例
{
    [SerializeField] private ItemData _data;
    [SerializeField] private int _quantity = 1;
    [Header("拾取是否销毁物体")]
    [SerializeField] private bool DestroyOnPickupComplete = false;   // 测试时改成 false

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
        if (_data == null)
        {
            Debug.LogWarning($"[Item] {name} 没有配置 ItemData");
            return;
        }

        ItemPickupRequested.Trigger(new ItemPickupData
        {
            data = _data,
            quantity = _quantity,
            onResult = leftover =>
            {
                if (leftover <= 0)
                {
                    if (DestroyOnPickupComplete) Destroy(gameObject);
                    return;
                }
                else
                {
                    _quantity = leftover;      
                }
            }
        });
    }
}