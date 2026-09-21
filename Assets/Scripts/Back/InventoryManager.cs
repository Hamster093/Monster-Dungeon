/****************************************************
    文件：InventoryManager.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026-09-21 15:43:26
	功能：背包库存管理器 摁下打开菜单 暂停游戏
*****************************************************/

using System;
using Unity.VisualScripting;
using UnityEngine;

public class InventoryManager : MonoBehaviour 
{
    public GameObject _InventoryMenu;

    private bool _menuActivated;
    public ItemSlot[] _itemSlot;

    public ItemSo[] _itemSos;

    private void Awake()
    {
        OpenBackPack.Register(ToggleBackpack);
    }

    private void OnDestroy()
    {
        OpenBackPack.UnRegister(ToggleBackpack);
    }
    /// <summary>
    /// 点击B键开关背包 同时暂停游戏
    /// </summary>
    private void Update()
    {
        if (Input.GetButtonDown("Inventory")&& _menuActivated)
        {
            Time.timeScale = 1;
            _InventoryMenu.SetActive(false);
            _menuActivated = false;
        }
        else if (Input.GetButtonDown("Inventory")&& !_menuActivated)
        {
            Time.timeScale = 0;
            _InventoryMenu.SetActive(true);
            _menuActivated = true;
        }
    }

    public void UseItem(string itemName)
    {
        for (int i = 0; i < _itemSos.Length; i++)
        {
            if (_itemSos[i].itemName==itemName)
            {
                _itemSos[i].UseItem();
            }
        }
    }

    /// <summary>
    /// 添加物品
    /// </summary>
    /// <param name="itemName">物品名称</param>
    /// <param name="quantity">物品数量</param>
    /// <param name="itemSprite">物品图片</param>
    public int AddItem(string itemName, int quantity, Sprite itemSprite,string itemDescription)
    {
        for (int i = 0; i < _itemSlot.Length; i++)
        {
            if (_itemSlot[i]._isFull == false && _itemSlot[i]._itemName == itemName || _itemSlot[i]._quantity == 0)
            {
                int leftOverItems = _itemSlot[i].AddItem(itemName, quantity, itemSprite, itemDescription);
                if (leftOverItems > 0)
                    leftOverItems = AddItem(itemName, leftOverItems, itemSprite, itemDescription);
                return leftOverItems;
            }
        }
        return quantity;

    }

    //public int TestAddItem()
    //{
    //    string itemName = "测试物体";
    //    int quantity = 1;
    //    Sprite itemSprite=null;
    //    string itemDescription = "测试文本";
    //    for (int i = 0; i < _itemSlot.Length; i++)
    //    {
    //        if (_itemSlot[i]._isFull == false && _itemSlot[i].name== name|| _itemSlot[i]._quantity == 0)
    //        {
    //            int leftOverItems= _itemSlot[i].AddItem(itemName, quantity, itemSprite, itemDescription);
    //            if (leftOverItems > 0)
    //                leftOverItems = TestAddItem();
    //            return leftOverItems;
    //        }
    //    }
    //    return quantity;
    //}

    public void DeselectAllSlots()
    {
        for (int i = 0; i < _itemSlot.Length; i++)
        {
            if (_itemSlot[i] != null)
            {
                _itemSlot[i]._selectedShader.SetActive(false);
                _itemSlot[i]._thisItemSelected = false;
            }
        }
    }

    public void ToggleBackpack()
    {
        if (_menuActivated)
        {
            Time.timeScale = 1;
            _InventoryMenu.SetActive(false);
            _menuActivated = false;
        }
        else
        {
            Time.timeScale = 0;
            _InventoryMenu.SetActive(true);
            _menuActivated = true;
        }
    }
}