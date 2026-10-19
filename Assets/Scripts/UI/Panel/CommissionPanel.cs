/****************************************************
    文件：CommissionPanel.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/9 23:19:02
	功能：委托面板，展示一条委托详情
*****************************************************/
using System;
using UnityEngine;
using UnityEngine.UI;

public class CommissionPanel : PanelBase
{
    [Header("文本")]
    [SerializeField] private Text txt_name;
    [SerializeField] private Text txt_Description;
    [SerializeField] private Text txt_Objectives;
    [SerializeField] private Text txt_Payment;

    [Header("按钮")]
    [SerializeField] private Button btn_AcceptCommission;
    [SerializeField] private Button CloseButton;

    public override bool IsModal => true;

    private ContractConfig _config;
    private string _objective, _payment;
    private bool _accepted;
    private ScrollRect _descriptionScroll;
    public void SetTextContext(string objective, string payment) { _objective = objective; _payment = payment; Refresh(); }

    /// <summary>接受委托时触发，外部可注册做业务处理</summary>
    private Action _onAccepted;
    private Action _onCancelled;

    public void SetOnAccepted(Action cb) => _onAccepted = cb;
    public void SetOnCancelled(Action cb) => _onCancelled = cb;

    public override void OnInit()
    {
        if (txt_Description != null)
        {
            var textRect = txt_Description.rectTransform;
            var viewport = new GameObject("CommissionDescriptionViewport", typeof(RectTransform), typeof(RectMask2D), typeof(ScrollRect));
            var rect = (RectTransform)viewport.transform;
            rect.SetParent(textRect.parent, false);
            rect.anchorMin = textRect.anchorMin; rect.anchorMax = textRect.anchorMax;
            rect.pivot = textRect.pivot; rect.anchoredPosition = textRect.anchoredPosition; rect.sizeDelta = textRect.sizeDelta;
            textRect.SetParent(rect, false); textRect.anchorMin = new Vector2(0, 1); textRect.anchorMax = Vector2.one;
            textRect.pivot = new Vector2(0.5f, 1); textRect.anchoredPosition = Vector2.zero; textRect.sizeDelta = new Vector2(0, rect.rect.height);
            txt_Description.alignment = TextAnchor.UpperLeft; txt_Description.verticalOverflow = VerticalWrapMode.Overflow;
            _descriptionScroll = viewport.GetComponent<ScrollRect>();
            _descriptionScroll.viewport = rect; _descriptionScroll.content = textRect;
            _descriptionScroll.horizontal = false; _descriptionScroll.scrollSensitivity = 24;
            _descriptionScroll.movementType = ScrollRect.MovementType.Clamped;
        }
        if (CloseButton != null)
            CloseButton.onClick.AddListener(OnCloseClick);
        else
            Debug.LogError("[CommissionPanel] CloseButton 未赋值!", this);

        if (btn_AcceptCommission != null)
            btn_AcceptCommission.onClick.AddListener(OnAcceptClick);
        else
            Debug.LogError("[CommissionPanel] btn_AcceptCommission 未赋值!", this);
    }

    public override void OnDestroy()
    {
        if (CloseButton != null)
            CloseButton.onClick.RemoveListener(OnCloseClick);

        if (btn_AcceptCommission != null)
            btn_AcceptCommission.onClick.RemoveListener(OnAcceptClick);

        _onAccepted = null;
        _onCancelled= null;

        base.OnDestroy();
    }

    public override void OnOpen(object data = null)
    {
        base.OnOpen(data);
        _accepted = false; _objective = null; _payment = null;

        if (data == null)
        {
            Debug.LogError("[CommissionPanel] OnOpen 未传入委托 id", this);
            return;
        }

        int contractId;
        if (data is int id)
        {
            contractId = id;
        }
        else if (!int.TryParse(data.ToString(), out contractId))
        {
            Debug.LogError($"[CommissionPanel] 无法解析委托 id: {data}", this);
            return;
        }

        var dict = ConfigDatabase.Instance?.Contracts;
        if (dict == null)
        {
            Debug.LogError("[CommissionPanel] ConfigDatabase.Contracts 为空", this);
            return;
        }

        if (!dict.TryGetValue(contractId, out _config) || _config == null)
        {
            Debug.LogError($"[CommissionPanel] 找不到委托配置 id = {contractId}", this);
            return;
        }

        Refresh();
    }

