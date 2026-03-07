// NativePlugin/src/cef_plugin.h
// ══════════════════════════════════════════════════════════════════════
// Depthweaver Phase 1 — CEF 플러그인 공개 C API
// ══════════════════════════════════════════════════════════════════════
//
// Unity C#에서 P/Invoke로 호출하는 모든 함수를 선언한다.
// NativePluginInterop.cs의 DllImport 시그니처와 1:1 대응.

#pragma once

#ifdef _WIN32
    #define EXPORT __declspec(dllexport)
#else
    #define EXPORT __attribute__((visibility("default")))
#endif

extern "C" {

    // ═══════════════════════════════════════════════════
    // 초기화 및 종료
    // ═══════════════════════════════════════════════════

    /// CEF 초기화. 반환: 0=성공, -1=이미 초기화, -2=CEF 실패, -3=브라우저 생성 실패
    EXPORT int CEF_Initialize(int width, int height, int frameRate);

    /// CEF 종료
    EXPORT void CEF_Shutdown();

    /// CEF 메시지 루프 1회 진행 (매 프레임 호출)
    EXPORT void CEF_DoMessageLoopWork();

    /// 초기화 상태
    EXPORT bool CEF_IsInitialized();

    // ═══════════════════════════════════════════════════
    // 페이지 로드
    // ═══════════════════════════════════════════════════

    EXPORT void CEF_LoadURL(const char* url);
    EXPORT int CEF_GetCurrentURL(char* buffer, int maxLen);
    EXPORT bool CEF_IsLoaded();
    EXPORT void CEF_GoBack();
    EXPORT void CEF_GoForward();
    EXPORT void CEF_Reload();

    // ═══════════════════════════════════════════════════
    // 프레임 획득
    // ═══════════════════════════════════════════════════

    EXPORT bool CEF_HasNewFrame();
    EXPORT void CEF_GetPixelBuffer(void* dest, int* outWidth, int* outHeight);
    EXPORT const void* CEF_GetSharedBufferPtr();
    EXPORT double CEF_GetFrameTimestamp();

    /// 네이티브 텍스처에 직접 복사 (DirectX/Metal)
    EXPORT void CEF_CopyToNativeTexture(void* texturePtr, int width, int height);

    /// 더티 영역 획득. buffer: [x0,y0,w0,h0,...]. 반환: 영역 수
    EXPORT int CEF_GetDirtyRects(int* buffer, int maxRects);

    // ═══════════════════════════════════════════════════
    // 입력 전달
    // ═══════════════════════════════════════════════════

    /// mouseButton: 0=Left,1=Middle,2=Right. eventType: 0=Move,1=Down,2=Up
    EXPORT void CEF_SendMouseEvent(int x, int y, int mouseButton, int eventType);
    EXPORT void CEF_SendMouseWheelEvent(int x, int y, int deltaX, int deltaY);

    /// keyType: 0=KeyDown,1=KeyUp,2=Char
    EXPORT void CEF_SendKeyEvent(int keyCode, int keyType, int modifiers);

    // ═══════════════════════════════════════════════════
    // JavaScript
    // ═══════════════════════════════════════════════════

    EXPORT void CEF_ExecuteJS(const char* script);
    EXPORT void CEF_EvaluateJS(const char* script, int callbackId);
    EXPORT bool CEF_GetJSResult(int callbackId, char* buffer, int maxLen);

    // ═══════════════════════════════════════════════════
    // 리사이즈
    // ═══════════════════════════════════════════════════

    EXPORT void CEF_Resize(int newWidth, int newHeight);

} // extern "C"
