/****************************************************
    文件：InventoryModel.cs
    功能：背包数据模型，与 UI 解耦
*****************************************************/

using System;
using UnityEngine;

public class InventoryModel
{
    public static InventoryModel Instance { get; private set; }

    private readonly ItemStack[] _slots;
    private readonly int _maxStack;

    public int Capacity => _slots.Length;

    /// <summary>由游戏启动时调用一次</summary>
    public static void Initialize(int capacity = 20, int maxStack = 99)
    {
        if (Instance != null) return;

        Instance = new InventoryModel(capacity, maxStack);
        ItemPickupRequested.Register(Instance.OnPickupRequested);
    }

    private InventoryModel(int capacity, int maxStack)
    {
        _slots = new ItemStack[capacity];
        for (int i = 0; i < capacity; i++)
            _slots[i] = new ItemStack();
        _maxStack = maxStack;
    }

    public ItemStack GetSlot(int index)
    {
        if (index < 0 || index >= _slots.Length) return null;
        return _slots[index];
    }
    public int CountId(int id)
    {
        int count=0;foreach(var slot in _slots)if(!slot.IsEmpty&&slot.data.id==id)count+=slot.quantity;
        var run=RunStateManager.Instance.run;if(run!=null&&run.itemInventory.TryGetValue(id,out int n))count+=n;return count;
    }
    /// <summary>主动使用与检定被动消耗共用，成功提交时只扣一件。</summary>
    public bool ConsumeId(int id)
    {
        for(int i=0;i<_slots.Length;i++)if(!_slots[i].IsEmpty&&_slots[i].data.id==id)return RemoveItem(i);
        var run=RunStateManager.Instance.run;if(run!=null&&run.itemInventory.TryGetValue(id,out int n)&&n>0){run.itemInventory[id]=n-1;return true;}return false;
    }

    /// <summary>添加物品，返回放不下的剩余数量</summary>
    public int AddItem(ItemStack itemStack)
    {

        if (itemStack == null || itemStack.data == null) return 0;

        ItemData data = itemStack.data;
        int quantity = itemStack.quantity;
        int originalQuantity = quantity;

        // 第一遍：堆叠到已有的同名物品上
        for (int i = 0; i < _slots.Length && quantity > 0; i++)
        {
            var slot = _slots[i];
            if (slot.IsEmpty) continue;
            if (slot.data.id != data.id) continue;   // 用 id 比较，比 name 稳
            if (slot.quantity >= _maxStack) continue;

            int space = _maxStack - slot.quantity;
            int toAdd = Mathf.Min(space, quantity);
            slot.quantity += toAdd;
            quantity -= toAdd;
        }

        // 第二遍：填入空槽
        for (int i = 0; i < _slots.Length && quantity > 0; i++)
        {
            var slot = _slots[i];
            if (!slot.IsEmpty) continue;

            slot.data = data;                          // 直接引同一个 ItemData
            slot.quantity = Mathf.Min(_maxStack, quantity);
            quantity -= slot.quantity;
        }

        int added = originalQuantity - quantity;
        BackpackChanged.Trigger();
        return quantity;
    }

    /// <summary>从指定槽位移除一定数量</summary>
    public bool RemoveItem(int slotIndex, int count = 1)
    {
        var slot = GetSlot(slotIndex);
        if (slot == null || slot.IsEmpty) return false;

        slot.quantity -= count;
        if (slot.quantity <= 0) slot.Clear();

        BackpackChanged.Trigger();
        return true;
    }

    /// <summary>使用指定槽位的物品（按需扩展）</summary>
    public void UseItem(int slotIndex)
    {
        var slot = GetSlot(slotIndex);
        if (slot == null || slot.IsEmpty) return;

        // TODO: 根据 itemName 查找 ItemSo 并调用 UseItem
    }

    private void OnPickupRequested(ItemPickupData data)
    {
        if(data == null || data.data == null) return;

        int leftover = AddItem(new ItemStack
        {
            data = data.data,
            quantity = data.quantity
        });

        data.onResult?.Invoke(leftover);
    }
}
