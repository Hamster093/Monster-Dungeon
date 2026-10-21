/****************************************************
    文件：MemorySystem.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/9/25 0:26:49
	功能：记忆系统
*****************************************************/

using System.Linq;

public class MemorySystem
{
    private readonly RunStateManager _runState;
    private readonly ConfigDatabase _config;

    public MemorySystem(RunStateManager runState, ConfigDatabase config)
    {
        _runState = runState;
        _config = config;
    }

    public void Init()
    {
        // 需要时注册监听
    }
    public void Dispose() { }
    /// <summary>
    /// 写入记忆
    /// </summary>
    /// <param name="adventurerId"></param>
    /// <param name="trigger"></param>
    public void Write(int adventurerId, MemoryTrigger trigger)
    {
        if (!_runState.run.adventurers.TryGetValue(adventurerId, out var adv)) return;

        MemoryConfig mem = null;
        foreach (var m in _config.Memories.Values)
        {
            if (m.trigger == trigger) { mem = m; break; }
        }
        if (mem == null) return;

        adv.memoryTimeline.Add(mem.text);

        // 更新报复等级
        if (mem.revengeLevel != RevengeLevel.None)
        {
            if ((int)mem.revengeLevel > (int)adv.revenge)
                adv.revenge = mem.revengeLevel;

            if (adv.revenge != RevengeLevel.None)
                adv.inRevengeWindow = true;
        }
    }
}

