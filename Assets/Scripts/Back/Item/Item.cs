/****************************************************
    文件：Item.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026-09-21 15:52:16
	功能：物品类
*****************************************************/

using UnityEngine;

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


    private InventoryManager _inventoryManager;

    private void Start()
    {
        _inventoryManager = GameObject.Find("InventoryCanvas").GetComponent<InventoryManager>();

    }

    /// <summary>
    /// 玩家拾取物品
    /// </summary>
    /// <param name="collision"></param>
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.tag=="Player")
        {
            int leftoverItems= _inventoryManager.AddItem(_itemName, _quantity, _sprite,_itemDescription);
            if (leftoverItems<=0)
                Destroy(gameObject);
            else
                _quantity=leftoverItems;
            
        }
    }
    /// <summary>
    /// 玩家获得物品
    /// </summary>
    public void PlayerAddItem()
    {
        int leftoverItems = _inventoryManager.AddItem(_itemName, _quantity, _sprite, _itemDescription);
        //if (leftoverItems <= 0)
        //    Destroy(gameObject);//如果物品有实例销毁 如果是商店 应该减去相应数量todo
        //else
        //    _quantity = leftoverItems;//背包装不下那么多的情况
    }
}