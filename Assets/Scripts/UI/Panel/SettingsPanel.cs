/****************************************************
    文件：SettingsPanel.cs
    作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026-10-13
    功能：设置面板（音频 / 画面 / 存档），继承 PanelBase，可跨场景复用。
         通过事件把「读档 / 存档」请求抛给外部（如 UIPanelManager）处理，
         面板本身不依赖任何具体的 GameManager。
*****************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class SettingsPanel : PanelBase
{
    public override bool IsModal => true;
    // ============================================================
    //  配置项 Key
    // ============================================================
    private const string KeyMusicVolume = "settings.music.volume";
    private const string KeySfxVolume = "settings.sfx.volume";
    private const string KeyMusicOn = "settings.music.on";
    private const string KeySfxOn = "settings.sfx.on";
    private const string KeyFullscreen = "settings.fullscreen";
    private const string KeyResolution = "settings.resolution.index";

    private const float DefaultVolume = 1f;
    private const bool DefaultOn = true;

    // 窗口的设计基准；全屏使用所在显示器的真实像素，不能把720p画面放大。
    private static readonly Vector2Int FixedResolution = new Vector2Int(1920, 1080);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplyStartupDisplay()
    {
#if !UNITY_EDITOR
        // 主菜单出现前就纠正旧版保存的低分辨率，不要求玩家先打开设置。
        ApplyDisplayMode(PlayerPrefs.GetInt(KeyFullscreen, 1) == 1);
#endif
    }

    private static Vector2Int FullscreenResolution()
    {
        var display = Screen.mainWindowDisplayInfo;
        return display.width > 0 && display.height > 0
            ? new Vector2Int(display.width, display.height) : FixedResolution;
    }

    private static void ApplyDisplayMode(bool fullscreen)
    {
        var resolution = fullscreen ? FullscreenResolution() : FixedResolution;
        Screen.SetResolution(resolution.x, resolution.y,
            fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
    }

    private void RefreshResolutionLabel(bool fullscreen)
    {
        if (dropdown_resolution == null || dropdown_resolution.options.Count == 0) return;
        var resolution = fullscreen ? FullscreenResolution() : FixedResolution;
        if (_resolutions.Count > 0) _resolutions[0] = resolution;
        dropdown_resolution.options[0].text = resolution.x + "x" + resolution.y;
        dropdown_resolution.RefreshShownValue();
    }

    [Header("音频")]
    [SerializeField] private Slider sld_music;
    [SerializeField] private Slider sld_sfx;
    [SerializeField] private Toggle tgl_music;
    [SerializeField] private Toggle tgl_sfx;

    [Header("画面")]
    [SerializeField] private Dropdown dropdown_resolution;
    [SerializeField] private Toggle tgl_fullscreen;

    [Header("存档")]
    [SerializeField] private Button btn_read_save;   // 读取存档
    [SerializeField] private Button btn_save_game;   // 保存游戏

    [Header("关闭")]
    [SerializeField] private Button btn_close;

    // ============================================================
    //  对外事件：外部（UIPanelManager 等）订阅后自行处理
    // ============================================================
    /// <summary>点击「读取存档」，外部负责打开存档面板</summary>
    public event Action OnReadSaveClicked;
    /// <summary>点击「保存游戏」</summary>
    public event Action OnSaveGameClicked;

    // ============================================================
    //  内部状态
    // ============================================================
    private readonly List<Vector2Int> _resolutions = new List<Vector2Int>();
    private bool _bindsDone;
    /// <summary>代码回填 UI 时置 true，屏蔽 onValueChanged 回调，避免误触发</summary>
    private bool _suppressEvents;

    // ============================================================
    //  生命周期
    // ============================================================
    public override void OnInit()
    {
        EnsureBinds();
        ApplyFromPrefs();
    }

    public override void OnOpen(object data = null)
    {
        EnsureBinds();
        SyncUIToRuntime();  // 每次打开时刷新一次显示
        if (btn_save_game) btn_save_game.interactable = SystemManager.Instance.IsInitialized;
        base.OnOpen(data);
    }

    public override void OnClose()
    { if (UIManager.Instance != null && UIManager.Instance.OpenStack.Contains(this)) UIManager.Instance.Close(this); else base.OnClose(); }

    public override bool OnEscapePressed()
    {
        OnClose();
        return true;
    }

    // ============================================================
    //  绑定
    // ============================================================
    private void EnsureBinds()
    {
        if (_bindsDone) return;
        _bindsDone = true;

        if (btn_close != null)
            btn_close.onClick.AddListener(OnClose);

        if (btn_read_save != null)
            btn_read_save.onClick.AddListener(() =>
            {
                if (OnReadSaveClicked != null) OnReadSaveClicked.Invoke();
                else SaveSlotsPanel.Show(false);
            });

        if (btn_save_game != null)
            btn_save_game.onClick.AddListener(() =>
            {
                if (OnSaveGameClicked != null) OnSaveGameClicked.Invoke();
                else SaveSlotsPanel.Show(true);
            });

        if (sld_music != null) sld_music.onValueChanged.AddListener(OnMusicVolumeChanged);
        if (sld_sfx != null) sld_sfx.onValueChanged.AddListener(OnSfxVolumeChanged);

        if (tgl_music != null) tgl_music.onValueChanged.AddListener(OnMusicToggleChanged);
        if (tgl_sfx != null) tgl_sfx.onValueChanged.AddListener(OnSfxToggleChanged);

        if (tgl_fullscreen != null) tgl_fullscreen.onValueChanged.AddListener(OnFullscreenChanged);

        InitResolutionDropdown();
    }

    // ============================================================
    //  分辨率下拉框（仅保留一个固定分辨率）
    // ============================================================
    private void InitResolutionDropdown()
    {
        if (dropdown_resolution == null) return;

        _resolutions.Clear();

        //  只放一个写死的分辨率
        _resolutions.Add(FixedResolution);

        var options = new List<Dropdown.OptionData>
        {
            new Dropdown.OptionData($"{FixedResolution.x}x{FixedResolution.y}")
        };

        dropdown_resolution.ClearOptions();
        dropdown_resolution.AddOptions(options);
        dropdown_resolution.value = 0;
        dropdown_resolution.onValueChanged.AddListener(OnResolutionChanged);
        dropdown_resolution.RefreshShownValue();
    }

    private int FindCurrentResolutionIndex()
    {
        // 现在只有一个选项，直接返回 0
        return 0;
    }

    // ============================================================
    //  从 PlayerPrefs 读取并应用
    // ============================================================
    private void ApplyFromPrefs()
    {
        _suppressEvents = true;

        float musicVol = PlayerPrefs.GetFloat(KeyMusicVolume, DefaultVolume);
        float sfxVol = PlayerPrefs.GetFloat(KeySfxVolume, DefaultVolume);
        bool musicOn = PlayerPrefs.GetInt(KeyMusicOn, DefaultOn ? 1 : 0) == 1;
        bool sfxOn = PlayerPrefs.GetInt(KeySfxOn, DefaultOn ? 1 : 0) == 1;
        bool fullscreen = PlayerPrefs.GetInt(KeyFullscreen, Screen.fullScreen ? 1 : 0) == 1;
        // 分辨率索引现在固定为 0，读取仅为了兼容旧存档
        int resIndex = PlayerPrefs.GetInt(KeyResolution, 0);

        if (sld_music != null) sld_music.value = musicVol;
        if (sld_sfx != null) sld_sfx.value = sfxVol;
        if (tgl_music != null) tgl_music.isOn = musicOn;
        if (tgl_sfx != null) tgl_sfx.isOn = sfxOn;
        if (tgl_fullscreen != null) tgl_fullscreen.isOn = fullscreen;
        RefreshFullscreenGraphic(fullscreen);

        // 应用到运行时
        AudioManager.Instance?.SetMusicVolume(musicVol);
        AudioManager.Instance?.SetSfxVolume(sfxVol);
        AudioManager.Instance?.ToggleMusic(musicOn);
        AudioManager.Instance?.ToggleSfx(sfxOn);
        ApplyDisplayMode(fullscreen);
        RefreshResolutionLabel(fullscreen);

        // 分辨率：只有一个选项，始终选中 0
        if (dropdown_resolution != null)
        {
            dropdown_resolution.value = 0;
            dropdown_resolution.RefreshShownValue();
        }

        _suppressEvents = false;
    }

    /// <summary>把当前运行时状态回填到 UI（不写 PlayerPrefs）</summary>
    private void SyncUIToRuntime()
    {
        _suppressEvents = true;

        if (dropdown_resolution != null)
        {
            dropdown_resolution.value = 0;
            dropdown_resolution.RefreshShownValue();
        }

        if (tgl_fullscreen != null) tgl_fullscreen.isOn = Screen.fullScreen;
        RefreshFullscreenGraphic(Screen.fullScreen);
        RefreshResolutionLabel(Screen.fullScreen);

        _suppressEvents = false;
    }

    // ============================================================
    //  回调：音频
    // ============================================================
    private void OnMusicVolumeChanged(float val)
    {
        if (_suppressEvents) return;

        PlayerPrefs.SetFloat(KeyMusicVolume, val);
        AudioManager.Instance?.SetMusicVolume(val);
    }

    private void OnSfxVolumeChanged(float val)
    {
        if (_suppressEvents) return;

        PlayerPrefs.SetFloat(KeySfxVolume, val);
        Debug.Log($"音效音量改变为: {val}");
        AudioManager.Instance?.SetSfxVolume(val);
    }

    private void OnMusicToggleChanged(bool state)
    {
        if (_suppressEvents) return;

        PlayerPrefs.SetInt(KeyMusicOn, state ? 1 : 0);
        AudioManager.Instance?.ToggleMusic(state);
    }

    private void OnSfxToggleChanged(bool state)
    {
        if (_suppressEvents) return;

        PlayerPrefs.SetInt(KeySfxOn, state ? 1 : 0);
        AudioManager.Instance?.ToggleSfx(state);
    }

    // ============================================================
    //  回调：画面
    // ============================================================
    private void OnFullscreenChanged(bool state)
    {
        if (_suppressEvents) return;

        PlayerPrefs.SetInt(KeyFullscreen, state ? 1 : 0);
        ApplyDisplayMode(state);
        RefreshResolutionLabel(state);
        RefreshFullscreenGraphic(state);
    }

    private void OnResolutionChanged(int index)
    {
        if (_suppressEvents) return;
        if (index < 0 || index >= _resolutions.Count) return;

        // 现在只有一个选项，实际上只会是 index = 0
        ApplyDisplayMode(Screen.fullScreen);
        RefreshResolutionLabel(Screen.fullScreen);
        PlayerPrefs.SetInt(KeyResolution, index);
    }

    // ============================================================
    //  对外：手动刷新（外部改了设置后可调用）
    // ============================================================
    public void RefreshSettings() => ApplyFromPrefs();
    private void RefreshFullscreenGraphic(bool state)
    {
        var image = tgl_fullscreen != null ? tgl_fullscreen.targetGraphic as Image : null;
        if (image != null && image.name == "FullscreenChoice")
            image.sprite = Resources.Load<Sprite>(state ? "Art/Settings/-_0010_√_选中全屏" : "Art/Settings/-_0009_√_选中窗口");
    }
}
