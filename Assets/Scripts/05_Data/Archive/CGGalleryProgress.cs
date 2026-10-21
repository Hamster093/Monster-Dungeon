using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>CG解锁属于全局记录，不随新局、手动槽位或自动档覆盖。</summary>
public static class CGGalleryProgress
{
    public sealed class Entry
    {
        public readonly string code,name,address;
        public Entry(string code,string name){this.code=code;this.name=name;address="Art/Endings/"+code;}
    }
    private sealed class Progress
    {
        public int version=1;
        public List<string> unlocked=new List<string>();
    }
    private static readonly Entry[] catalog={
        new Entry("MY","Myth"),new Entry("FE","Ferdia"),new Entry("DW","Clot·Lach·Atros"),
        new Entry("SA","Sarah"),new Entry("AU","Audrey"),new Entry("DA","Da"),new Entry("UN","Nobody")
    };
    private static Progress progress;
    private static string loadedPath;
    public static IReadOnlyList<Entry> Catalog=>catalog;
    public static string StoragePath=>Path.Combine(Application.persistentDataPath,"cg_gallery_progress.json");

    public static bool IsUnlocked(string address)
    {EnsureLoaded();return progress.unlocked.Contains(address);}
    public static Entry[] GetUnlocked()
    {EnsureLoaded();return catalog.Where(entry=>progress.unlocked.Contains(entry.address)).ToArray();}

    /// <summary>只由实际CG展示钩子调用；选人和剧情预览不调用此入口。</summary>
    public static bool RecordShown(string address)
    {
        EnsureLoaded();
        if(!catalog.Any(entry=>entry.address==address)||progress.unlocked.Contains(address))return false;
        var next=new Progress{unlocked=new List<string>(progress.unlocked)};next.unlocked.Add(address);
        return Write(next);
    }

    /// <summary>只迁移明确完成的接任档；选人、检定、剧情和CG转场中的档案均不解锁。</summary>
    public static int MigrateCompletedSaves()
    {
        EnsureLoaded();var next=new Progress{unlocked=new List<string>(progress.unlocked)};
        var keys=new List<string>{"run_state"};for(int i=1;i<=4;i++)keys.Add(SaveAndLoadManager.SlotKey(i));
        foreach(string key in keys)
        {
            if(!SaveAndLoadManager.Exists(key))continue;
            var run=SaveAndLoadManager.Load<RunState>(key);
            string address=CompletedAddress(run);
            if(!string.IsNullOrEmpty(address)&&!next.unlocked.Contains(address))next.unlocked.Add(address);
        }
        int added=next.unlocked.Count-progress.unlocked.Count;
        return added>0&&Write(next)?added:0;
    }

    // 独立纯读取接口供迁移检查使用，不推进剧情，不改变原存档。
    public static string CompletedAddress(RunState run)
    {
        if(!SaveAndLoadManager.IsValid(run)||string.IsNullOrEmpty(run.narrativeSave))return null;
        try
        {
            var saved=JsonConvert.DeserializeObject<NarrativeSystem.SaveData>(run.narrativeSave);
            if(saved==null||saved.successionFailed||string.IsNullOrEmpty(saved.ending)||!saved.ending.StartsWith("succession:",StringComparison.Ordinal))return null;
            bool completed=saved.stage=="ending_complete"||(saved.uiVersion<7&&saved.stage=="ended");
            if(!completed)return null;
            string code=saved.ending.Substring("succession:".Length);
            return catalog.FirstOrDefault(entry=>entry.code==code)?.address;
        }
        catch(JsonException){return null;}
    }

    public static void Reload(){progress=null;EnsureLoaded();}
    private static void EnsureLoaded()
    {
        string path=StoragePath;if(progress!=null&&loadedPath==path)return;
        loadedPath=path;progress=Read(path)??Read(path+".bak")??new Progress();
        progress.unlocked=(progress.unlocked??new List<string>()).Where(address=>catalog.Any(entry=>entry.address==address)).Distinct().ToList();
    }
    private static Progress Read(string path)
    {
        if(!File.Exists(path))return null;
        try{return JsonConvert.DeserializeObject<Progress>(File.ReadAllText(path));}
        catch(Exception error)when(error is IOException||error is UnauthorizedAccessException||error is JsonException)
        {Debug.LogWarning("CG鉴赏记录读取失败："+error.Message);return null;}
    }
    private static bool Write(Progress next)
    {
        string path=StoragePath,temporary=path+".tmp";
        try
        {
            Directory.CreateDirectory(Application.persistentDataPath);
            File.WriteAllText(temporary,JsonConvert.SerializeObject(next,Formatting.Indented));
            // 同目录写完再替换，失败时保留原文件；下一次展示仍可重试登记。
            if(File.Exists(path))File.Replace(temporary,path,path+".bak");else File.Move(temporary,path);
            progress=next;loadedPath=path;return true;
        }
        catch(Exception error)when(error is IOException||error is UnauthorizedAccessException)
        {Debug.LogWarning("CG鉴赏记录保存失败："+error.Message);return false;}
    }
}
