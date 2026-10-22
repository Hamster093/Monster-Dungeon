/****************************************************
    文件：HUDPanel.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026-09-21 12:20:23
	功能：Nothing
*****************************************************/

using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class HUDPanel : PanelBase
{
    public Transform ButtonRoot;
    [SerializeField] private Button _playerButton;
    [SerializeField] private Text _playerText;
    [SerializeField] private Text _revengeWarning;
    // ---------- 顶部状态栏 ----------
    private Text _dayText;
    private Text _phaseText;
    private Text _cashText;
    private Text _nutrientText;
    private Text _apText;
    private Text _remainingDays;
    private Text _installmentPayment;

    public override bool IsModal => false;

    public override bool OnEscapePressed() => false;

    public override void OnInit()
    {
        if (_playerButton) _playerButton.onClick.AddListener(TogglePlayer);
        BindButtonClick("Backpack", ToggleBackpack);
        BindButtonClick("Settings", ToggleSettings);          // 新增：设置
        BindButtonClick("DialogueHistory", OpenDialogueHistory);     // 新增：历史记录
        BindButtonClick("CharacterArchive", OpenCharacterArchive);    // 新增：人物档案


        // 顶部
        _dayText = FindByName<Text>("DayText");
        _phaseText = FindByName<Text>("PhaseText");
        _cashText = FindByName<Text>("CashText");
        _nutrientText = FindByName<Text>("NutrientText");
        _apText = FindByName<Text>("APText");
        _remainingDays = FindByName<Text>("RemainingDays");
        _installmentPayment = FindByName<Text>("InstallmentPayment");
    }

    private void Start()
    {
        SystemManager.Instance.Schedule.RefreshScheduleData(); ;
        RefreshTopBar();
    }

    private void OnEnable()
    {
        NextDayEvent.Register(RefreshTopBar);
        TopBarRefreshEvent.Register(RefreshTopBar);
    }

    private void OnDisable()
    {
        NextDayEvent.UnRegister(RefreshTopBar);
        TopBarRefreshEvent.UnRegister(RefreshTopBar);
    }

    private void Update()
    {
        RefreshPlayerEntry(); // 转场/模态关闭后立即恢复入口，不能等下次金币或日期事件。
        if(_revengeWarning){string warning=SystemManager.Instance.Narrative?.RevengeWarning??"";if(_revengeWarning.text!=warning)_revengeWarning.text=warning;}
        if (GuideHelpOverlay.BlocksInput || PresentationEffects.IsBusy || PlayerBuildPanel.IsChoosing || EndingPresentationPanel.IsShowing || UIManager.Instance.LastModalCloseFrame == Time.frameCount) return;
        if (Input.GetButtonDown("Inventory"))
        {
            ToggleBackpack();
        }
    }
    public override void OnClose()
    {
        Debug.Log($"[HUDPanel] OnClose 调用栈:\n{System.Environment.StackTrace}");
        base.OnClose();
    }


    private void BindButtonClick(string buttonName,UnityAction action)
    {
        ButtonRoot.Find(buttonName).Find(buttonName+"Button").GetComponent<Button>().onClick.AddListener(action);
    }

    #region TopBar


    /// <summary>
    /// 刷新顶部状态栏
    /// </summary>
    public void RefreshTopBar()
    {
        var run = SystemManager.Instance.RunStateManager.run;
        RefreshPlayerEntry();
        var archive = ButtonRoot.Find("CharacterArchive/CharacterArchiveButton").GetComponent<Button>();
        archive.interactable = SystemManager.Instance.Narrative?.Enabled == true
            ? SystemManager.Instance.Narrative.CanBrowseArchive
            : UIManager.Instance.IsOpen<TalkPanel>() && SystemManager.Instance.VisitSystem.CurrentVisitorId > 0;
        if (archive.targetGraphic) archive.targetGraphic.color = archive.interactable ? Color.white : new Color(.4f,.4f,.4f,1);
        if (_dayText) _dayText.text = $" {run.currentDay} ";//第 {run.currentDay} 天
        if (_phaseText) _phaseText.text = $" {run.currentPhase}";//阶段
        if (_cashText) _cashText.text = $" {run.cash}";//现金
        if (_nutrientText) _nutrientText.text = $"养分 {run.nutrient}";
        if (_apText) _apText.text = $" {run.actionPoint}";//行动点
        if (_remainingDays) _remainingDays.text = $"当前阶段剩余经营日 {run.remainingDays}";
        if (_installmentPayment) _installmentPayment.text = $"分期金额 {run.installmentPayment}";
    }
    private void RefreshPlayerEntry()
    {
        var run=SystemManager.Instance?.RunStateManager?.run;if(run==null)return;
        if (_playerButton && run.player != null)
        {
            _playerButton.gameObject.SetActive(run.player.created || run.currentDay <= 3);
            _playerButton.interactable = run.player.created && (!UIManager.Instance.HasLeftOverlay || PlayerBuildPanel.IsShowing);
            if (_playerText) _playerText.text = "自身状态　›";
        }
    }
    #endregion

    #region OnButton

        private void ToggleBackpack()
    {
        // 已打开就关闭，没打开就打开
        if (UIManager.Instance.IsOpen<BackpackPanel>())
            UIManager.Instance.Close<BackpackPanel>();
        else
            UIManager.Instance.Open<BackpackPanel>();
    }

    /// <summary>设置面板（同样做成开关式）</summary>
    private void TogglePlayer()
    {
        if (GuideHelpOverlay.BlocksInput || PresentationEffects.IsBusy || !RunStateManager.Instance.run.player.created) return;
        if (UIManager.Instance.HasLeftOverlay && !PlayerBuildPanel.IsShowing) return;
        if (UIManager.Instance.IsOpen<PlayerBuildPanel>()) UIManager.Instance.Close<PlayerBuildPanel>();
        else UIManager.Instance.Open<PlayerBuildPanel>();
    }
    private void ToggleSettings()
    {
        if (UIManager.Instance.IsOpen<SettingsPanel>())
            UIManager.Instance.Close<SettingsPanel>();
        else
            UIManager.Instance.Open<SettingsPanel>();
    }

    /// <summary>历史记录（由 TalkPanel 提供的静态入口）</summary>
    private void OpenDialogueHistory()
    {

        if (!UIManager.Instance.IsOpen<TalkPanel>()) return;
        if (UIManager.Instance.IsOpen<DialogueHistoryPanel>())
            UIManager.Instance.Close<DialogueHistoryPanel>();
        else
            UIManager.Instance.Open<DialogueHistoryPanel>();
    }

    /// <summary>人物档案（由 TalkPanel 提供的静态入口）</summary>
    private void OpenCharacterArchive()
    {
        if (SystemManager.Instance.Narrative?.Enabled == true && !SystemManager.Instance.Narrative.CanBrowseArchive) return;
        var talkPanel = UIManager.Instance.Get<TalkPanel>();
        if (talkPanel == null)
        {
            Debug.LogWarning("[HUDPanel] 场景中不存在 TalkPanel，无法打开角色档案");
            return;
        }
        talkPanel.ToggleCharacterArchive();
    }

    #endregion
}


