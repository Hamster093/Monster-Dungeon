/****************************************************
    文件：DailyDebriefDirector.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/16 16:13:27
	功能：每日回访系统
*****************************************************/

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DailyDebriefSystem
{
    private readonly RunStateManager _runState;
    private readonly VisitSystem _visitSystem;

    private bool _running;

    public DailyDebriefSystem(RunStateManager runState, VisitSystem visitSystem)
    {
        _runState = runState;
        _visitSystem = visitSystem;
    }

    // ───────────── 生命周期 ─────────────

    public void Init()
    {
        DialogueController.OnAllVisitorsFinished += OnAllVisitorsFinished;
    }

    public void Dispose()
    {
        DialogueController.OnAllVisitorsFinished -= OnAllVisitorsFinished;
    }

    // ───────────── 入口 ─────────────

    private void OnAllVisitorsFinished(DialogueController controller)
    {
        if (_running)
        {
            controller.ContinueToSettlement();
            return;
        }

        _running = true;

        SystemManager.Instance.DailyCommission.ResolveAllChecks();

        var visitors = new List<int>(_visitSystem.TodayVisitors);

        var results = new List<AdventureResult>(visitors.Count);
        foreach (var id in visitors)
            results.Add(RunCheck(id));

        var summary = BuildSummaryLines(results);

        // ★ 黑屏 → 亮屏，作为纯过渡，不依赖对话完成
        ScreenFader.Instance.FadeToBlack(1f, () =>
        {
            ScreenFader.Instance.FadeFromBlack(1f, () =>
            {
                // 亮屏后，先把总结当第一段回访播，再逐个汇报
                PlaySummaryThenDebrief(controller, visitors, results, summary);
            });
        });
    }

    private void PlaySummaryThenDebrief(
    DialogueController controller,
    List<int> visitors,
    List<AdventureResult> results,
    List<DialogueLine> summary)
    {
        if (summary != null && summary.Count > 0)
        {
            controller.PlayReportLines(summary, () =>
            {
                PlayDebrief(controller, visitors, results, 0);
            });
        }
        else
        {
            PlayDebrief(controller, visitors, results, 0);
        }
    }

    // ───────────── 回访递归 ─────────────

    private void PlayDebrief(
     DialogueController controller,
     List<int> visitors,
     List<AdventureResult> results,
     int index)
    {
        if (index >= visitors.Count)
        {
            _running = false;
            // ★ 最后一行已经在屏幕上，等 1 秒再进结算
            DelayedSettlement(controller, 1f);
            return;
        }

        var lines = BuildReportLines(visitors[index], results[index]);
        controller.PlayReportLines(lines, () =>
        {
            PlayDebrief(controller, visitors, results, index + 1);
        });
    }

    private void DelayedSettlement(DialogueController controller, float delay)
    {
        // 用一个 MonoBehaviour 帮忙跑协程
        var runner = ScreenFader.Instance;
        if (runner == null)
        {
            controller.ContinueToSettlement();
            return;
        }

        runner.StartCoroutine(DelayedSettlementRoutine(controller, delay));
    }

    private IEnumerator DelayedSettlementRoutine(DialogueController controller, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        controller.ContinueToSettlement();
    }

    // ───────────── 业务（TODO） ─────────────

    private AdventureResult RunCheck(int visitorId)
    {
        // TODO: 用 D20Service / AdventureResolver 算检定
        return new AdventureResult();
    }

    private List<DialogueLine> BuildSummaryLines(List<AdventureResult> results)
    {
        // TODO: 根据检定结果拼总结台词
        return new List<DialogueLine>
        {
            new DialogueLine { speakerName = "旁白", content = "今天的接待结束了。" },
            new DialogueLine { speakerName = "旁白", content = "冒险者们陆续回来汇报情况。" },
        };
    }

    private List<DialogueLine> BuildReportLines(int visitorId, AdventureResult result)
    {
        // TODO: 根据访客和结果拼汇报台词
        return new List<DialogueLine>
        {
            new DialogueLine { speakerName = "旁白", content = $"访客 {visitorId} 回来汇报了。" },
            new DialogueLine { speakerName = "旁白", content = $"访客 {visitorId} 回来汇报了//////。" },
        };
    }
}