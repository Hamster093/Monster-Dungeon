/****************************************************
    文件：ScreenFader.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/15 15:51:01
	功能：全屏黑幕渐变。挂在 UIManager 的 Layer_Modal 层最底层下的一个 Image 上。
*****************************************************/

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ScreenFader : MonoBehaviour
{
    public static ScreenFader Instance { get; private set; }

    [SerializeField] private Image _fadeImage;
    [SerializeField] private Image _imaBG;

    private Coroutine _routine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        ResetFader();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ============================================================
    //  对外接口
    // ============================================================

    public void FadeToBlack(float duration, Action onComplete = null)
    {
        if (_imaBG != null) _imaBG.gameObject.SetActive(true);
        StartFade(0f, 1f, duration, onComplete);
    }

    public void FadeFromBlack(float duration, Action onComplete = null)
    {
        StartFade(1f, 0f, duration, onComplete);
    }

    /// <summary>立即设为全黑或全透明（无渐变）</summary>
    public void SetBlack(bool black)
    {
        if (_routine != null) { StopCoroutine(_routine); _routine = null; }

        if (black)
        {
            if (_imaBG != null) _imaBG.gameObject.SetActive(true);
            if (_fadeImage != null)
            {
                _fadeImage.gameObject.SetActive(true);
                SetAlpha(1f);
            }
        }
        else
        {
            ResetFader();
        }
    }

    /// <summary>
    /// 重置所有状态：停协程、黑幕透明、_fadeImage 与 _imaBG 全部隐藏。
    /// 打开面板 / 切场景 / 异常中断后调用，避免下次打开还是黑的。
    /// </summary>
    public void ResetFader()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }

        if (_fadeImage != null)
        {
            _fadeImage.raycastTarget = false;
            SetAlpha(0f);
            _fadeImage.gameObject.SetActive(false);
        }

        if (_imaBG != null)
            _imaBG.gameObject.SetActive(false);
    }

    // ============================================================
    //  内部
    // ============================================================

    private void StartFade(float from, float to, float duration, Action onComplete)
    {
        if (_fadeImage == null)
        {
            onComplete?.Invoke();
            return;
        }

        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(FadeRoutine(from, to, duration, onComplete));
    }

    private IEnumerator FadeRoutine(float from, float to, float duration, Action onComplete)
    {
        _fadeImage.gameObject.SetActive(true);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetAlpha(Mathf.Lerp(from, to, t));
            yield return null;
        }

        SetAlpha(to);

        // ★ 变透明 → 把黑幕和背景遮罩都关掉
        if (Mathf.Approximately(to, 0f))
        {
            _fadeImage.gameObject.SetActive(false);
            if (_imaBG != null) _imaBG.gameObject.SetActive(false);
        }

        _routine = null;
        onComplete?.Invoke();
    }

    private void SetAlpha(float a)
    {
        if (_fadeImage == null) return;
        var c = _fadeImage.color;
        c.a = a;
        _fadeImage.color = c;
    }
}