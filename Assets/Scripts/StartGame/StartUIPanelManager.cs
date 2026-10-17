using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 负责所有面板的显隐切换、按钮回调绑定、设置面板的交互。
/// </summary>
public class UIPanelManager : MonoBehaviour
{
    [Header("面板根节点")]
    [SerializeField] private GameObject pnl_start_game;
    [SerializeField] private GameObject pnl_settings;
    [SerializeField] private GameObject pnl_load_game;

    [Header("主面板按钮")]
    [SerializeField] private Button btn_start_game;
    [SerializeField] private Button btn_continue_game;
    [SerializeField] private Button btn_settings;

    [Header("设置面板")]
    [SerializeField] private Slider sld_music;
    [SerializeField] private Slider sld_sfx;
    [SerializeField] private Toggle tgl_music;
    [SerializeField] private Toggle tgl_sfx;
    [SerializeField] private Dropdown dropdown_resolution;
    [SerializeField] private Toggle tgl_fullscreen;
    [SerializeField] private Button btn_read_save_in_settings; // 设置面板里的“读取存档”
    [SerializeField] private Button btn_save_game;             // 设置面板里的“保存游戏”
    [SerializeField] private Button btn_close_settings;

    [Header("存档面板")]
    [SerializeField] private Button btn_close_load_game;
    [SerializeField] private Button[] btn_save_slots;          // 5个存档槽位的按钮数组

    [Header("音乐引用")]
    public AudioClip mainMenuBGM;
    [Header("音效配置")]
    public AudioClip btnClickSfx; 

    private void Awake()
    {
        BindButtons();
        InitDropdown();
        CloseAllPanels(); // 初始化时确保状态正确
        pnl_start_game.SetActive(true); // 默认显示主面板
    }
    private void Start()
    {
        // 播放 BGM
        AudioManager.Instance.PlayBGM(AudioKeys.AMB_Dungeon);
    }
    /// <summary>播放按钮点击音效</summary>
    private void PlayClick()
    {
        if (btnClickSfx != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(AudioKeys.SFX_Click);
        }
    }


    /// <summary>
    /// 绑定所有按钮点击事件
    /// </summary>
    private void BindButtons()
    {
        // 主面板
        // ★ 修复了你原来这行最后多出来的一个逗号
        btn_close_settings.onClick.AddListener(() => {PlayClick(); TogglePanel(pnl_settings, false); });
        btn_start_game.onClick.AddListener(() => { PlayClick(); StartGame.Instance.StartNewGame(); });
        btn_continue_game.onClick.AddListener(() => { PlayClick(); StartGame.Instance.ContinueGame(); });
        btn_settings.onClick.AddListener(() => { PlayClick(); TogglePanel(pnl_settings, true); });

        // 存档面板关闭
        btn_close_load_game.onClick.AddListener(() => { PlayClick(); TogglePanel(pnl_load_game, false); });

        // 设置面板功能按钮
        if (btn_read_save_in_settings != null)
            btn_read_save_in_settings.onClick.AddListener(() => { PlayClick(); TogglePanel(pnl_load_game, true); });

        btn_save_game.onClick.AddListener(() => { PlayClick(); StartGame.Instance.SaveSystem(); });

        // 音量与音效滑块 (注：滑块通常不播点击音效，不然拖动时会响成一片)
        sld_music.onValueChanged.AddListener((val) => AudioManager.Instance.SetMusicVolume(val));
        sld_sfx.onValueChanged.AddListener((val) => {
            AudioManager.Instance.SetSfxVolume(val); // 补全了你原本只打Log没设音量的逻辑
        });

        // 音乐/音效开关
        tgl_music.onValueChanged.AddListener((state) => { PlayClick(); AudioManager.Instance.ToggleMusic(state); });
        tgl_sfx.onValueChanged.AddListener((state) => { PlayClick(); AudioManager.Instance.ToggleSfx(state); });
        // 全屏开关
        tgl_fullscreen.onValueChanged.AddListener((state) => {
            Screen.fullScreen = state;
            Debug.Log("全屏切换");

        });

        // 绑定 5 个存档槽位的读取回调
        for (int i = 0; i < btn_save_slots.Length; i++)
        {
            if (btn_save_slots[i] != null)
            {
                int slotId = i + 1; // 存档ID从1开始
                btn_save_slots[i].onClick.AddListener(() => {
                    StartGame.Instance.LoadSystem(slotId);
                    TogglePanel(pnl_load_game, false); // 读取后自动关闭面板
                });
            }
        }
    }

    /// <summary>
    /// 初始化分辨率下拉菜单
    /// </summary>
    private void InitDropdown()
    {
        dropdown_resolution.options.Clear();
        dropdown_resolution.options.Add(new Dropdown.OptionData("1920x1080"));
        dropdown_resolution.options.Add(new Dropdown.OptionData("1366x768"));
        dropdown_resolution.RefreshShownValue();

        dropdown_resolution.onValueChanged.AddListener((index) => {
            switch (index)
            {
                case 0:
                    Screen.SetResolution(1920, 1080, Screen.fullScreen);
                    Debug.Log("切换到1920,*1080");
                    break;
                case 1:
                    Screen.SetResolution(1366, 768, Screen.fullScreen);
                    Debug.Log("切换到1366*768");
                    break;
            }
        });
    }

    /// <summary>
    /// 面板显示/隐藏通用方法
    /// </summary>
    private void TogglePanel(GameObject panel, bool show)
    {
        panel.SetActive(show);
    }

    /// <summary>
    /// 关闭所有子面板，仅保留主面板
    /// </summary>
    private void CloseAllPanels()
    {
        pnl_settings.SetActive(false);
        pnl_load_game.SetActive(false);
    }
}