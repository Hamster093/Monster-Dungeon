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

    /// <summary>接受委托时触发，外部可注册做业务处理</summary>
    public event Action<ContractConfig> OnAccept;

    public override void OnInit()
    {
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

        OnAccept = null;

        base.OnDestroy();
    }

    public override void OnOpen(object data = null)
    {
        base.OnOpen(data);

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
            txt_Description.text = _config.description;

        if (txt_Objectives != null)
            txt_Objectives.text = BuildObjectiveText(_config);

        if (txt_Payment != null)
            txt_Payment.text = _config.reward.ToString();
    }

    /// <summary>根据委托配置拼出“目标”文本，具体格式你可以按需改</summary>
    private string BuildObjectiveText(ContractConfig cfg)
    {
        if (cfg.targetMonsterId <= 0)
            return "无指定目标";

        if (ConfigDatabase.Instance.Monsters.TryGetValue(cfg.targetMonsterId, out var monster))
            return $"讨伐目标：{monster.name}";

        return $"讨伐目标 ID：{cfg.targetMonsterId}";
    }

    // ───────────── 按钮回调 ─────────────

    private void OnAcceptClick()
    {
        if (_config == null) return;

        var sys = SystemManager.Instance?.DailyCommission;
        if (sys == null)
        {
            Debug.LogError("[CommissionPanel] DailyCommission 为空", this);
            return;
        }

        int adventurerId = SystemManager.Instance.VisitSystem.CurrentVisitorId;
        sys.AddNormal(_config.id, adventurerId);

        OnAccept?.Invoke(_config);

        ToastManager.Show("委托接取成功");

        UIManager.Instance.Close(this);
    }

    private void OnCloseClick()
    {
        UIManager.Instance.Close(this);
    }
}
