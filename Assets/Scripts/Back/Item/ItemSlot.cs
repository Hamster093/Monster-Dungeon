/****************************************************
    文件：ItemSlot.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026-09-21 15:52:25
	功能：Nothing
*****************************************************/

using System;
using System.Xml;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ItemSlot : MonoBehaviour ,IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    //=====物品属性=====//
    public string _itemName;
    public int _quantity;
    public Sprite _itemSprite;
    public bool _isFull;
    public string _itemDescription;
    public Sprite _emptySprite;

    [SerializeField] private int _maxNumberOfItems;

    //=====物品格子=====//
    [SerializeField]
    private Text _quantityText;

    [SerializeField]
    private Image _itemImage;


    //=====物品格子声明=====//
    public Image _itemDescriptionImage;
    public Text _itemDescriptionNameText;
    public Text _itemDescriptionText;

    //名字面板
    [SerializeField]
    private NamePanel _namePanle;

    public GameObject _selectedShader; 
    public bool _thisItemSelected;

    private InventoryManager _inventoryManager;

    public GameObject _itemPrefab;

    private void Start()
    {
        _inventoryManager=GameObject.Find("InventoryCanvas").GetComponent<InventoryManager>();//此处子物体拿到父物体引用 后续应改为事件 todo
    }

    public int AddItem(string itemName, int quantity, Sprite itemSprite,string itemDescription)
    {
        if (_isFull)
            return quantity;

        //更新名称
        _itemName =itemName;
        //更新图片
        _itemSprite=itemSprite;
        _itemImage.sprite = itemSprite;
        //更新描述
        _itemDescription=itemDescription;
        //更新数量
        _quantity += quantity;
        if (_quantity >= _maxNumberOfItems)
        {
            _quantityText.text = _maxNumberOfItems.ToString();
            _quantityText.enabled = true;
            _isFull = true;

            int extraItem = _quantity - _maxNumberOfItems;
            _quantity = _maxNumberOfItems;
            return extraItem;
        }

        _quantityText.text = _quantity.ToString();
        _quantityText.enabled = true;

        return 0;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            OnLeftClick();
        }
        else if (eventData.button == PointerEventData.InputButton.Right)
        {
            OnRightClick();
        }
    }

    private void OnLeftClick()
    {
        //选中状态再次点击 触发使用逻辑  todo如果可使用 项目可能不需要消耗品 先写在这
        if (_thisItemSelected)
        {
            _inventoryManager.UseItem(_itemName);
            _quantity--;
            _quantityText.text=_quantity.ToString();
            if (_quantity<=0)
            {
                EmptySlot();
            }
        }
        else
        {
            _inventoryManager.DeselectAllSlots();
            _selectedShader.SetActive(true);
            _thisItemSelected = true;

            _itemDescriptionNameText.text = _itemName;
            _itemDescriptionText.text = _itemDescription;
            _itemDescriptionImage.sprite = _itemSprite;
            if (_itemDescriptionImage.sprite == null)
                _itemDescriptionImage.sprite = _emptySprite;
        }
        //todo 鼠标左键单击查看 弹出查看窗口  点击该窗口外的图片关闭窗口
    }

    /// <summary>
    /// 消耗物品方法
    /// </summary>
    /// <exception cref="NotImplementedException"></exception>
    private void EmptySlot()
    {
        //背包面板清空
        _quantityText.enabled=false;
        _itemImage.sprite = _emptySprite;
        //描述栏位面板清空
        _itemDescriptionNameText.text = "";
        _itemDescriptionText.text = "";
        _itemDescriptionImage.sprite = _emptySprite;
    }

    private void OnRightClick()
    {
        //todo 鼠标右键弹出菜单 菜单包含查看 丢弃按钮
        //test测试丢弃逻辑
        DiscardItem();
    }

    /// <summary>
    /// 丢弃逻辑
    /// </summary>
    private void DiscardItem()
    {
        GameObject droppedItem = Instantiate(_itemPrefab, new Vector3 (0,0,0), Quaternion.identity);//todo 暂时生成在000

        Item itemComp = droppedItem.GetComponent<Item>();
        if (itemComp != null)
        {
            itemComp._quantity = 1;
            itemComp._itemName = _itemName;
            itemComp._sprite = _itemSprite;
            itemComp._itemDescription = _itemDescription;
        }

        // 3. 更新子物体Square的精灵图
        SpriteRenderer sr = droppedItem.transform.Find("Square")?.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.sprite = _itemSprite;
        }

        if (_isFull)
        {
            _isFull = false;
        }
        //判断物品是否为空
        _quantity--;

        _quantityText.text = _quantity.ToString();
        if (_quantity <= 0)
        {
            EmptySlot();
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_quantity <= 0 || string.IsNullOrEmpty(_itemName))
            return;
        _namePanle.gameObject.SetActive(true);
        _namePanle.Show(_itemName);

    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _namePanle.Hide();
    }

}