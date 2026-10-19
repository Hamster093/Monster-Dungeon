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

    private void Awake()
    {
        SaveAndLoadManager.MigrateLegacy();
        pnl_start_game.SetActive(true);
        pnl_settings.SetActive(false);
        pnl_load_game.SetActive(false);
        btn_start_game.onClick.AddListener(() => StartGame.Instance.StartNewGame());
        btn_continue_game.onClick.AddListener(() => StartGame.Instance.ContinueGame());
        btn_continue_game.interactable = SaveAndLoadManager.PrepareContinue();
        btn_settings.onClick.AddListener(() => pnl_settings.GetComponent<SettingsPanel>().OnOpen());
    }

    private void Update()
    {
        if(!Input.GetKeyDown(KeyCode.Escape)||GuideHelpOverlay.BlocksInput)return;
        if(CGGalleryPanel.HandleEscape())return;
        // 标题场景没有UIManager时，仍沿用原面板的返回接口；一次ESC只返回一层。
        if(UIManager.Instance!=null)return;
        if(SaveSlotsPanel.HandleEscape())return;
        var settings=pnl_settings.GetComponent<SettingsPanel>();
        if(settings.IsOpen&&settings.gameObject.activeInHierarchy)settings.OnEscapePressed();
    }
}
