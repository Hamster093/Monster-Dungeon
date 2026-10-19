/****************************************************
    文件：DailySettlementPanel.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/15 15:43:37
	功能：每日结算面板，展示当日各项收益，模态显示。
*****************************************************/

using System;
using UnityEngine;
using UnityEngine.UI;

/****************************************************
    文件：SettlementData.cs
    功能：每日结算数据载体
*****************************************************/
[System.Serializable]
public class SettlementData
{
    public int legacyCommissionIncome;   // 旧版委托收益
    public int specialCommissionIncome;  // 特殊委托收益
    public int monsterBusinessIncome;    // 魔物经营收益
    public int totalIncome;              // 总收益
    public System.Collections.Generic.List<string> notes = new();
    public int installmentDue;           // 下阶段应缴纳分期

    public SettlementData() { }

    public SettlementData(int legacy, int special, int monster, int total, int installment)
    {
        legacyCommissionIncome = legacy;
        specialCommissionIncome = special;
        monsterBusinessIncome = monster;
        totalIncome = total;
        installmentDue = installment;
    }
}

public class DailySettlementPanel : PanelBase
{
    [Header("文本")]
    [SerializeField] private Text txt_LegacyCommission;   // 旧版委托收益
    [SerializeField] private Text txt_SpecialCommission;  // 特殊委托收益
    [SerializeField] private Text txt_MonsterBusiness;    // 魔物经营收益
    [SerializeField] private Text txt_Total;              // 总收益
    [SerializeField] private Text txt_Installment;        // 下阶段应缴纳分期

    [Header("关闭")]
    [SerializeField] private Button btn_Close;

    public override bool IsModal => true;

    /// <summary>面板关闭时触发（点遮罩、点关闭按钮、ESC 都会走这里）</summary>

    [SerializeField] private Text _detailsText;
    [SerializeField] private ScrollRect _detailsScroll;
    private SettlementData _data;

    public override void OnInit()
    {
        if (btn_Close != null)
            btn_Close.onClick.AddListener(OnCloseClick);
    }

    public override void OnDestroy()
    {
        if (btn_Close != null)
            btn_Close.onClick.RemoveListener(OnCloseClick);

        base.OnDestroy();
    }

    public override void OnOpen(object data = null)
    {
        base.OnOpen(data);
        _closedNotified = false;
        // 接单等操作提示不能覆盖结算标题；账目是独立的确认层。
        ToastManager.Instance?.Clear();

        if (data is SettlementData d)
            _data = d;
        else
        {
            Debug.LogWarning("[DailySettlementPanel] OnOpen 未传入 SettlementData，使用默认 0");
            _data = new SettlementData();
        }

        RefreshUI();
    }

    public override void OnClose()
    {
        base.OnClose();

    }


    // ───────────── 刷新显示 ─────────────

    /// <summary>外部手动赋值也可调用，方便后面接计算函数</summary>
    public void SetData(SettlementData data)
    {
        _data = data ?? new SettlementData();
        RefreshUI();
    }

    /// <summary>
    /// 便捷重载：5 个数值直接传，内部打包成 SettlementData
    /// </summary>
    public void SetData(int legacy, int special, int monster, int total, int installment)
    {
        SetData(new SettlementData(legacy, special, monster, total, installment));
    }

    private void RefreshUI()
    {
        if (_data == null) _data = new SettlementData();
        if(_detailsText)_detailsText.text=string.Join("\n\n",_data.notes.ConvertAll(NarrativeSystem.HidePrivateSuccessionProgress));
        if(_detailsScroll){Canvas.ForceUpdateCanvases();_detailsText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,Mathf.Max(_detailsScroll.viewport.rect.height,_detailsText.preferredHeight+24));_detailsScroll.StopMovement();_detailsScroll.verticalNormalizedPosition=1;}

        if (txt_LegacyCommission != null)
            txt_LegacyCommission.text = $"普通委托\n<size=42>{_data.legacyCommissionIncome}</size> 金币";

        if (txt_SpecialCommission != null)
            txt_SpecialCommission.gameObject.SetActive(false); // 旧存档字段可读，当前结算不再展示此栏。

        if (txt_MonsterBusiness != null)
            txt_MonsterBusiness.text = $"魔物经营\n<size=42>{_data.monsterBusinessIncome}</size> 金币";

        if (txt_Total != null)
            txt_Total.text = $"今日收入\n<size=62>{_data.totalIncome}</size> 金币";

        if (txt_Installment != null)
            txt_Installment.text = $"阶段应缴：{_data.installmentDue} 金币";
    }

    // ───────────── 按钮 ─────────────
    private bool _closedNotified;
    private void OnCloseClick()
    {
        if (_closedNotified) return;
        _closedNotified = true;

        if (SystemManager.Instance.Narrative?.Enabled == true)
        {
            UIManager.Instance.Close(this);
            SystemManager.Instance.Narrative.CompleteSettlement(_data);
            return;
        }

        if (_data != null)
            SystemManager.Instance.Economy.AddCash(_data.totalIncome);

        // 2. 清空当日委托队列
        SystemManager.Instance.DailyCommission.Clear();

        UIManager.Instance.Close(this);
        ScreenFader.Instance.FadeFromBlack(2f, () =>
        {
            NextDayEvent.Trigger();
        });
    }
}
   
