/// <summary>
/// 阶段控制器：持有棋盘 + 阶段配置，负责"切阶段"的完整流程。
/// 挂在场景里，Inspector 拖上 board 和 config 即可。
/// </summary>

using System.Collections.Generic;
using UnityEngine;

public class StageController : MonoBehaviour
{
    [SerializeField] private BackpackBoard board;
    [SerializeField] private StageTileLayoutConfig config;
    [SerializeField] private int startStage = 1;

    public int CurrentStage { get; private set; }

    private void Awake()
    {
        CurrentStage = startStage;
    }

    private void OnEnable()
    {
        PhaseEndedEvent.Register(OnPhaseEnded);
    }

    private void OnDisable()
    {
        PhaseEndedEvent.UnRegister(OnPhaseEnded);
    }

    private void Start()
    {
        ApplyStage(CurrentStage);
    }

    // ── 事件入口 ──────────────────────────────────
    private void OnPhaseEnded(int endedStage)
    {
        if (endedStage != CurrentStage) return;   // 不是当前阶段的事件，忽略

        ApplyStage(CurrentStage + 1);
    }

    // ── 对外手动调用 ──────────────────────────────
    public void NextStage() => ApplyStage(CurrentStage + 1);

    public void ApplyStage(int stage)
    {
        if (board == null) { Debug.LogError("[StageController] board 未绑定"); return; }
        if (config == null) { Debug.LogError("[StageController] config 未绑定"); return; }

        ReturnAllItemsToStorage();
        StageTileApplier.Apply(board, config, stage);

        CurrentStage = stage;
        Debug.Log($"[StageController] 已切到阶段 {stage}");

        RefreshShopPanelIfOpen();
    }

    private void ReturnAllItemsToStorage()
    {
        var snapshot = new List<ItemView>(board.Items);

        foreach (var v in snapshot)
        {
            if (v == null) continue;

            if (v.Item == null || v.Item.Def == null)
            {
                Destroy(v.gameObject);
                continue;
            }

            if (int.TryParse(v.Item.Def.Id, out int id))
                MonsterStorageModel.Instance.Add(id, v.Item.Uid, v.Item.Evolved,v.Item.UsedDay,v.Item.EvolutionKind);

            board.RemoveItem(v.Item);
            Destroy(v.gameObject);
        }
    }

    private void RefreshShopPanelIfOpen()
    {
        if (UIManager.Instance == null) return;
        var panel = UIManager.Instance.Get<ShopPanel>();
        if (panel != null && panel.IsOpen)
            panel.SwitchToStorage();
    }
}
