// Assets/Scripts/CEF/IBrowserBackend.cs
// ══════════════════════════════════════════════════════════════════════
// Depthweaver Phase 1 — 브라우저 백엔드 추상화
// ══════════════════════════════════════════════════════════════════════
//
// 브라우저 엔진의 구체적 구현(CefSharp, Vuplex, 커스텀 등)을
// 나머지 시스템으로부터 완전히 분리하는 전략 패턴 인터페이스.
//
// 구현체:
//   - CEFNativeBackend: CefSharp C++ 네이티브 플러그인 (기본)
//   - (확장 가능) VuplexBackend, WebViewBackend 등
//
// CEFTextureSource는 이 인터페이스만 참조하므로,
// 백엔드 교체 시 텍스처 파이프라인 코드는 변경되지 않는다.

using System;
using UnityEngine;

/// <summary>
/// 브라우저 프레임 수신 이벤트 인자.
/// 백엔드가 새 프레임을 렌더링할 때마다 발생한다.
/// </summary>
public class BrowserFrameEventArgs : EventArgs
{
    /// <summary>픽셀 데이터 포인터 (BGRA32, width * height * 4 바이트)</summary>
    public IntPtr PixelBufferPtr { get; }

    /// <summary>프레임 너비 (픽셀)</summary>
    public int Width { get; }

    /// <summary>프레임 높이 (픽셀)</summary>
    public int Height { get; }

    /// <summary>프레임 타임스탬프 (밀리초)</summary>
    public double Timestamp { get; }

    public BrowserFrameEventArgs(IntPtr pixelBufferPtr, int width, int height, double timestamp)
    {
        PixelBufferPtr = pixelBufferPtr;
        Width = width;
        Height = height;
        Timestamp = timestamp;
    }
}

/// <summary>
/// 페이지 로드 상태 이벤트 인자.
/// </summary>
public class PageLoadEventArgs : EventArgs
{
    public string URL { get; }
    public bool IsMainFrame { get; }
    public int HttpStatusCode { get; }
    public string ErrorMessage { get; }

    public PageLoadEventArgs(string url, bool isMainFrame, int httpStatusCode = 200, string errorMessage = null)
    {
        URL = url;
        IsMainFrame = isMainFrame;
        HttpStatusCode = httpStatusCode;
        ErrorMessage = errorMessage;
    }
}

/// <summary>
/// 브라우저 백엔드 인터페이스.
/// 오프스크린 브라우저 렌더링, URL 탐색, JS 실행, 입력 전달을 추상화한다.
/// </summary>
public interface IBrowserBackend : IDisposable
{
    // ═══════════════════════════════════════════════════
    // 이벤트
    // ═══════════════════════════════════════════════════

    /// <summary>새 프레임이 렌더링되었을 때 발생</summary>
    event EventHandler<BrowserFrameEventArgs> OnFrameReady;

    /// <summary>페이지 로드 시작 시 발생</summary>
    event EventHandler<PageLoadEventArgs> OnLoadStarted;

    /// <summary>페이지 로드 완료 시 발생</summary>
    event EventHandler<PageLoadEventArgs> OnLoadCompleted;

    /// <summary>페이지 로드 오류 시 발생</summary>
    event EventHandler<PageLoadEventArgs> OnLoadError;

    // ═══════════════════════════════════════════════════
    // 속성
    // ═══════════════════════════════════════════════════

    /// <summary>백엔드 초기화 완료 여부</summary>
    bool IsInitialized { get; }

    /// <summary>현재 페이지 로딩 완료 여부</summary>
    bool IsPageLoaded { get; }

    /// <summary>현재 로드된 URL</summary>
    string CurrentURL { get; }

    /// <summary>오프스크린 렌더 너비 (픽셀)</summary>
    int RenderWidth { get; }

    /// <summary>오프스크린 렌더 높이 (픽셀)</summary>
    int RenderHeight { get; }

