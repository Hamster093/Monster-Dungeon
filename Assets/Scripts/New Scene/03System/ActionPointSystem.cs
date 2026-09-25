/****************************************************
    文件：ActionPointSystem.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/9/25 0:16:18
	功能：行动点数系统
*****************************************************/

public class ActionPointSystem
{
    private readonly RunStateManager _runState;


    public ActionPointSystem(RunStateManager runState)
    {
        _runState = runState;
    }

    public int Current => _runState.run.actionPoint;

    public void Init()
    {
        NextDayEvent.Register(ResetDaily);
    }

    /// <summary>
    /// 是否可消耗点数
    /// </summary>
    public bool CanSpend(int cost)
        => RunStateManager.Instance.run.actionPoint >= cost;
    /// <summary>
    /// 花费点数
    /// </summary>
    public bool Spend(int cost)
    {
        if (!CanSpend(cost)) return false;
        _runState.run.actionPoint -= cost;
        return true;
    }
    /// <summary>
    /// 返回当次搭话所需行动点
    /// </summary>
    /// <param name="formalTalkCount">已经搭过几次”，下一次是第 formalTalkCount+1 次</param>
    /// <returns></returns>
    public int GetTalkCost(int formalTalkCount)
    {
        int nextIndex = formalTalkCount + 1;
        if (nextIndex < 1) nextIndex = 1;
        if (nextIndex > 5) nextIndex = 5;
        return nextIndex;
    }
    /// <summary>
    /// 重置点数
    /// </summary>
    public void ResetDaily()
        => RunStateManager.Instance.run.actionPoint = 5;

    public void Dispose()
    {
        NextDayEvent.UnRegister(ResetDaily);
    }
}

