/****************************************************
    文件：MonsterSlot.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/7 19:59:26
	功能：准备面板的魔物槽位（显示 + 拖拽信号转发）
*****************************************************/

/****************************************************
    文件：MonsterSlot.cs
	作者：DADI
    日期：2026/10/7
	功能：准备面板的魔物槽位（显示 + 拖拽信号转发）
*****************************************************/

using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>准备面板一个槽位的运行时数据</summary>
[Serializable]
public class MonsterSlotData
{
    public int monsterId;   // 0 = 空

    public MonsterConfig Config =>
        monsterId > 0 && ConfigDatabase.Instance.Monsters.TryGetValue(monsterId, out var c)
            ? c : null;
}

public class MonsterSlot : MonoBehaviour,
    IPointerClickHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    //=====显示组件（和 ItemSlot 命名风格一致）=====//
    [SerializeField] private Image _frameImage;    // 空槽底图（Img_MonsterX 自己的 Image）
    [SerializeField] private Image _avatarImage;   // 魔物头像（Img_Cage 的 Image）
    [SerializeField] public GameObject _selectedShader;// 选中高亮（Img_Cage 的子物体，SetActive(true/false)）

    [Header("测试图标")]
    [SerializeField] private string _iconAddressOverride = "Icons/WWM";

    [SerializeField] private CanvasGroup _canvasGroup;

    //=====面板可注册的回调=====//
    public Action<int> OnLeftClickedOverride;
    public Action<int> OnRightClickedOverride;
    public Action<int, int> OnSwapRequested;   // (from, to)
    public Action<MonsterSlot> OnDragStarted;  // 通知面板创建 ghost
    public Action<MonsterSlot> OnDragEnded;

    public bool _thisSlotSelected;
    public int SlotIndex { get; private set; }

    private MonsterSlotData _data = new();
    public MonsterSlotData Data => _data;

    // 图标加载状态
    private string _iconAddress;
    private int _iconRequestId;

    // ---------- 绑定 / 刷新 ----------

    public void Bind(int index, Sprite emptySprite)
    {
        SlotIndex = index;

        if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        Refresh(new MonsterSlotData(), emptySprite);
    }

    public void Refresh(MonsterSlotData data, Sprite emptySprite)
    {
        _data = data ?? new MonsterSlotData();

        // 底框
        if (_frameImage != null && emptySprite != null)
            _frameImage.sprite = emptySprite;

        // 空槽
        if (_data.monsterId <= 0)
        {
            _iconAddress = null;
            _iconRequestId++;
            if (_avatarImage != null)
            {
                _avatarImage.sprite = null;
                _avatarImage.enabled = false;
            }
            return;
        }

        // 有魔物：按 id 取头像
        string addr = string.IsNullOrEmpty(_iconAddressOverride)
    ? $"Icons/Monsters/{_data.monsterId}"
    : _iconAddressOverride;
        if (addr != _iconAddress || _avatarImage.sprite == null)
        {
            _iconAddress = addr;
            LoadIconAsync(addr, ++_iconRequestId);
        }
    }

    private async void LoadIconAsync(string address, int requestId)
    {
        var sp = await SpriteLoader.LoadAsync(address);
        if (this == null || _avatarImage == null) return;
        if (requestId != _iconRequestId) return;

        _avatarImage.sprite = sp;
        _avatarImage.enabled = sp != null;
    }

    // ---------- 选中 ----------

    public void SetSelected(bool selected)
    {
        _thisSlotSelected = selected;
        if (_selectedShader != null) _selectedShader.SetActive(selected);
    }

    // ---------- 点击 ----------

    public void OnPointerClick(PointerEventData e)
    {
        if (e.button == PointerEventData.InputButton.Left)
        {
            if (OnLeftClickedOverride != null) { OnLeftClickedOverride.Invoke(SlotIndex); return; }
        }
        else if (e.button == PointerEventData.InputButton.Right)
        {
            if (OnRightClickedOverride != null) { OnRightClickedOverride.Invoke(SlotIndex); return; }
        }
    }

    // ---------- 拖拽 ----------

    public void OnBeginDrag(PointerEventData e)
    {
        if (_data.monsterId <= 0) return;    // 空格不能拖

        // 关键：拖拽期间关掉自身射线，让底下格子收到 OnDrop
        _canvasGroup.blocksRaycasts = false;
        OnDragStarted?.Invoke(this);
    }

    public void OnDrag(PointerEventData e)
    {
        // ghost 由 Panel 负责跟随鼠标，这里不做
    }

    public void OnEndDrag(PointerEventData e)
    {
        _canvasGroup.blocksRaycasts = true;
        OnDragEnded?.Invoke(this);
    }

    public void OnDrop(PointerEventData e)
    {
        var src = e.pointerDrag != null ? e.pointerDrag.GetComponent<MonsterSlot>() : null;
        if (src == null || src == this) return;

        OnSwapRequested?.Invoke(src.SlotIndex, SlotIndex);
    }

}