    /// <summary>백엔드 이름 (로깅 및 디버그용)</summary>
    string BackendName { get; }

    // ═══════════════════════════════════════════════════
    // 생명주기
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// 브라우저 백엔드를 초기화한다.
    /// width/height는 오프스크린 렌더 해상도, frameRate는 목표 FPS.
    /// </summary>
    /// <returns>성공 시 true</returns>
    bool Initialize(int width, int height, int frameRate);

    /// <summary>
    /// 브라우저를 종료하고 리소스를 해제한다.
    /// IDisposable.Dispose()와 동일하지만, 명시적 이름 제공.
    /// </summary>
    void Shutdown();

    /// <summary>
    /// 매 프레임 호출. 브라우저 메시지 루프 진행.
    /// </summary>
    void Tick();

    // ═══════════════════════════════════════════════════
    // 네비게이션
    // ═══════════════════════════════════════════════════

    /// <summary>URL을 로드한다</summary>
    void LoadURL(string url);

    /// <summary>뒤로 가기</summary>
    void GoBack();

    /// <summary>앞으로 가기</summary>
    void GoForward();

    /// <summary>페이지 새로고침</summary>
    void Reload();

    // ═══════════════════════════════════════════════════
    // JavaScript
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// JavaScript를 실행한다 (fire-and-forget).
    /// Phase 2 depth-extractor.js 주입 시 사용.
    /// </summary>
    void ExecuteJavaScript(string script);

    /// <summary>
    /// JavaScript를 실행하고 결과를 콜백으로 반환한다.
    /// </summary>
    void EvaluateJavaScript(string script, Action<bool, string> callback);

    // ═══════════════════════════════════════════════════
    // 입력 전달
    // ═══════════════════════════════════════════════════

    /// <summary>마우스 이동 이벤트</summary>
    void SendMouseMove(int x, int y);

    /// <summary>마우스 버튼 다운</summary>
    void SendMouseDown(int x, int y, MouseButton button);

    /// <summary>마우스 버튼 업</summary>
    void SendMouseUp(int x, int y, MouseButton button);

    /// <summary>마우스 휠 스크롤</summary>
    void SendMouseWheel(int x, int y, int deltaX, int deltaY);

    /// <summary>키 다운</summary>
    void SendKeyDown(int windowsKeyCode, KeyModifiers modifiers);

    /// <summary>키 업</summary>
    void SendKeyUp(int windowsKeyCode, KeyModifiers modifiers);

    /// <summary>문자 입력 (텍스트 필드용)</summary>
    void SendChar(char character);

    // ═══════════════════════════════════════════════════
    // 프레임 획득
    // ═══════════════════════════════════════════════════

    /// <summary>새 프레임 대기 중 여부</summary>
    bool HasNewFrame();

    /// <summary>
    /// 픽셀 데이터를 대상 버퍼에 복사한다.
    /// dest는 width * height * 4 바이트 이상이어야 함 (BGRA32).
    /// </summary>
    void CopyPixelBuffer(IntPtr dest, out int width, out int height);

    /// <summary>
    /// 공유 메모리 포인터를 직접 반환한다 (복사 없음, 최적화용).
    /// 지원하지 않는 백엔드는 IntPtr.Zero를 반환.
    /// </summary>
    IntPtr GetSharedBufferPtr();

    // ═══════════════════════════════════════════════════
    // 리사이즈
    // ═══════════════════════════════════════════════════

    /// <summary>오프스크린 렌더 해상도를 변경한다</summary>
    void Resize(int newWidth, int newHeight);
}

/// <summary>CEF 마우스 버튼 열거형</summary>
public enum MouseButton
{
    Left = 0,
    Middle = 1,
    Right = 2
}

/// <summary>CEF 키보드 수정자 플래그</summary>
[Flags]
public enum KeyModifiers
{
    None = 0,
    Shift = 1 << 0,
    Control = 1 << 1,
    Alt = 1 << 2,
    Command = 1 << 3 // macOS
}
