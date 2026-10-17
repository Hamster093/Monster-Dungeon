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

    private RectTransform _originalBoardParent;
    private Vector2 _originalBoardPos;
    private PlacedItem _selectedMonsterItem;
    private bool _isSelectingTarget = false;
    private Transform _originalParent;
    private Vector2 _originalPos;


    public override bool IsModal => true;

    private ContractConfig _config;

    /// <summary>接受委托时触发，外部可注册做业务处理</summary>
    private Action _onAccepted;
    private Action _onCancelled;

    public void SetOnAccepted(Action cb) => _onAccepted = cb;
    public void SetOnCancelled(Action cb) => _onCancelled = cb;

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
    private void OnEnable()
    {
        // 订阅右键事件
        if (BackpackBoard.Instance != null)
            BackpackBoard.Instance.OnItemRightClicked += OnBoardRightClick;
    }

    private void OnDisable()
    {
        // 退订
        if (BackpackBoard.Instance != null)
            BackpackBoard.Instance.OnItemRightClicked -= OnBoardRightClick;
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
        AudioManager.Instance.PlaySFX(AudioKeys.SFX_Back); // ★ 按 ESC 退出音效
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

    // 玩家点击“接受”按钮
    private void OnAcceptClick()
    {
        if (!_isSelectingTarget)
        {
            _isSelectingTarget = true;
            AudioManager.Instance.PlaySFX(AudioKeys.SFX_Click); // ★ 点击进入选怪模式音效
            var boardRoot = BackpackBoard.Instance.BoardUIRoot;

            // 1. 记录原父节点和坐标
            _originalParent = boardRoot.parent;
            _originalPos = boardRoot.anchoredPosition;

            // 2. 借到顶层，居中（false 表示相对新父节点计算，不保留原世界坐标）
            boardRoot.SetParent(UIManager.Instance.LayerTop, false);
            boardRoot.anchoredPosition = Vector2.zero;
            boardRoot.SetAsLastSibling();

            // 锁定拖拽，订阅右键...
            BackpackBoard.Instance.AllowDrag = false;
            BackpackBoard.Instance.AllowClick = false;

            ToastManager.Show("请右键点击棋盘上的魔物作为指路目标");

            return;
        }
    }


    // 玩家在棋盘上右键了
    private void OnBoardRightClick(PlacedItem item)
    {
        if (item.Instance == null) return; // 不是魔物，忽略
        AudioManager.Instance.PlaySFX(AudioKeys.SFX_Confirm); // ★ 选中目标音效
        // 记录选中的魔物
        _selectedMonsterItem = item;
        ToastManager.Show($"已选中：魔物 {item.Instance.uid}，正在为您接取委托...");

        // 立即执行接取！
        ExecuteAccept();
    }

    private void OnCloseClick()
    {
        AudioManager.Instance.PlaySFX(AudioKeys.SFX_Back); // ★ 点关闭/取消按钮音效
        var cb = _onCancelled;
        _onAccepted = null;
        _onCancelled = null;
        cb?.Invoke();

        UIManager.Instance.Close(this);
    }

    public override void OnClose()
    {

        _onAccepted = null;
        _onCancelled = null;
        base.OnClose();
        // 如果玩家没选怪就关面板
        if (_isSelectingTarget)
        {
            RestoreBoard();
        }
    }

    private void ExecuteAccept()
    {
        var sys = SystemManager.Instance?.DailyCommission;
        if (sys == null) return;

        int adventurerId = SystemManager.Instance.VisitSystem.CurrentVisitorId;

        // 塞入委托系统
        sys.AddNormal(_config.id, adventurerId, _selectedMonsterItem.Instance);
        AudioManager.Instance.PlaySFX(AudioKeys.SFX_CheckSuccess);
        ToastManager.Show("委托接取成功");

        // ★ 还原棋盘状态
        RestoreBoard();

        // 恢复面板透明度（如果有改的话）
        // GetComponent<CanvasGroup>().alpha = 1f;

        // 回调并关闭面板
        var cb = _onAccepted;
        _onAccepted = null;
        _onCancelled = null;
        cb?.Invoke();

        UIManager.Instance.Close(this);
    }
    // ==========================================
    // 4. 还原棋盘（独立方法，成功/取消时调用）
    // ==========================================
    private void RestoreBoard()
    {
        _isSelectingTarget = false;
        _selectedMonsterItem = null;

        if (_originalParent == null) return;

        var boardRoot = BackpackBoard.Instance.BoardUIRoot;

        // 1. 塞回原处并恢复坐标
        boardRoot.SetParent(_originalParent, false);
        boardRoot.anchoredPosition = _originalPos;

        // 2. 恢复面板
        var cg = GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.alpha = 1f;
            cg.blocksRaycasts = true;
        }

        // 3. 退订、恢复交互
        BackpackBoard.Instance.AllowDrag = true;
        BackpackBoard.Instance.OnItemRightClicked -= OnBoardRightClick;

        _originalParent = null;
    }
}
