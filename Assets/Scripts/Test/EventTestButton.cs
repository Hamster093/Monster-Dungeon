/****************************************************
    文件：EventTestButton.cs
    作者：DADI
	功能：测试按钮——按字符串触发对应事件类的 Trigger。
         例：eventTypeName 填 "NextDayEvent"，
             点按钮即执行 NextDayEvent.Trigger(...)
*****************************************************/

using System;
using System.Reflection;
using UnityEngine;

public class EventTestButton : MonoBehaviour
{
    [Header("事件类名（可写短名或完整命名空间）")]
    [Tooltip("例：NextDayEvent 或 MyGame.Events.NextDayEvent")]
    public string eventTypeName = "NextDayEvent";

    [Header("参数（会自动转成事件的 TArg 类型；无参事件留空）")]
    public string argument = "";

    [Header("程序集过滤（留空 = 搜所有程序集；一般填 Assembly-CSharp）")]
    public string assemblyFilter = "Assembly-CSharp";

    /// <summary>绑到 Button.onClick 上即可</summary>
    public void Trigger()
    {
        if (string.IsNullOrWhiteSpace(eventTypeName))
        {
            Debug.LogError("[EventTestButton] 事件类名为空");
            return;
        }

        var type = FindType(eventTypeName);
        if (type == null)
        {
            Debug.LogError($"[EventTestButton] 找不到类型：{eventTypeName}");
            return;
        }

        // 找静态 Trigger（继承自 Event<T,TArg>，需要 FlattenHierarchy）
        var trigger = type.GetMethod(
            "Trigger",
            BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);

        if (trigger == null)
        {
            Debug.LogError($"[EventTestButton] {type.Name} 没有找到静态 Trigger 方法");
            return;
        }

        var ps = trigger.GetParameters();

        object[] args;
        if (ps.Length == 0)
        {
            args = Array.Empty<object>();
        }
        else if (ps.Length == 1)
        {
            var paramType = ps[0].ParameterType;
            if (!TryConvert(argument, paramType, out var val))
            {
                Debug.LogError(
                    $"[EventTestButton] 参数 '{argument}' 无法转换为 {paramType.Name}（{type.Name}）");
                return;
            }
            args = new object[] { val };
        }
        else
        {
            Debug.LogError($"[EventTestButton] Trigger 参数数量不支持：{ps.Length}");
            return;
        }

        try
        {
            trigger.Invoke(null, args);
            Debug.Log($"[EventTestButton] 已触发 {type.Name}.Trigger({string.Join(", ", args)})");
        }
        catch (TargetInvocationException e)
        {
            Debug.LogError(
                $"[EventTestButton] 触发 {type.Name} 时异常：{e.InnerException?.Message ?? e.Message}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[EventTestButton] 触发 {type.Name} 失败：{e.Message}");
        }
    }

    // ── 查找类型 ────────────────────────────────
    private Type FindType(string name)
    {
        // 先按完整名快查
        var t = Type.GetType(name);
        if (t != null) return t;

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!string.IsNullOrEmpty(assemblyFilter)
                && asm.GetName().Name != assemblyFilter) continue;

            Type[] types;
            try { types = asm.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = Array.FindAll(ex.Types, x => x != null); }
            catch { continue; }

            foreach (var type in types)
            {
                if (type == null) continue;
                if (type.Name == name || type.FullName == name) return type;
            }
        }
        return null;
    }

    // ── 参数转换 ────────────────────────────────
    private bool TryConvert(string raw, Type target, out object result)
    {
        result = null;

        if (target == typeof(string)) { result = raw ?? string.Empty; return true; }

        if (target == typeof(int))
        {
            if (int.TryParse(raw, out var v)) { result = v; return true; }
            return false;
        }
        if (target == typeof(long))
        {
            if (long.TryParse(raw, out var v)) { result = v; return true; }
            return false;
        }
        if (target == typeof(float))
        {
            if (float.TryParse(raw, out var v)) { result = v; return true; }
            return false;
        }
        if (target == typeof(double))
        {
            if (double.TryParse(raw, out var v)) { result = v; return true; }
            return false;
        }
        if (target == typeof(bool))
        {
            if (bool.TryParse(raw, out var v)) { result = v; return true; }
            return false;
        }

        if (target.IsEnum)
        {
            try { result = Enum.Parse(target, raw, ignoreCase: true); return true; }
            catch { return false; }
        }

        return false;
    }

    private delegate bool TryParseFunc<T>(string s, out T v);

    private bool Parse<T>(string raw, TryParseFunc<T> parser, Action<T> assign)
    {
        if (parser(raw, out var v)) { assign(v); return true; }
        return false;
    }
}