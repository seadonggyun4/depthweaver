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

    /// Helper 앱 경로 설정 (macOS 전용, CEF_Initialize 전에 호출)
    EXPORT void CEF_SetHelperPath(const char* path);

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

    // ═══════════════════════════════════════════════════
    // 진단
    // ═══════════════════════════════════════════════════

    /// 진단 정보: [0]=OnPaint 횟수, [1]=GetViewRect 횟수, [2]=브라우저 존재, [3]=렌더러 존재
    EXPORT void CEF_GetDiagnostics(int* buffer, int maxLen);

    // ═══════════════════════════════════════════════════
    // 깊이 데이터 (Phase 2)
    // ═══════════════════════════════════════════════════

    /// 새로운 깊이 프레임이 있는지 확인
    EXPORT bool CEF_HasNewDepthFrame();

    /// 깊이 픽셀 데이터를 복사 (R-channel only)
    /// dest: size×size 바이트 버퍼
    /// outSize: 깊이 맵 한 변 크기 (예: 512)
    /// 반환: 성공 여부
    EXPORT bool CEF_GetDepthPixels(void* dest, int* outSize);

    /// 깊이 프레임 수신 횟수 (진단용)
    EXPORT int CEF_GetDepthFrameCount();

    // ═══════════════════════════════════════════════════
    // 로그 시스템 (Unity Console 출력용)
    // ═══════════════════════════════════════════════════

    /// 버퍼링된 로그 메시지를 가져온다.
    /// buffer: 로그 메시지를 받을 char 배열
    /// maxLen: 버퍼 최대 크기
    /// 반환: 실제 복사된 바이트 수 (0이면 새 로그 없음)
    EXPORT int CEF_GetLogMessages(char* buffer, int maxLen);

} // extern "C"