    public override bool OnEscapePressed()
    {
        UIManager.Instance.Close(this);
        return true;
    }

    // ───────────── 刷新显示 ─────────────

    private void Refresh()
    {
        if (_config == null) return;

        if (txt_name != null)
            txt_name.text = _config.name;

        if (txt_Description != null)
        {
            txt_Description.text = _config.description;
            txt_Description.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(_descriptionScroll.viewport.rect.height, txt_Description.preferredHeight + 8));
            _descriptionScroll.verticalNormalizedPosition = 1;
        }

        if (txt_Objectives != null)
            txt_Objectives.text = IsConfigured(_objective) ? _objective : BuildObjectiveText(_config);

        if (txt_Payment != null)
            txt_Payment.text = IsConfigured(_payment) ? (_payment.Contains(_config.reward.ToString()) ? _payment : _payment + "\n" + _config.reward + " 金币") : _config.reward + " 金币";
    }

    /// <summary>根据委托配置拼出“目标”文本，具体格式你可以按需改</summary>
    private string BuildObjectiveText(ContractConfig cfg)
    {
        if (cfg.targetMonsterId <= 0)
            return "无指定目标";

        if (ConfigDatabase.Instance.Monsters.TryGetValue(cfg.targetMonsterId, out var monster))
            return "目标类别：" + DailyCommissionSystem.TypeName(DailyCommissionSystem.TargetType(cfg));

        return $"讨伐目标 ID：{cfg.targetMonsterId}";
    }
    private static bool IsConfigured(string text) => !string.IsNullOrEmpty(text) && !text.Contains("待主策配置") && !text.Contains("由委托配置显示");

    // ───────────── 按钮回调 ─────────────

    private void OnAcceptClick()
    {
        if (_config == null || _accepted || !IsOpen) return;

        var sys = SystemManager.Instance?.DailyCommission;
        if (sys == null) return;

        if (SystemManager.Instance.Narrative?.Enabled == true)
        {
            SystemManager.Instance.Narrative.RequestNormalRoute();
            return;
        }
        // 兼容原独立 JSON 模式，同样必须确认棋盘目标后才入队。
        int actor = SystemManager.Instance.VisitSystem.CurrentVisitorId;
        UIManager.Instance.Open<RouteSelectionPanel>(new RouteSelectionPanel.OpenData {
            state = new RouteSelectionPanel.RouteState { contractId = _config.id, adventurerId = actor },
            confirmed = item => {
                int target=int.Parse(item.Def.Id);
                var entry = new CommissionEntry(_config.id, actor) { targetMonsterId=target, targetUid=item.Uid, requestedMonsterType=DailyCommissionSystem.TargetType(_config), wrongRoute=ConfigDatabase.Instance.Monsters[target].monsterType!=DailyCommissionSystem.TargetType(_config), rankFactor=DailyCommissionSystem.RankFactor(ConfigDatabase.Instance.Adventurers[actor].baseLevel,ConfigDatabase.Instance.Monsters[target].level) };
                sys.AddNormal(entry); CompleteRouteAcceptance(); ToastManager.Show("委托接取成功");
            }
        });
    }

    /// <summary>唯一提交出口，指路确认前不修改 _accepted，也不触发接单回调。</summary>
    public void CompleteRouteAcceptance()
    {
        if (_accepted || !IsOpen) return;
        _accepted = true;
        UIManager.Instance.Close(this);
    }

    private void OnCloseClick()
    {
        UIManager.Instance.Close(this);
    }

    public override void OnClose()
    {
        // 确认、关闭、遮罩、ESC 都经由同一个结果出口，先关闭再恢复对话。
        var callback = _accepted ? _onAccepted : _onCancelled;
        _onAccepted = null;
        _onCancelled = null;
        base.OnClose();
        callback?.Invoke();
    }
}
