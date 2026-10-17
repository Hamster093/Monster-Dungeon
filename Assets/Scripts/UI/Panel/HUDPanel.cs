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
        var run = SystemManager.Instance.RunState.run;
        if (_dayText) _dayText.text = $" {run.currentDay} ";//第 {run.currentDay} 天
        if (_phaseText) _phaseText.text = $" {run.currentPhase}";//阶段
        if (_cashText) _cashText.text = $" {run.cash}";//现金
        if (_nutrientText) _nutrientText.text = $"养分 {run.nutrient}";
        if (_apText) _apText.text = $" {run.actionPoint}";//行动点
        if (_remainingDays) _remainingDays.text = $"当前阶段剩余经营日 {run.remainingDays}";
        if (_installmentPayment) _installmentPayment.text = $"分期金额 {run.installmentPayment}";
    }
    #endregion

    #region OnButton

        private void ToggleBackpack()
    {
        if (UIManager.Instance.IsOpen<BackpackPanel>())
        {
            AudioManager.Instance.PlaySFX(AudioKeys.SFX_Back); // ★ 关闭背包
            UIManager.Instance.Close<BackpackPanel>();
        }
        else
        {
            AudioManager.Instance.PlaySFX(AudioKeys.SFX_Click); // ★ 打开背包
            UIManager.Instance.Open<BackpackPanel>();
        }
    }

    /// <summary>设置面板（同样做成开关式）</summary>
    private void ToggleSettings()
    {
        if (UIManager.Instance.IsOpen<SettingsPanel>())
        {
            AudioManager.Instance.PlaySFX(AudioKeys.SFX_Back); // ★ 关闭设置
            UIManager.Instance.Close<SettingsPanel>();
        }
        else
        {
            AudioManager.Instance.PlaySFX(AudioKeys.SFX_Click); // ★ 打开设置
            UIManager.Instance.Open<SettingsPanel>();
        }
    }

    /// <summary>历史记录（由 TalkPanel 提供的静态入口）</summary>
    private void OpenDialogueHistory()
    {
        if (!UIManager.Instance.IsOpen<TalkPanel>()) return;

        if (UIManager.Instance.IsOpen<DialogueHistoryPanel>())
        {
            AudioManager.Instance.PlaySFX(AudioKeys.SFX_Back); // ★ 关闭历史
            UIManager.Instance.Close<DialogueHistoryPanel>();
        }
        else
        {
            AudioManager.Instance.PlaySFX(AudioKeys.SFX_Click); // ★ 打开历史
            UIManager.Instance.Open<DialogueHistoryPanel>();
        }
    }

    /// <summary>人物档案（由 TalkPanel 提供的静态入口）</summary>
    private void OpenCharacterArchive()
    {
        var talkPanel = UIManager.Instance.Get<TalkPanel>();
        if (talkPanel == null)
        {
            Debug.LogWarning("[HUDPanel] 场景中不存在 TalkPanel，无法打开角色档案");
            return;
        }
        // 音效在 TalkPanel.ToggleCharacterArchive 内部已经处理
        talkPanel.ToggleCharacterArchive();
    }

    #endregion
}


