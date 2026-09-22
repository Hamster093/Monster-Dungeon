/****************************************************
    文件：ItemSlot.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026-09-21 15:52:25
	功能：Nothing
*****************************************************/

using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ItemDescriptionData
{
    public string itemName;
    public string itemDescription;
    public Sprite itemSprite;
    public Sprite emptySprite;   // 没有图片时的兜底
}

public class ItemActionData
{
    public string itemName;
    public Sprite itemSprite;
    public string itemDescription;
    public Sprite emptySprite;

    /// <summary>点击“丢弃”时由 ItemSlot 执行</summary>
    public Action onDrop;
}

public class ItemSlot : MonoBehaviour ,IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    //=====物品格子=====//
    [SerializeField] private Text _quantityText;
    [SerializeField] private Image _itemImage;
    [SerializeField] public GameObject _selectedShader; 

    public bool _thisItemSelected;

    public int SlotIndex { get; private set; }
    private ItemStack _stack;

    /// <summary>由 BackpackPanel 在 OnInit 时绑定索引</summary>
    public void Bind(int index, Sprite emptySprite)
    {
        SlotIndex = index;
        Refresh(InventoryModel.Instance.GetSlot(index), emptySprite);
    }

    /// <summary>刷新单个格子的显示</summary>
    public void Refresh(ItemStack stack, Sprite emptySprite)
    {
        _stack = stack;

        if (stack == null || stack.IsEmpty)
        {
            _itemImage.sprite = emptySprite;
            _quantityText.enabled = false;
        }
        else
        {
            _itemImage.sprite = stack.sprite != null ? stack.sprite : emptySprite;
            _quantityText.enabled = stack.quantity > 1;
            _quantityText.text = stack.quantity.ToString();
        }
    }

    public void SetSelected(bool selected)
    {
        _thisItemSelected = selected;
        _selectedShader.SetActive(selected);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
            OnLeftClick();
        else if (eventData.button == PointerEventData.InputButton.Right)
            OnRightClick();
    }

    private void OnLeftClick()
    {
        DeselectAllRequested.Trigger();
        SetSelected(true);

        if (_stack == null || _stack.IsEmpty) return;

        if (_thisItemSelected)
        {
            // 使用逻辑
            // InventoryModel.Instance.UseItem(SlotIndex);
            
        }
    }
    private void OnRightClick()
    {
        if (_stack == null || _stack.IsEmpty) return;

        UIManager.Instance.Open<ItemActionPanel>(new ItemActionData
        {
            itemName = _stack.itemName,
            itemSprite = _stack.sprite,
            itemDescription = _stack.description,
            onDrop = () => DropItem()
        });
    }

    private void DropItem()
    {
        if (_stack == null || _stack.IsEmpty) return;

        // 生成掉落物？TODO

        //丢弃一个
        InventoryModel.Instance.RemoveItem(SlotIndex, 1);
        
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_stack == null || _stack.IsEmpty) return;
        UIManager.Instance.Open<NamePanel>(_stack.itemName);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        UIManager.Instance.Close<NamePanel>();
    }
}