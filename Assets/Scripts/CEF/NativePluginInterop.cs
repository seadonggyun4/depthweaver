// Assets/Scripts/CEF/NativePluginInterop.cs
// ══════════════════════════════════════════════════════════════════════
// Depthweaver Phase 1 — CEF 네이티브 플러그인 P/Invoke 선언
// ══════════════════════════════════════════════════════════════════════
//
// 모든 C++ 네이티브 플러그인 호출을 한 곳에 집중시킨다.
// 이 클래스를 수정하면 모든 P/Invoke 시그니처가 일관되게 관리된다.
//
// 네이티브 플러그인 이름:
//   - Windows: cef_plugin.dll
//   - macOS:   cef_plugin.bundle
//
// 에러 코드:
//    0 = 성공
//   -1 = 이미 초기화됨
//   -2 = CEF 초기화 실패
//   -3 = 브라우저 생성 실패

using System;
using System.Runtime.InteropServices;

/// <summary>
/// CEF 네이티브 플러그인 P/Invoke 정적 래퍼.
/// 모든 네이티브 호출을 중앙 집중화하여 관리한다.
/// </summary>
public static class NativePluginInterop
{
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
    private const string PLUGIN_NAME = "cef_plugin";
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
    private const string PLUGIN_NAME = "cef_plugin";
#else
    private const string PLUGIN_NAME = "cef_plugin";
#endif

    // ═══════════════════════════════════════════════════
    // 초기화 및 종료
    // ═══════════════════════════════════════════════════

    [DllImport(PLUGIN_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern int CEF_Initialize(int width, int height, int frameRate);

    [DllImport(PLUGIN_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern void CEF_Shutdown();

    [DllImport(PLUGIN_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern void CEF_DoMessageLoopWork();

    [DllImport(PLUGIN_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern bool CEF_IsInitialized();

    // ═══════════════════════════════════════════════════
    // 페이지 로드
    // ═══════════════════════════════════════════════════

    [DllImport(PLUGIN_NAME, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void CEF_LoadURL([MarshalAs(UnmanagedType.LPStr)] string url);

    [DllImport(PLUGIN_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern int CEF_GetCurrentURL(
        [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] buffer,
        int maxLen
    );

    [DllImport(PLUGIN_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern bool CEF_IsLoaded();

    [DllImport(PLUGIN_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern void CEF_GoBack();

    [DllImport(PLUGIN_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern void CEF_GoForward();

    [DllImport(PLUGIN_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern void CEF_Reload();

    // ═══════════════════════════════════════════════════
    // 프레임 획득
    // ═══════════════════════════════════════════════════

    [DllImport(PLUGIN_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern bool CEF_HasNewFrame();

    [DllImport(PLUGIN_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern void CEF_GetPixelBuffer(IntPtr dest, out int outWidth, out int outHeight);

    [DllImport(PLUGIN_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr CEF_GetSharedBufferPtr();

    [DllImport(PLUGIN_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern double CEF_GetFrameTimestamp();

    /// <summary>
    /// 네이티브 텍스처 포인터에 직접 복사 (최적화 경로).
    /// GPU→GPU 복사로 CPU 복사를 우회한다.
    /// </summary>
    [DllImport(PLUGIN_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern void CEF_CopyToNativeTexture(IntPtr texturePtr, int width, int height);

    /// <summary>
    /// 더티 영역 정보 획득. buffer에 [x0,y0,w0,h0, x1,y1,w1,h1, ...] 형태로 기록.
    /// 반환값: 더티 영역 수.
    /// </summary>
    [DllImport(PLUGIN_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern int CEF_GetDirtyRects(
        [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] int[] buffer,
        int maxRects
    );

    // ═══════════════════════════════════════════════════
    // 입력 전달
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// 마우스 이벤트. eventType: 0=Move, 1=Down, 2=Up
    /// mouseButton: 0=Left, 1=Middle, 2=Right
    /// </summary>
    [DllImport(PLUGIN_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern void CEF_SendMouseEvent(int x, int y, int mouseButton, int eventType);

    [DllImport(PLUGIN_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern void CEF_SendMouseWheelEvent(int x, int y, int deltaX, int deltaY);

    /// <summary>
    /// 키 이벤트. keyType: 0=KeyDown, 1=KeyUp, 2=Char
    /// </summary>
    [DllImport(PLUGIN_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern void CEF_SendKeyEvent(int keyCode, int keyType, int modifiers);

    // ═══════════════════════════════════════════════════
    // JavaScript 실행
    // ═══════════════════════════════════════════════════

    [DllImport(PLUGIN_NAME, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void CEF_ExecuteJS([MarshalAs(UnmanagedType.LPStr)] string script);

    [DllImport(PLUGIN_NAME, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void CEF_EvaluateJS([MarshalAs(UnmanagedType.LPStr)] string script, int callbackId);

    [DllImport(PLUGIN_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern bool CEF_GetJSResult(
        int callbackId,
        [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] byte[] buffer,
        int maxLen
    );

    // ═══════════════════════════════════════════════════
    // 리사이즈
    // ═══════════════════════════════════════════════════

    [DllImport(PLUGIN_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern void CEF_Resize(int newWidth, int newHeight);

    // ═══════════════════════════════════════════════════
    // 유틸리티
    // ═══════════════════════════════════════════════════

    /// <summary>네이티브 플러그인 존재 여부를 확인한다</summary>
    public static bool IsPluginAvailable()
    {
        try
        {
            return CEF_IsInitialized() || true; // DllImport가 성공하면 플러그인 존재
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    /// <summary>에러 코드를 사람이 읽을 수 있는 메시지로 변환</summary>
    public static string GetErrorMessage(int errorCode)
    {
        switch (errorCode)
        {
            case 0: return "성공";
            case -1: return "이미 초기화됨";
            case -2: return "CEF 초기화 실패";
            case -3: return "브라우저 생성 실패";
            default: return $"알 수 없는 오류 ({errorCode})";
        }
    }
}
