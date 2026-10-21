using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

/// <summary>首页和游戏内共用的四槽界面；只负责选择，序列化仍交给原存档管理器。</summary>
public class SaveSlotsPanel : PanelBase
{
    [SerializeField] private Button[] slots;
    [SerializeField] private Text[] summaries;
    [SerializeField] private Text[] phaseNumbers;
    [SerializeField] private Text[] dayNumbers;
    [SerializeField] private Text title;
    [SerializeField] private Button back;
    [SerializeField] private GameObject _overwriteRoot;
    [SerializeField] private Text _overwriteText;
    [SerializeField] private Button _overwriteConfirm, _overwriteCancel;
    private bool saving;
    private int pendingOverwrite;
    private static SaveSlotsPanel current;
    private SettingsPanel settingsSource;

    public static void Show(bool saveMode)
    {
        if (current == null)
        {
            var prefab = Resources.Load<SaveSlotsPanel>("UI/SaveSlotsPanel");
            var canvas = UIManager.Instance != null ? UIManager.Instance.RootCanvas : FindObjectOfType<Canvas>();
            if (prefab == null || canvas == null) { Debug.LogError("存档面板预制体或画布缺失。"); return; }
            current = Instantiate(prefab, canvas.transform, false);
        }
        current.saving = saveMode;
        current.pendingOverwrite = 0;
        // 暂时隐藏来源设置页，保留实例和滑块值；只让最上层存档页显示返回入口。
        if (!current.IsOpen)
        {
            current.settingsSource = Array.Find(FindObjectsOfType<SettingsPanel>(true),
                panel => panel.IsOpen && panel.gameObject.activeInHierarchy);
            if (current.settingsSource) current.settingsSource.gameObject.SetActive(false);
        }
        current.OnOpen();
        current.transform.SetAsLastSibling();
    }

    public override bool IsModal => true;
    public static bool IsShowing => current != null && current.IsOpen;
    public override void OnInit()
    {
        for (int i = 0; i < slots.Length; i++)
        { int id = i + 1; slots[i].onClick.AddListener(() => Select(id)); }
        back.onClick.AddListener(() => OnEscapePressed());
        _overwriteConfirm.onClick.AddListener(ConfirmOverwrite);
        _overwriteCancel.onClick.AddListener(CancelOverwrite);
    }
    public override void OnOpen(object data = null)
    { base.OnOpen(data); pendingOverwrite=0;_overwriteRoot.SetActive(false);RefreshSlots(); }
    public override void OnClose()
    {
        UIManager.Instance?.ConsumeModalCloseInput();
        pendingOverwrite = 0;
        _overwriteRoot.SetActive(false);
        base.OnClose();
        var source = settingsSource; settingsSource = null;
        if (source && source.IsOpen) source.gameObject.SetActive(true);
    }
    private void RefreshSlots()
    {
        back.interactable=pendingOverwrite==0;
        title.text = saving ? "保存游戏" : "读取存档";
        for (int i = 0; i < slots.Length; i++)
        {
            var info = SaveAndLoadManager.GetSlotInfo(i + 1);
            phaseNumbers[i].text = info.valid ? info.phase.ToString() : "—";
            dayNumbers[i].text = info.valid ? info.day.ToString() : "—";
            summaries[i].text = !info.exists ? "空存档" : !info.valid ? "存档损坏" :
                info.savedUtc > 0 ? new DateTime(info.savedUtc,DateTimeKind.Utc).ToLocalTime().ToString("yyyy-MM-dd HH:mm") : "旧版存档";
            slots[i].interactable = pendingOverwrite==0&&(saving || info.valid);
        }
    }
    private void Select(int id)
    {
        if(!IsOpen||pendingOverwrite!=0)return;
        if (saving)
        {
            if (!SystemManager.Instance.IsInitialized) return;
            var info = SaveAndLoadManager.GetSlotInfo(id);
            // 已有存档必须通过独立按钮确认，双击槽位也不能直接覆盖。
            if (info.exists)
            {
                pendingOverwrite = id;
                _overwriteText.text="覆盖存档 "+id+"？\n"+info.summary;
                _overwriteConfirm.interactable=true;
                _overwriteRoot.SetActive(true);
                RefreshSlots();
                EventSystem.current?.SetSelectedGameObject(_overwriteCancel.gameObject);
                return;
            }
            SaveSlot(id);
        }
        else if (SaveAndLoadManager.ActivateSlot(id))
        { OnClose(); SceneManager.LoadScene("PlayerGame"); }
        else { RefreshSlots(); ToastManager.ShowFeedback("无法读取该存档"); }
    }
    private void ConfirmOverwrite()
    {
        if(!IsOpen||!saving||pendingOverwrite==0)return;
        _overwriteConfirm.interactable=false;
        SaveSlot(pendingOverwrite);
    }
    private void SaveSlot(int id)
    {
        if(SystemManager.Instance.RunState.SaveSlot(id))
        {
            CancelOverwrite();
            ToastManager.ShowFeedback("存档保存成功");
        }
        else
        {
            _overwriteConfirm.interactable=true;
            ToastManager.ShowFeedback("保存失败，原存档已保留");
        }
    }
    private void CancelOverwrite()
    {
        int previous=pendingOverwrite;
        pendingOverwrite=0;_overwriteRoot.SetActive(false);RefreshSlots();
        if(previous>0)EventSystem.current?.SetSelectedGameObject(slots[previous-1].gameObject);
    }
    public override bool OnEscapePressed()
    {
        // ESC 由 UIManager 统一分发；先退出覆盖确认，再退出四槽，不穿透到来源设置页。
        if(pendingOverwrite!=0)CancelOverwrite();else OnClose();
        return true;
    }
    public static bool HandleEscape() => IsShowing&&current.OnEscapePressed();
}
