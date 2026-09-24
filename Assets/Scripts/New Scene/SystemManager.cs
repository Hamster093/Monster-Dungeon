/****************************************************
    文件：SystemManager.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/9/25 0:56:27
	功能：
*****************************************************/

public class SystemManager
{
    private static SystemManager _instance;
    public static SystemManager Instance => _instance ??= new SystemManager();

    public ConfigDatabase Config { get; private set; }
    public RunStateManager RunState { get; private set; }
    public ScheduleSystem Schedule { get; private set; }
    public ActionPointSystem ActionPoint { get; private set; }
    public ReceptionController Reception { get; private set; }
    public MemorySystem Memory { get; private set; }
    public EconomySystem Economy { get; private set; }

    private D20Service _d20;
    private AdventureResolver _resolver;

    public bool IsInitialized { get; private set; }

    public void Init()
    {
        // 1. 配置
        ConfigDatabase.Load();
        Config = ConfigDatabase.Instance;

        // 2. 运行时状态
        RunState = RunStateManager.Instance;
        if (SaveAndLoadManager.Exists("run_state")) RunState.Load();
        else RunState.NewRun();

        // 3. 底层服务
        _d20 = new D20Service();
        _resolver = new AdventureResolver(_d20);

        // 4. 业务系统
        Economy = new EconomySystem(RunState);
        ActionPoint = new ActionPointSystem(RunState);
        Memory = new MemorySystem(RunState, Config);
        Schedule = new ScheduleSystem(RunState, Config);
        Reception = new ReceptionController(RunState, Config, _resolver, Economy, ActionPoint, Memory);

        // 5. 让各系统自己注册事件
        Schedule.Init();
        ActionPoint.Init();
        Memory.Init();
        Economy.Init();
        Reception.Init();

        IsInitialized = true;
    }

    public void Dispose()
    {
        Schedule?.Dispose();
        ActionPoint?.Dispose();
        Memory?.Dispose();
        Economy?.Dispose();
        Reception?.Dispose();

        IsInitialized = false;
    }
}