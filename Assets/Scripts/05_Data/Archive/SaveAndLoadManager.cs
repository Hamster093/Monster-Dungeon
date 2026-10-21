/****************************************************
    文件：GamePassEvent.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026-09-25 19:26:04
	功能：存档与读档管理器
*****************************************************/
using System.Collections.Generic;
using System.IO;
using System;
using Newtonsoft.Json;
using UnityEngine;

public class SaveAndLoadManager
{
    /// <summary>
    /// 根据存档键名生成对应的本地文件路径
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    private static string GetPath(string key)
        => Path.Combine(Application.persistentDataPath, key + ".json");
    /// <summary>
    /// 将指定的数据对象序列化并保存到本地
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="key"></param>
    /// <param name="data"></param>
    public static void Save<T>(string key, T data)
    {
        var json = JsonConvert.SerializeObject(data, Formatting.Indented);
        // 同目录临时文件写完后再替换；写入失败不能损坏原存档。
        var path = GetPath(key);
        var temporary = path + ".tmp";
        Directory.CreateDirectory(Application.persistentDataPath);
        File.WriteAllText(temporary, json);
        if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
        else File.Move(temporary, path);
    }
    /// <summary>
    /// 从本地读取指定键名的存档数据，并反序列化为目标对象。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="key"></param>
    /// <returns></returns>
    public static T Load<T>(string key) where T : new()
    {
        var path = GetPath(key);
        if (!File.Exists(path)) return new T();
        try { return JsonConvert.DeserializeObject<T>(File.ReadAllText(path)) ?? new T(); }
        catch (Exception e) { Debug.LogWarning("存档读取失败：" + e.Message); return new T(); }
    }
    /// <summary>
    /// 检查指定键名的存档文件是否已经存在于本地存储中。
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public static bool Exists(string key)
        => File.Exists(GetPath(key));
    /// <summary>
    /// 从本地删除指定键名的存档文件。
    /// </summary>
    /// <param name="key"></param>
    public static void Delete(string key)
    {
        var path = GetPath(key);
        if (File.Exists(path)) File.Delete(path);
    }

    public sealed class SlotInfo
    {
        public bool exists, valid;
        public int phase, day;
        public long savedUtc;
        public string summary;
    }
    public static bool IsValid(RunState state) => state != null && state.adventurers != null && state.adventurers.Count > 0;
    public static string SlotKey(int id)
    {
        if (id < 1 || id > 4) throw new ArgumentOutOfRangeException(nameof(id));
        return "manual_slot_" + id;
    }
    public static SlotInfo GetSlotInfo(int id)
    {
        string key = SlotKey(id);
        bool exists = Exists(key);
        var state = exists ? Load<RunState>(key) : null;
        bool valid = IsValid(state);
        long timestamp = valid ? state.savedUtc : 0;
        return new SlotInfo { exists = exists, valid = valid, savedUtc = timestamp, phase = valid ? state.currentPhase : 0, day = valid ? state.currentDay : 0,
            summary = !exists ? "空存档" : !valid ? "存档损坏，无法读取" :
            "阶段" + state.currentPhase + "　第" + state.currentDay + "天\n" +
            (timestamp > 0 ? new DateTime(timestamp, DateTimeKind.Utc).ToLocalTime().ToString("yyyy-MM-dd HH:mm") : "旧版存档") };
    }
    /// <summary>只在第一槽为空时复制旧自动档，保留源文件，重复启动不会覆盖手动档。</summary>
    public static void MigrateLegacy()
    {
        if (Exists(SlotKey(1)) || !Exists("run_state")) return;
        var old = Load<RunState>("run_state");
        if (IsValid(old)) Save(SlotKey(1), old);
    }
    public static bool ActivateSlot(int id)
    {
        var state = Load<RunState>(SlotKey(id));
        if (!IsValid(state)) return false;
        try { Save("run_state", state); return true; }
        catch (Exception e) { Debug.LogWarning(e.Message); return false; }
    }
    public static bool PrepareContinue()
    {
        MigrateLegacy();
        if (Exists("run_state") && IsValid(Load<RunState>("run_state"))) return true;
        int latest = 0; long stamp = -1;
        for (int i = 1; i <= 4; i++)
        { var info = GetSlotInfo(i); if (info.valid && info.savedUtc > stamp) { latest = i; stamp = info.savedUtc; } }
        return latest > 0 && ActivateSlot(latest);
    }
}
