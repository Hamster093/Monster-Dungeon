using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// 简易音频管理器（单例），控制背景音乐和音效。
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("全局音量控制 (0-1)")]
    [Range(0f, 1f)] public float masterVolume = 1f;
    [Range(0f, 1f)] public float musicVolume = 1f;
    [Range(0f, 1f)] public float sfxVolume = 1f;

    // 音乐开关
    [Header("音乐开关")]
    [SerializeField] private AudioSource bgmSource;
    private bool isMusicOn = true;

    // 音效开关
    [Header("音效开关")]
    private bool isSfxOn = true;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // DontDestroyOnLoad(gameObject); // 如果需要跨场景保留，请取消注释
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
        ApplyMasterVolume();
    }

    /// <summary>
    /// 设置背景音乐音量
    /// </summary>
    public void SetMusicVolume(float volume)
    {
        musicVolume = volume;
        ApplyMasterVolume();
        if (bgmSource != null) bgmSource.volume = masterVolume;
        Debug.Log($"音量改变为: {volume}");
    }

    /// <summary>
    /// 播放UI或交互音效
    /// </summary>
    public void PlaySfx(AudioClip clip)
    {
        if (clip == null) return;
        // 简易实现，实际项目建议用对象池
        AudioSource.PlayClipAtPoint(clip, Camera.main.transform.position);
        AudioSource tempSource = GetComponent<AudioSource>();
        if (tempSource == null) tempSource = gameObject.AddComponent<AudioSource>();
        tempSource.PlayOneShot(clip, masterVolume);
    }

    /// <summary>
    /// 切换音乐开关
    /// </summary>
    public void ToggleMusic(bool state)
    {
        isMusicOn = state;
        if (bgmSource != null)
        {
            bgmSource.mute = !isMusicOn;
            if (isMusicOn)
                Debug.Log("音乐取消静音");
            else
                Debug.Log("音乐静音");
        }
    }

    /// <summary>
    /// 切换音效开关
    /// </summary>
    public void ToggleSfx(bool state)
    {
        isSfxOn = state;
        if (isSfxOn)
            Debug.Log("音效取消静音");
        else
            Debug.Log("音效静音");
    }

    private void ApplyMasterVolume()
    {
        AudioListener.volume = masterVolume;
    }
}