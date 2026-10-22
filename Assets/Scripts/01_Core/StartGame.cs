using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 游戏核心逻辑控制器。
/// </summary>
public class StartGame : MonoBehaviour
{
    public static StartGame Instance;

    [Header("目标游戏场景名称")]
    [SerializeField] private string targetGameSceneName = "";
    [SerializeField] private Button _galleryButton;
    [SerializeField] private Button _continueButton;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        CGGalleryProgress.MigrateCompletedSaves();
        if(_galleryButton)_galleryButton.onClick.AddListener(OpenCGGallery);
    }
    public void OpenCGGallery()=>CGGalleryPanel.Show();
    private void Start()
    {
        // 常态/悬停使用原画分件，继续按钮无有效存档时只降低透明度。
        if(_continueButton&&_continueButton.targetGraphic)
        {var color=_continueButton.targetGraphic.color;color.a=_continueButton.interactable?1:.42f;_continueButton.targetGraphic.color=color;}
    }
    private void OnDestroy()
    {
        if(_galleryButton)_galleryButton.onClick.RemoveListener(OpenCGGallery);
        if(Instance==this)Instance=null;
    }

    /// <summary>
    /// 跳转到目标游戏场景（在 Inspector 赋值）
    /// </summary>
    public void StartNewGame()
    {
        if (!string.IsNullOrEmpty(targetGameSceneName))
        {
            SaveAndLoadManager.Delete("run_state");
            MonsterStorageModel.Instance.Clear();
            DialogueHistoryModel.Instance?.Clear();
            // 黑幕跨场景保持，等类型选择就绪后再淡亮，避免先闪出整备棋盘。
            PresentationEffects.PrepareOpening();
            SceneManager.LoadScene(targetGameSceneName);
        }
        else
            Debug.LogWarning("Game: 未设置目标游戏场景名称！");
    }

    /// <summary>
    /// 继续游戏（跳转至相同的游戏场景）
    /// </summary>
    public void ContinueGame()
    {
        if (!string.IsNullOrEmpty(targetGameSceneName) && SaveAndLoadManager.PrepareContinue()) SceneManager.LoadScene(targetGameSceneName);
    }

    /// <summary>
    /// 保存游戏 (单例接口)
    /// </summary>
    public void SaveSystem()
    {
        if (SystemManager.Instance.IsInitialized) SystemManager.Instance.RunStateManager.Save();
    }

    /// <summary>
    /// 读取存档 (单例接口)
    /// </summary>
    public void LoadSystem(int saveSlotId)
    {
        if (SaveAndLoadManager.ActivateSlot(saveSlotId)) SceneManager.LoadScene(targetGameSceneName);
    }
    public void QuitGame() { Application.Quit(); }
}
