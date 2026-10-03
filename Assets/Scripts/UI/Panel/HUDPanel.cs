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
        if (_dayText) _dayText.text = $"第 {run.currentDay} 天";
        if (_phaseText) _phaseText.text = $"阶段 {run.currentPhase}";
        if (_cashText) _cashText.text = $"现金 {run.cash}";
        if (_nutrientText) _nutrientText.text = $"养分 {run.nutrient}";
        if (_apText) _apText.text = $"行动点 {run.actionPoint}";
        if (_remainingDays) _remainingDays.text = $"当前阶段剩余经营日 {run.remainingDays}";
        if (_installmentPayment) _installmentPayment.text = $"分期金额 {run.installmentPayment}";
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
    #endregion
}

