/****************************************************
    文件：Event.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026-09-21 13:01:30
	功能：Nothing
*****************************************************/

using System;
using Unity.Burst.Intrinsics;
using UnityEngine;

public class Event<T> where T : Event<T>
{
    private static Action mOnEvent;
    /// <summary>
    /// 注册事件
    /// </summary>
    /// <param name="onEvent"></param>
    public static void Register(Action onEvent) => mOnEvent += onEvent;
    /// <summary>
    /// 注销事件
    /// </summary>
    /// <param name="onEvent"></param>
    public static void UnRegister(Action onEvent) => mOnEvent -= onEvent;

    /// <summary>
    /// 触发事件
    /// </summary>
    public static void Trigger() => mOnEvent?.Invoke();
}

// 带一个参数的事件
public class Event<T, TArg> where T : Event<T, TArg>
{
    private static Action<TArg> mOnEvent;

    public static void Register(Action<TArg> onEvent) => mOnEvent += onEvent;
    public static void UnRegister(Action<TArg> onEvent) => mOnEvent -= onEvent;
    public static void Trigger(TArg arg) => mOnEvent?.Invoke(arg);
}