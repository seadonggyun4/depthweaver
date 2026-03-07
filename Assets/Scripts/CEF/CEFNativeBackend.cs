// Assets/Scripts/CEF/CEFNativeBackend.cs
// ══════════════════════════════════════════════════════════════════════
// Depthweaver Phase 1 — CefSharp 네이티브 플러그인 백엔드
// ══════════════════════════════════════════════════════════════════════
//
// IBrowserBackend를 CefSharp C++ 네이티브 플러그인으로 구현한다.
// NativePluginInterop의 P/Invoke 호출을 래핑하여
// 상위 레이어(CEFTextureSource, CEFBridge)에 깔끔한 API를 제공한다.
//
// 이 클래스는 MonoBehaviour가 아니다.
// CEFTextureSource가 이 클래스의 인스턴스를 관리한다.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

public class CEFNativeBackend : IBrowserBackend
{
    // ═══════════════════════════════════════════════════
    // IBrowserBackend 이벤트
    // ═══════════════════════════════════════════════════

    public event EventHandler<BrowserFrameEventArgs> OnFrameReady;
    public event EventHandler<PageLoadEventArgs> OnLoadStarted;
    public event EventHandler<PageLoadEventArgs> OnLoadCompleted;
    public event EventHandler<PageLoadEventArgs> OnLoadError;

    // ═══════════════════════════════════════════════════
    // IBrowserBackend 속성
    // ═══════════════════════════════════════════════════

    public bool IsInitialized { get; private set; }
    public bool IsPageLoaded => IsInitialized && NativePluginInterop.CEF_IsLoaded();
    public string CurrentURL => GetCurrentURLInternal();
    public int RenderWidth { get; private set; }
    public int RenderHeight { get; private set; }
    public string BackendName => "CEF Native (CefSharp)";

    // ═══════════════════════════════════════════════════
    // 내부 상태
    // ═══════════════════════════════════════════════════

    private readonly byte[] urlBuffer = new byte[2048];
    private int jsCallbackCounter;
    private readonly Dictionary<int, Action<bool, string>> pendingJSCallbacks = new Dictionary<int, Action<bool, string>>();
    private readonly byte[] jsResultBuffer = new byte[65536]; // 64KB JS 결과 버퍼
    private bool wasLoaded;
    private bool disposed;

    // 성능 측정
    private double lastFrameTimestamp;

    /// <summary>마지막 프레임 전송 소요 시간 (밀리초)</summary>
    public float LastFrameTransferTimeMs { get; private set; }

    // ═══════════════════════════════════════════════════
    // 생명주기
    // ═══════════════════════════════════════════════════

    public bool Initialize(int width, int height, int frameRate)
    {
        if (IsInitialized)
        {
            Debug.LogWarning("[UIShader] CEFNativeBackend: 이미 초기화됨");
            return false;
        }

        RenderWidth = width;
        RenderHeight = height;

        int result = NativePluginInterop.CEF_Initialize(width, height, frameRate);
        if (result != 0)
        {
            Debug.LogError($"[UIShader] CEFNativeBackend 초기화 실패: {NativePluginInterop.GetErrorMessage(result)}");
            return false;
        }

        IsInitialized = true;
        wasLoaded = false;

        Debug.Log($"[UIShader] CEFNativeBackend 초기화 완료: {width}x{height} @ {frameRate}fps");
        return true;
    }

    public void Shutdown()
    {
        if (!IsInitialized) return;

        NativePluginInterop.CEF_Shutdown();
        IsInitialized = false;
        pendingJSCallbacks.Clear();

        Debug.Log("[UIShader] CEFNativeBackend 종료 완료");
    }

    public void Tick()
    {
        if (!IsInitialized) return;

        // CEF 메시지 루프 진행
        NativePluginInterop.CEF_DoMessageLoopWork();

        // 로드 상태 변화 감지 (네이티브 콜백 대신 폴링)
        PollLoadState();

        // 대기 중인 JS 결과 폴링
        PollJSResults();
    }

    // ═══════════════════════════════════════════════════
    // 네비게이션
    // ═══════════════════════════════════════════════════

    public void LoadURL(string url)
    {
        if (!IsInitialized) return;

        wasLoaded = false;
        OnLoadStarted?.Invoke(this, new PageLoadEventArgs(url, true));

        NativePluginInterop.CEF_LoadURL(url);
        Debug.Log($"[UIShader] CEF URL 로드: {url}");
    }

    public void GoBack()
    {
        if (!IsInitialized) return;
        NativePluginInterop.CEF_GoBack();
    }

    public void GoForward()
    {
        if (!IsInitialized) return;
        NativePluginInterop.CEF_GoForward();
    }

    public void Reload()
    {
        if (!IsInitialized) return;
        NativePluginInterop.CEF_Reload();
    }

    // ═══════════════════════════════════════════════════
    // JavaScript
    // ═══════════════════════════════════════════════════

    public void ExecuteJavaScript(string script)
    {
        if (!IsInitialized) return;
        NativePluginInterop.CEF_ExecuteJS(script);
    }

    public void EvaluateJavaScript(string script, Action<bool, string> callback)
    {
        if (!IsInitialized)
        {
            callback?.Invoke(false, "Backend not initialized");
            return;
        }

        int callbackId = ++jsCallbackCounter;
        if (callback != null)
        {
            pendingJSCallbacks[callbackId] = callback;
        }

        NativePluginInterop.CEF_EvaluateJS(script, callbackId);
    }

