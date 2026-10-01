/****************************************************
    文件：GamePassEvent.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026-09-25 17:30:04
	功能：接待界面主控制器。只需挂到根节点 ReceptionPanel 上，
         其余子节点在 Awake 中按名字递归查找。
*****************************************************/

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ReceptionPanel : PanelBase
{
    // ---------- 顶部状态栏 ----------
    private Text _dayText;
    private Text _phaseText;
    private Text _cashText;
    private Text _nutrientText;
    private Text _apText;
    private Text _remainingDays;
    private Text _installmentPayment;

    // ---------- 来客信息 ----------
    private Text _nameText;
    private Text _partyTag;
    private Text _demandText;
    private Text _observationText;
    private Text _targetZoneText;

    // ---------- 行动按钮 ----------
    private Button _talkButton;
    private Button _investigateButton;
    private Button _refuseButton;
    private Button _truthRouteButton;
    private Button _lieRouteButton;

    // ---------- 调试按钮 ----------
    private Button _nextDayButton;
    private Button _addCashButton;

    // ---------- 反馈文本 ----------
    private Text _feedbackText;

    // ---------- 选项面板 ----------
    private Transform _optionsPanel;
    private Button _optionButtonTemplate;

    // ---------- 运行时 ----------
    private int _currentAdventurerId = -1;
    private readonly List<Button> _spawnedOptions = new List<Button>();

    // ---------- 接待相关时 ----------
    private List<int> _todayVisitors = new ();// 今日访客列表
    private int _visitorIndex = 0;// 今日访客索引

    // ============================================================
    //  生命周期
    // ============================================================
    public override void OnInit()
    {
        // 顶部
        _dayText = FindByName<Text>("DayText");
        _phaseText = FindByName<Text>("PhaseText");
        _cashText = FindByName<Text>("CashText");
        _nutrientText = FindByName<Text>("NutrientText");
        _apText = FindByName<Text>("APText");
        _remainingDays = FindByName<Text>("RemainingDays");
        _installmentPayment = FindByName<Text>("InstallmentPayment");

        // 来客
        _nameText = FindByName<Text>("NameText");
        _partyTag = FindByName<Text>("PartyTag");
        _demandText = FindByName<Text>("DemandText");
        _observationText = FindByName<Text>("ObservationText");
        _targetZoneText = FindByName<Text>("TargetZoneText");

        // 按钮
        _talkButton = FindByName<Button>("TalkButton");
        _investigateButton = FindByName<Button>("InvestigateButton");
        _refuseButton = FindByName<Button>("RefuseButton");
        _truthRouteButton = FindByName<Button>("TruthRouteButton");
        _lieRouteButton = FindByName<Button>("LieRouteButton");

        // 调试
        _nextDayButton = FindByName<Button>("NextDayButton");
        _addCashButton = FindByName<Button>("AddCashButton");

        // 反馈
        _feedbackText = FindByName<Text>("FeedbackText");

        // 选项面板
        var opGo = FindByName<Transform>("OptionsPanel");
        _optionsPanel = opGo;
        var tmpl = FindByName<Button>("OptionButtonTemplate");
        if (tmpl != null)
        {
            _optionButtonTemplate = tmpl;
            tmpl.gameObject.SetActive(false);
        }
        
    }

    private void Start()
    {
        if (_talkButton) _talkButton.onClick.AddListener(OnTalk);
        if (_investigateButton) _investigateButton.onClick.AddListener(OnInvestigate);
        if (_refuseButton) _refuseButton.onClick.AddListener(OnRefuse);
        if (_truthRouteButton) _truthRouteButton.onClick.AddListener(() => OnRoute(true));
        if (_lieRouteButton) _lieRouteButton.onClick.AddListener(() => OnRoute(false));

        if (_nextDayButton) _nextDayButton.onClick.AddListener(() => NextDayEvent.Trigger());
        if (_addCashButton)
        {
            _addCashButton.onClick.AddListener(() =>
            {
                SystemManager.Instance.Economy.AddCash(2000);
                RefreshTopBar();
            });
        }

        //今日访客
        if (SystemManager.Instance.RunState.run.todayVisitors.Count > 0)
            _currentAdventurerId = SystemManager.Instance.RunState.run.todayVisitors[0];

        HideOptions();
        RefreshAll();
    }

    private void OnEnable()
    {
        TodayVisitorsReadyEvent.Register(OnVisitorsReady);
        NextDayEvent.Register(RefreshTopBar);
        ReceptionComplete.Register(OnVisitorFinished);
    }
    private void OnDisable()
    {
        TodayVisitorsReadyEvent.UnRegister(OnVisitorsReady);
        NextDayEvent.UnRegister(RefreshTopBar);
        ReceptionComplete.UnRegister(OnVisitorFinished);
    }

    // ============================================================
    //  事件
    // ============================================================
    /// <summary>
    /// 访客准备就绪
    /// </summary>
    /// <param name="ids"></param>
    private void OnVisitorsReady(List<int> ids)
    {
        HideOptions();
        _todayVisitors = ids ?? new List<int>();
        _visitorIndex = 0;
        ShowCurrentVisitor();
        RefreshTopBar();
    }

    /// <summary>
    /// 显示当前索引对应的访客
    /// </summary>
    private void ShowCurrentVisitor()
    {
        if (_todayVisitors.Count == 0 || _visitorIndex >= _todayVisitors.Count)
        {
            _currentAdventurerId = -1;
            ClearVisitor();
            return;
        }

        _currentAdventurerId = _todayVisitors[_visitorIndex];
        RefreshVisitor();
    }

    /// <summary>
    /// 接待完当前这位时调用
    /// </summary>
    public void OnVisitorFinished()
    {
        _visitorIndex++;
        ShowCurrentVisitor();
        RefreshTopBar();
    }

    // ============================================================
    //  刷新
    // ============================================================
    /// <summary>
    /// 全部刷新
    /// </summary>
    private void RefreshAll()
    {
        RefreshTopBar();
        if (_currentAdventurerId >= 0) RefreshVisitor();
        else ClearVisitor();
    }
    /// <summary>
    /// 根据当前天数和阶段刷新 RunState 中的 remainingDays 和 installmentPayment
    /// </summary>
    private void UpdateRunState()
    {
        var run = SystemManager.Instance.RunState.run;
        var cfg = ConfigDatabase.Instance;

        run.remainingDays = cfg.GetRemainingDays(run.currentDay, run.currentPhase);
        run.installmentPayment = cfg.GetInstallmentPayment(run.currentPhase);
    }

    /// <summary>
    /// 刷新顶部状态栏
    /// </summary>
    private void RefreshTopBar()
    {
        UpdateRunState();

        var run = SystemManager.Instance.RunState.run;
        if (_dayText) _dayText.text = $"第 {run.currentDay} 天";
        if (_phaseText) _phaseText.text = $"阶段 {run.currentPhase}";
        if (_cashText) _cashText.text = $"现金 {run.cash}";
        if (_nutrientText) _nutrientText.text = $"养分 {run.nutrient}";
        if (_apText) _apText.text = $"行动点 {run.actionPoint}";
        if (_remainingDays) _remainingDays.text = $"当前阶段剩余经营日 {run.remainingDays}";
        if (_installmentPayment) _installmentPayment.text = $"分期金额 {run.installmentPayment}";
    }
    /// <summary>
    /// 刷新访客列表
    /// </summary>
    private void RefreshVisitor()
    {
        var sm = SystemManager.Instance;
        if (!sm.Config.Adventurers.TryGetValue(_currentAdventurerId, out var adv)) { ClearVisitor(); return; }
        if (!sm.Config.Contracts.TryGetValue(adv.contractId, out var contract)) { ClearVisitor(); return; }
        if (!sm.Config.Monsters.TryGetValue(contract.targetMonsterId, out var mon)) { ClearVisitor(); return; }
        if (!sm.Config.Zones.TryGetValue(contract.targetZoneId, out var zone)) { ClearVisitor(); return; }

        if (_nameText) _nameText.text = $"{adv.name} · {adv.baseLevel} 级";
        if (_partyTag) _partyTag.text = adv.isParty ? "小队" : "";
        if (_demandText) _demandText.text = contract.description;
        if (_targetZoneText) _targetZoneText.text = $"目标：{zone.name} · {mon.name}";

        var state = sm.RunState.run.adventurers[_currentAdventurerId];
        var obs = "";
        if (state.health == HealthState.Damaged) obs += "身上有伤。";
        if (state.health == HealthState.Critical) obs += "伤势严重。";
        if (state.equipment == EquipmentState.Damaged) obs += "装备有磨损。";
        if (state.equipment == EquipmentState.Critical) obs += "装备破损严重。";
        if (_observationText)
            _observationText.text = string.IsNullOrEmpty(obs) ? "看起来状态正常。" : obs;
    }
    /// <summary>
    /// 清空访客列表
    /// </summary>
    private void ClearVisitor()
    {
        if (_nameText) _nameText.text = "今日无来客";
        if (_partyTag) _partyTag.text = "";
        if (_demandText) _demandText.text = "";
        if (_observationText) _observationText.text = "";
        if (_targetZoneText) _targetZoneText.text = "";
    }
    /// <summary>
    /// 添加反馈文本
    /// </summary>
    /// <param name="msg"></param>
    private void AppendFeedback(string msg)
    {
        if (_feedbackText) _feedbackText.text += msg + "\n";
        Debug.Log(msg);
    }

    // ============================================================
    //  按钮事件
    // ============================================================
    /// <summary>
    /// 搭话
    /// </summary>
    private void OnTalk()
    {
        if (_currentAdventurerId < 0) { AppendFeedback("没有来客。"); return; }

        var sm = SystemManager.Instance;
        var options = sm.Reception.GetTalkOptions(_currentAdventurerId);
        if (options == null || options.Count == 0)
        {
            AppendFeedback("没有可用的搭话选项。");
            return;
        }

        ShowTalkOptions(options);
    }
    /// <summary>
    /// 调查
    /// </summary>
    private void OnInvestigate()
    {
        if (_currentAdventurerId < 0) { AppendFeedback("没有来客。"); return; }

        var result = SystemManager.Instance.Reception.Investigate(_currentAdventurerId);
        AppendFeedback(result.Message);
        RefreshTopBar();
    }
    /// <summary>
    /// 拒绝
    /// </summary>
    private void OnRefuse()
    {
        if (_currentAdventurerId < 0) { AppendFeedback("没有来客。"); return; }

        var result = SystemManager.Instance.Reception.Refuse(_currentAdventurerId);
        AppendFeedback(result.Message);

        HideOptions();
        _currentAdventurerId = -1;
        ClearVisitor();
        RefreshTopBar();

        ReceptionComplete.Trigger();//完成接待，触发事件
    }
    /// <summary>
    /// 指路
    /// </summary>
    /// <param name="isTruth"></param>
    private void OnRoute(bool isTruth)
    {
        if (_currentAdventurerId < 0) { AppendFeedback("没有来客。"); return; }

        var result = SystemManager.Instance.Reception.GiveRoute(_currentAdventurerId, isTruth);
        AppendFeedback(result.Message);

        if (result.ShouldEndVisit)
        {
            HideOptions();
            _currentAdventurerId = -1;
            ClearVisitor();
        }
        RefreshTopBar();

        ReceptionComplete.Trigger();//完成接待，触发事件
    }

    // ============================================================
    //  搭话选项面板
    // ============================================================
    /// <summary>
    /// 显示对话选项
    /// </summary>
    /// <param name="options"></param>
    private void ShowTalkOptions(List<DialogueOptionConfig> options)
    {
        if (_optionsPanel == null || _optionButtonTemplate == null)
        {
            AppendFeedback("缺少 OptionsPanel 或 OptionButtonTemplate。");
            return;
        }

        HideOptions();

        var sm = SystemManager.Instance;
        var state = sm.RunState.run.adventurers[_currentAdventurerId];

        foreach (var opt in options)
        {
            int cost = sm.ActionPoint.GetTalkCost(state.formalTalkCount);
            bool canAfford = sm.ActionPoint.CanSpend(cost);

            var btn = Instantiate(_optionButtonTemplate, _optionsPanel);
            btn.gameObject.SetActive(true);

            var label = btn.GetComponentInChildren<Text>();
            if (label != null)
                label.text = $"{opt.text}（{cost} AP）";

            btn.interactable = canAfford;

            int optionId = opt.id;
            btn.onClick.AddListener(() => OnOptionClicked(optionId));

            _spawnedOptions.Add(btn);
        }
    }
    /// <summary>
    /// ShowTalkOptions点击回调
    /// </summary>
    /// <param name="optionId"></param>
    private void OnOptionClicked(int optionId)
    {
        if (_currentAdventurerId < 0) { HideOptions(); return; }

        var result = SystemManager.Instance.Reception.Talk(_currentAdventurerId, optionId);
        AppendFeedback(result.Message);

        HideOptions();
        RefreshTopBar();
    }
    /// <summary>
    /// 隐藏选项
    /// </summary>
    private void HideOptions()
    {
        foreach (var btn in _spawnedOptions)
        {
            if (btn != null) Destroy(btn.gameObject);
        }
        _spawnedOptions.Clear();
    }

    // ============================================================
    //  工具：按名字递归查找
    // ============================================================
    private T FindByName<T>(string nodeName) where T : Component
        => FindRecursive<T>(transform, nodeName);

    private T FindRecursive<T>(Transform root, string nodeName) where T : Component
    {
        if (root.name == nodeName)
        {
            var c = root.GetComponent<T>();
            if (c != null) return c;
        }
        for (int i = 0; i < root.childCount; i++)
        {
            var result = FindRecursive<T>(root.GetChild(i), nodeName);
            if (result != null) return result;
        }
        return null;
    }
}