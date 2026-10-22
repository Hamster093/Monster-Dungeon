/****************************************************
    文件：SystemManager.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/9/25 0:56:27
	功能：系统管理器
*****************************************************/

public class SystemManager
{
    private static SystemManager _instance;
    public static SystemManager Instance => _instance ??= new SystemManager();

    public ConfigDatabase Config { get; private set; }
    public RunStateManager RunStateManager { get; private set; }
    public ScheduleSystem Schedule { get; private set; }
    public ActionPointSystem ActionPoint { get; private set; }
    public EconomySystem Economy { get; private set; }
    public VisitSystem VisitSystem { get; private set; }
    public DailyCommissionSystem DailyCommission { get; private set; }
    public NarrativeSystem Narrative { get; private set; }


    public bool IsInitialized { get; private set; }

    public void Init()
    {
        // 1. 配置
        ConfigDatabase.Load();
        Config = ConfigDatabase.Instance;

        // 2. 运行时状态
        RunStateManager = RunStateManager.Instance;
        if (SaveAndLoadManager.Exists("run_state")) RunStateManager.Load();
        else RunStateManager.NewRun();


        // 4. 业务系统
        Economy = new EconomySystem(RunStateManager);
        ActionPoint = new ActionPointSystem(RunStateManager);
        Schedule = new ScheduleSystem(RunStateManager, Config);
        VisitSystem = new VisitSystem(RunStateManager);
        DailyCommission = new DailyCommissionSystem();
        Narrative = new NarrativeSystem(this);

        // 5. 让各系统自己注册事件
        Schedule.Init();
        Schedule.RefreshScheduleData();
        ActionPoint.Init();
        Economy.Init();
        VisitSystem.Init();
        Narrative.Init();

        IsInitialized = true;
    }

    public void Dispose()
    {
        Schedule?.Dispose();
        ActionPoint?.Dispose();
        Economy?.Dispose();
        VisitSystem?.Dispose();

        IsInitialized = false;
    }

}
