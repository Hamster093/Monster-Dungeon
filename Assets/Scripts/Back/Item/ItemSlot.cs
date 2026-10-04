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

// 右键菜单数据：物品 + 回调
public class ItemActionData
{
    public ItemData data;
    public Action onDrop;
}

public class ItemSlot : MonoBehaviour ,IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    //=====物品格子=====//
    [SerializeField] private Text _quantityText;
    [SerializeField] private Image _itemImage;
    [SerializeField] public GameObject _selectedShader;

    [SerializeField] private Text _subText;//可选，商店格子显示价格

    private Func<int, ItemStack> _dataProvider;

    //=====商店复用代码添加的逻辑，商店点击格子时会走 OnLeftClickedOverride 逻辑======
    /// <summary>面板可注册，注册后左键点击由面板接管，默认逻辑不再执行</summary>
    public System.Action<int> OnLeftClickedOverride;
    /// <summary>面板可注册，注册后右键点击由面板接管</summary>
    public System.Action<int> OnRightClickedOverride;

    public bool _thisItemSelected;

    public int SlotIndex { get; private set; }
    private ItemStack _stack;

    /// <summary>
    /// 由 BackpackPanel 在 OnInit 时绑定索引,商店传入自己的provider
    /// </summary>
    public void Bind(int index, Sprite emptySprite, Func<int, ItemStack> provider = null)
    {
        SlotIndex = index;
        _dataProvider = provider ?? (i => InventoryModel.Instance.GetSlot(i));
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
        if (OnLeftClickedOverride != null)
        {
            OnLeftClickedOverride.Invoke(SlotIndex);
            return;
        }

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
        if (OnRightClickedOverride != null)
        {
            OnRightClickedOverride.Invoke(SlotIndex);
            return;
        }

        if (_stack == null || _stack.IsEmpty) return;

        UIManager.Instance.Open<ItemActionPanel>(new ItemActionData
        {
            data = _stack.data,
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

    public void SetSubText(string text)
    {
        if (_subText == null) return;
        _subText.gameObject.SetActive(!string.IsNullOrEmpty(text));
        _subText.text = text;
    }
}