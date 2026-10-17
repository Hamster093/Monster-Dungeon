/****************************************************
    文件：AudioManager.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/9 21:51:02
	功能：音频系统
*****************************************************/
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// 集中管理所有音频的 Key，防止手敲字符串出错
/// </summary>
public static class AudioKeys
{
    // 环境音 (BGM/AMB)
    public const string AMB_Dungeon = "AMB_001_Loop";
    public const string AMB_Reception = "AMB_002_Loop";
    public const string AMB_AbnormalDungeon = "AMB_003_Loop";

    // 音效 (SFX)
    public const string SFX_Click = "SFX_001";
    public const string SFX_Confirm = "SFX_002";
    public const string SFX_Back = "SFX_003";
    public const string SFX_Invalid = "SFX_004";
    public const string SFX_InfoGet = "SFX_005";
    public const string SFX_StatUp = "SFX_006";
    public const string SFX_StatDown = "SFX_007";
    public const string SFX_D20Roll = "SFX_008";
    public const string SFX_CheckSuccess = "SFX_009";
    public const string SFX_CheckFail = "SFX_010";
    public const string SFX_SevereFail = "SFX_011";
    public const string SFX_GoldUp = "SFX_012";
    public const string SFX_GoldDown = "SFX_013";
    public const string SFX_ItemUse = "SFX_014";
    public const string SFX_AltarStart = "SFX_015";
    public const string SFX_MonsterAdd = "SFX_016";
    public const string SFX_AltarProcess1 = "SFX_017";
    public const string SFX_AltarProcess2 = "SFX_018";
    public const string SFX_MonsterDrag = "SFX_019";
    public const string SFX_TrapTrigger = "SFX_020";
    public const string SFX_DailySettle = "SFX_021_Loop";
    public const string SFX_NewDay = "SFX_022";
}

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("音量控制 (0-1)")]
    [Range(0f, 1f)] public float masterVolume = 1f;
    [Range(0f, 1f)] public float musicVolume = 1f;
    [Range(0f, 1f)] public float sfxVolume = 1f;

    [Header("开关状态")]
    public bool isMusicOn = true;
    public bool isSfxOn = true;

    private AudioSource _bgmSource;
    private AudioSource _loopSfxSource; // 专门播放循环音效（如当日结算）
    private AudioSource[] _sfxPool;     // 普通短音效池
    private int _sfxPoolIndex = 0;
    private const int SFX_POOL_SIZE = 10;
    // 记录还没加载完时，玩家请求播放的 BGM
    private string _pendingBgmKey = "";

    // 缓存加载好的音频
    private Dictionary<string, AudioClip> _audioCache = new Dictionary<string, AudioClip>();
    private bool _isLoaded = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        if (Instance == null)
        {
            var go = new GameObject("AudioManager");
            DontDestroyOnLoad(go);
            go.AddComponent<AudioManager>();
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        EnsureAudioListener();

        // 初始化各个音源
        _bgmSource = gameObject.AddComponent<AudioSource>();
        _bgmSource.loop = true;
        _bgmSource.playOnAwake = false;

        _loopSfxSource = gameObject.AddComponent<AudioSource>();
        _loopSfxSource.loop = true;
        _loopSfxSource.playOnAwake = false;

        _sfxPool = new AudioSource[SFX_POOL_SIZE];
        for (int i = 0; i < SFX_POOL_SIZE; i++)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            _sfxPool[i] = source;
        }

        ApplyVolume();
        LoadAllAudiosAsync();
    }

    // ==========================================
    //  加载逻辑 (Addressables)
    // ==========================================
    /// <summary>
    /// 从 Addressables 批量加载所有标签为 "Audio" 的资源
    /// 请确保你的音频文件在 Addressables 中被打上了 "Audio" 标签
    /// </summary>
    private async void LoadAllAudiosAsync()
    {
        var handle = Addressables.LoadAssetsAsync<AudioClip>("Audio", null);
        await handle.Task;

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            foreach (var clip in handle.Result)
            {
                if (!_audioCache.ContainsKey(clip.name))
                    _audioCache.Add(clip.name, clip);
            }
            _isLoaded = true;
            Debug.Log($"[AudioManager] 成功加载 {_audioCache.Count} 个音频资源！");

            // ★ 关键修改：如果前面有人请求过 BGM，现在自动补播
            if (!string.IsNullOrEmpty(_pendingBgmKey))
            {
                PlayBGM(_pendingBgmKey);
                _pendingBgmKey = "";
            }
        }
        else
        {
            Debug.LogError("[AudioManager] 音频加载失败！请检查 Addressables 中是否有 'Audio' 标签。");
        }
    }

    // ==========================================
    //  播放接口
    // ==========================================

    /// <summary>播放背景音乐/环境音</summary>
    public void PlayBGM(string key)
    {
        if (!isMusicOn) return;

        // ★ 如果资源还没加载完，先记下来，等加载完再播
        if (!_isLoaded)
        {
            _pendingBgmKey = key;
            return;
        }

        if (_audioCache.TryGetValue(key, out var clip))
        {
            if (_bgmSource.clip == clip && _bgmSource.isPlaying) return;
            _bgmSource.clip = clip;
            _bgmSource.Play();
        }
        else
        {
            Debug.LogWarning($"[AudioManager] 找不到 BGM: {key}，请检查名字是否完全一致！");
        }
    }

    /// <summary>播放普通短音效</summary>
    public void PlaySFX(string key)
    {
        if (!_isLoaded || !isSfxOn || sfxVolume <= 0f) return;
        if (_audioCache.TryGetValue(key, out var clip))
        {
            var source = _sfxPool[_sfxPoolIndex];
            _sfxPoolIndex = (_sfxPoolIndex + 1) % _sfxPool.Length;
            source.PlayOneShot(clip);
        }
        else Debug.LogWarning($"[AudioManager] 找不到 SFX: {key}");
    }

    /// <summary>播放循环音效（如结算音效）</summary>
    public void PlayLoopSFX(string key)
    {
        if (!_isLoaded || !isSfxOn) return;
        if (_audioCache.TryGetValue(key, out var clip))
        {
            _loopSfxSource.clip = clip;
            _loopSfxSource.Play();
        }
    }

    /// <summary>停止循环音效</summary>
    public void StopLoopSFX()
    {
        _loopSfxSource.Stop();
    }

    // ==========================================
    //  快捷业务接口
    // ==========================================
    public void PlaySuccess() => PlaySFX(AudioKeys.SFX_CheckSuccess);
    public void PlayFail() => PlaySFX(AudioKeys.SFX_CheckFail);
    public void PlayGoldChange(bool isUp) => PlaySFX(isUp ? AudioKeys.SFX_GoldUp : AudioKeys.SFX_GoldDown);

    // ==========================================
    //  音量与静音控制
    // ==========================================
    public void SetMusicVolume(float vol)
    {
        musicVolume = Mathf.Clamp01(vol);
        ApplyVolume();
    }

    public void SetSfxVolume(float vol)
    {
        sfxVolume = Mathf.Clamp01(vol);
        ApplyVolume();
    }

    public void SetMasterVolume(float vol)
    {
        masterVolume = Mathf.Clamp01(vol);
        ApplyVolume();
    }

    public void ToggleMusic(bool state)
    {
        isMusicOn = state;
        if (!isMusicOn) _bgmSource.Pause();
        else _bgmSource.UnPause();
    }

    public void ToggleSfx(bool state)
    {
        isSfxOn = state;
        if (!isSfxOn) _loopSfxSource.Stop(); // 关音效时顺便停掉循环音效
    }

    private void ApplyVolume()
    {
        float bgmFinalVol = masterVolume * musicVolume;
        float sfxFinalVol = masterVolume * sfxVolume;

        _bgmSource.volume = bgmFinalVol;
        _loopSfxSource.volume = sfxFinalVol;

        foreach (var s in _sfxPool)
        {
            s.volume = sfxFinalVol;
        }
    }
    private void EnsureAudioListener()
    {
        // 找场景里所有的监听器
        var listeners = FindObjectsOfType<AudioListener>();

        // 如果发现有别的监听器（比如在相机上），把它们禁用，防止出现"有两个AudioListener"的警告
        foreach (var listener in listeners)
        {
            if (listener.gameObject != gameObject)
            {
                listener.enabled = false;
            }
        }

        // 检查自己身上有没有，没有就加一个
        if (GetComponent<AudioListener>() == null)
        {
            gameObject.AddComponent<AudioListener>();
        }
    }
}