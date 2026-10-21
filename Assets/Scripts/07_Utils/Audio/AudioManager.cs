using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Newtonsoft.Json;

/// <summary>沿用原管理器：音乐交叉淡化、环境音和短音效分开；映射集中在Audio/catalog。</summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;
    [Range(0,1)]public float masterVolume=1,musicVolume=1,sfxVolume=1;
    [SerializeField]AudioSource bgmSource;
    [Serializable]public sealed class Context{public string music,ambient;public bool loop=true;public float ambientGain=1;}
    [Serializable]public sealed class Cue{public string clip;public float gain=1,pitch=1;public int priority=10;}
    [Serializable]public sealed class Catalog{public Dictionary<string,Context> contexts;public Dictionary<string,Cue> cues;public Dictionary<string,string> nodes;}
    public Catalog Mapping{get;private set;}
    public string BaseContext{get;private set;}="title";
    public string MusicId{get;private set;}
    public string AmbientId{get;private set;}
    public string LastCue{get;private set;}
    public int PlayedCueCount{get;private set;}
    public bool MusicEnabled=>musicOn;
    public bool SfxEnabled=>sfxOn;
    readonly Dictionary<string,AudioClip> clips=new Dictionary<string,AudioClip>();
    readonly Dictionary<string,float> positions=new Dictionary<string,float>();
    readonly HashSet<string> played=new HashSet<string>();
    readonly Dictionary<string,Cue> pending=new Dictionary<string,Cue>();
    AudioSource second,ambient,sfx,active;Coroutine fade,ambientFade;
    bool musicOn=true,sfxOn=true,archive;float mainGain=1,secondGain,ambientGain,ambientBlend=1;string applied;int cashDelta;
    void Awake()
    {
        if(Instance&&Instance!=this){Destroy(gameObject);return;}
        Instance=this;DontDestroyOnLoad(gameObject);
        var json=Resources.Load<TextAsset>("Audio/catalog");Mapping=json?JsonConvert.DeserializeObject<Catalog>(json.text):new Catalog();
        if(!bgmSource)bgmSource=gameObject.AddComponent<AudioSource>();
        bgmSource.Stop();Setup(bgmSource);second=NewSource();ambient=NewSource();sfx=NewSource();active=bgmSource;
        musicVolume=PlayerPrefs.GetFloat("settings.music.volume",1f);sfxVolume=PlayerPrefs.GetFloat("settings.sfx.volume",1f);
        musicOn=PlayerPrefs.GetInt("settings.music.on",1)==1;sfxOn=PlayerPrefs.GetInt("settings.sfx.on",1)==1;
        SceneManager.sceneLoaded+=SceneLoaded;SetContext(SceneManager.GetActiveScene().name=="StartGame"?"title":"preparation");
    }
    void OnDestroy(){SceneManager.sceneLoaded-=SceneLoaded;if(Instance==this)Instance=null;}
    AudioSource NewSource(){var source=gameObject.AddComponent<AudioSource>();Setup(source);return source;}
    static void Setup(AudioSource source){source.playOnAwake=false;source.spatialBlend=0;source.volume=0;source.loop=false;}
    void SceneLoaded(Scene scene,LoadSceneMode mode){pending.Clear();played.Clear();positions.Clear();archive=false;SetContext(scene.name=="StartGame"?"title":"preparation");}
    AudioClip Clip(string id){if(string.IsNullOrEmpty(id))return null;if(!clips.TryGetValue(id,out var clip)){clip=Resources.Load<AudioClip>("Audio/"+id);clips[id]=clip;}return clip;}
    public void SetContext(string context)
    {
        if(Mapping?.contexts==null||!Mapping.contexts.ContainsKey(context))return;
        BaseContext=context;ApplyContext();
    }
    void ApplyContext()
    {
        bool danger=BaseContext=="uneasy"||BaseContext=="danger"||BaseContext=="ending"||BaseContext=="stage"||BaseContext=="credits";
        string key=archive&&!danger?"archive":BaseContext;
        if(applied==key)return;applied=key;
        var context=Mapping.contexts[key];var origin=Mapping.contexts[BaseContext];
        ChangeMusic(context.music,context.loop);
        string amb=key=="archive"?origin.ambient:context.ambient;
        ambientGain=key=="archive"?origin.ambientGain:context.ambientGain;
        if(AmbientId!=amb){AmbientId=amb;if(ambientFade!=null)StopCoroutine(ambientFade);ambientFade=StartCoroutine(ChangeAmbient(amb));}
        Volumes();
    }
    void LateUpdate()
    {
        if(cashDelta!=0){PlayCue(cashDelta>0?"coins_in":"coins_out");cashDelta=0;}
        var ui=UIManager.Instance;var panel=ui?.Get<CharacterArchivePanel>();
        bool shown=panel&&panel.IsOpen&&panel.gameObject.activeInHierarchy&&ui.CurrentLeftPanelName==nameof(CharacterArchivePanel)&&!PlayerBuildPanel.IsShowing;
        if(shown!=archive){archive=shown;ApplyContext();}
        // 同帧业务音优先于通用点击；鼠标和键盘回调不会各响一次。
        bool business=pending.Values.Any(c=>c.priority>=20);
        var chosen=pending.OrderByDescending(p=>p.Value.priority).Where(p=>!business||p.Value.priority>=20).Take(business?2:1).ToArray();
        pending.Clear();foreach(var item in chosen){var clip=Clip(item.Value.clip);if(!clip||!sfxOn)continue;sfx.pitch=item.Value.pitch;sfx.PlayOneShot(clip,item.Value.gain);LastCue=item.Key;PlayedCueCount++;}
    }
    void ChangeMusic(string id,bool loop)
    {
        // 同曲目进入片尾时允许由单次改为循环；已播放完的终局曲也能继续承接致谢。
        if(MusicId==id){active.loop=loop;if(!active.isPlaying&&active.clip)active.Play();return;}MusicId=id;
        if(fade!=null)StopCoroutine(fade);
        var outgoing=active;if(outgoing.clip)positions[outgoing.clip.name]=outgoing.time;
        var incoming=active==bgmSource?second:bgmSource;
        incoming.Stop();incoming.clip=Clip(id);incoming.loop=loop;
        if(incoming.clip){if(loop&&positions.TryGetValue(id,out var time))incoming.time=Mathf.Min(time,Mathf.Max(0,incoming.clip.length-.1f));incoming.Play();}
        active=incoming;fade=StartCoroutine(Crossfade(outgoing,incoming));
    }
    IEnumerator Crossfade(AudioSource outgoing,AudioSource incoming)
    {
        float old=outgoing==bgmSource?mainGain:secondGain;SetGain(incoming,0);
        for(float t=0;t<.8f;t+=Time.unscaledDeltaTime){float k=Mathf.Clamp01(t/.8f);SetGain(outgoing,old*(1-k));SetGain(incoming,k);Volumes();yield return null;}
        SetGain(outgoing,0);SetGain(incoming,1);outgoing.Stop();Volumes();fade=null;
    }
    void SetGain(AudioSource source,float value){if(source==bgmSource)mainGain=value;else secondGain=value;}
    IEnumerator ChangeAmbient(string id)
    {
        float initial=ambientBlend;
        for(float t=0;t<.35f;t+=Time.unscaledDeltaTime){ambientBlend=initial*(1-t/.35f);Volumes();yield return null;}
        ambientBlend=0;ambient.Stop();ambient.clip=Clip(id);ambient.loop=true;
        if(ambient.clip){ambient.Play();for(float t=0;t<.35f;t+=Time.unscaledDeltaTime){ambientBlend=t/.35f;Volumes();yield return null;}}
        ambientBlend=1;Volumes();ambientFade=null;
    }
    public void PlayCue(string key,string once=null)
    {
        if(!sfxOn||Mapping?.cues==null||!Mapping.cues.TryGetValue(key,out var cue))return;
        if(cue.priority>=20&&SystemManager.Instance?.Narrative?.IsRestoring==true)return;
        if(once!=null&&!played.Add(key+":"+once))return;
        pending[key]=cue;
    }
    public void CashChanged(int delta){if(SystemManager.Instance?.Narrative?.IsRestoring!=true)cashDelta+=delta;}
    public void ClearTransient(){pending.Clear();played.Clear();cashDelta=0;}
    // 保留原接口，短音效绝不再从背景音乐音源上播放。
    public void PlaySfx(AudioClip clip){if(clip&&sfxOn){sfx.pitch=1;sfx.PlayOneShot(clip);}}
    public void SetMusicVolume(float value){musicVolume=Mathf.Clamp01(value);Volumes();}
    public void SetSfxVolume(float value){sfxVolume=Mathf.Clamp01(value);Volumes();}
    public void ToggleMusic(bool state){musicOn=state;Volumes();}
    public void ToggleSfx(bool state){sfxOn=state;if(!state){pending.Clear();sfx.Stop();}Volumes();}
    void Volumes()
    {
        AudioListener.volume=masterVolume;
        if(bgmSource){bgmSource.volume=.6f*musicVolume*mainGain;bgmSource.mute=!musicOn;}
        if(second){second.volume=.6f*musicVolume*secondGain;second.mute=!musicOn;}
        if(ambient){ambient.volume=.15f*sfxVolume*ambientGain*ambientBlend;ambient.mute=!sfxOn;}
        if(sfx){sfx.volume=.65f*sfxVolume;sfx.mute=!sfxOn;}
    }
}