    // ═══════════════════════════════════════════════════
    // 입력 전달
    // ═══════════════════════════════════════════════════

    public void SendMouseMove(int x, int y)
    {
        if (!IsInitialized) return;
        NativePluginInterop.CEF_SendMouseEvent(x, y, 0, 0); // eventType 0 = Move
    }

    public void SendMouseDown(int x, int y, MouseButton button)
    {
        if (!IsInitialized) return;
        NativePluginInterop.CEF_SendMouseEvent(x, y, (int)button, 1); // eventType 1 = Down
    }

    public void SendMouseUp(int x, int y, MouseButton button)
    {
        if (!IsInitialized) return;
        NativePluginInterop.CEF_SendMouseEvent(x, y, (int)button, 2); // eventType 2 = Up
    }

    public void SendMouseWheel(int x, int y, int deltaX, int deltaY)
    {
        if (!IsInitialized) return;
        NativePluginInterop.CEF_SendMouseWheelEvent(x, y, deltaX, deltaY);
    }

    public void SendKeyDown(int windowsKeyCode, KeyModifiers modifiers)
    {
        if (!IsInitialized) return;
        NativePluginInterop.CEF_SendKeyEvent(windowsKeyCode, 0, (int)modifiers); // keyType 0 = KeyDown
    }

    public void SendKeyUp(int windowsKeyCode, KeyModifiers modifiers)
    {
        if (!IsInitialized) return;
        NativePluginInterop.CEF_SendKeyEvent(windowsKeyCode, 1, (int)modifiers); // keyType 1 = KeyUp
    }

    public void SendChar(char character)
    {
        if (!IsInitialized) return;
        NativePluginInterop.CEF_SendKeyEvent((int)character, 2, 0); // keyType 2 = Char
    }

    // ═══════════════════════════════════════════════════
    // 프레임 획득
    // ═══════════════════════════════════════════════════

    public bool HasNewFrame()
    {
        return IsInitialized && NativePluginInterop.CEF_HasNewFrame();
    }

    public void CopyPixelBuffer(IntPtr dest, out int width, out int height)
    {
        if (!IsInitialized)
        {
            width = 0;
            height = 0;
            return;
        }

        NativePluginInterop.CEF_GetPixelBuffer(dest, out width, out height);
        lastFrameTimestamp = NativePluginInterop.CEF_GetFrameTimestamp();
    }

    public IntPtr GetSharedBufferPtr()
    {
        if (!IsInitialized) return IntPtr.Zero;
        return NativePluginInterop.CEF_GetSharedBufferPtr();
    }

    // ═══════════════════════════════════════════════════
    // 리사이즈
    // ═══════════════════════════════════════════════════

    public void Resize(int newWidth, int newHeight)
    {
        if (!IsInitialized) return;

        RenderWidth = newWidth;
        RenderHeight = newHeight;
        NativePluginInterop.CEF_Resize(newWidth, newHeight);

        Debug.Log($"[UIShader] CEF 리사이즈: {newWidth}x{newHeight}");
    }

    // ═══════════════════════════════════════════════════
    // IDisposable
    // ═══════════════════════════════════════════════════

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        Shutdown();
    }

    // ═══════════════════════════════════════════════════
    // 내부 헬퍼
    // ═══════════════════════════════════════════════════

    private string GetCurrentURLInternal()
    {
        if (!IsInitialized) return string.Empty;

        int len = NativePluginInterop.CEF_GetCurrentURL(urlBuffer, urlBuffer.Length);
        if (len <= 0) return string.Empty;

        return Encoding.UTF8.GetString(urlBuffer, 0, len);
    }

    /// <summary>
    /// 네이티브 플러그인의 로드 상태를 폴링하여 이벤트를 발생시킨다.
    /// CEF 콜백을 Unity 메인 스레드에서 안전하게 처리하기 위한 방식.
    /// </summary>
    private void PollLoadState()
    {
        bool currentlyLoaded = NativePluginInterop.CEF_IsLoaded();

        if (currentlyLoaded && !wasLoaded)
        {
            wasLoaded = true;
            string url = GetCurrentURLInternal();
            OnLoadCompleted?.Invoke(this, new PageLoadEventArgs(url, true));
        }
        else if (!currentlyLoaded && wasLoaded)
        {
            wasLoaded = false;
        }
    }

    /// <summary>
    /// 대기 중인 JS 평가 결과를 폴링한다.
    /// </summary>
    private void PollJSResults()
    {
        if (pendingJSCallbacks.Count == 0) return;

        // 복사본으로 순회 (콜백 내에서 딕셔너리 수정 가능)
        var pendingIds = new List<int>(pendingJSCallbacks.Keys);

        foreach (int callbackId in pendingIds)
        {
            if (NativePluginInterop.CEF_GetJSResult(callbackId, jsResultBuffer, jsResultBuffer.Length))
            {
                string result = Encoding.UTF8.GetString(jsResultBuffer).TrimEnd('\0');

                if (pendingJSCallbacks.TryGetValue(callbackId, out var callback))
                {
                    pendingJSCallbacks.Remove(callbackId);
                    callback?.Invoke(true, result);
                }
            }
        }
    }
}
