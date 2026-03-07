// NativePlugin/src/cef_plugin.cpp
// ══════════════════════════════════════════════════════════════════════
// Depthweaver Phase 1 — CEF 플러그인 메인 진입점
// ══════════════════════════════════════════════════════════════════════
//
// 모든 extern "C" API를 구현한다.
// 전역 상태(g_renderHandler, g_client)를 관리하며,
// Unity C#의 NativePluginInterop에서 호출된다.

#include "cef_plugin.h"
#include "browser_app.h"
#include "browser_client.h"
#include "render_handler.h"
#include "include/cef_app.h"
#include "include/cef_browser.h"

#include <cstring>
#include <algorithm>

// ═══════════════════════════════════════════════════
// 전역 상태
// ═══════════════════════════════════════════════════

static CefRefPtr<OffscreenRenderHandler> g_renderHandler;
static CefRefPtr<BrowserClient> g_client;
static int g_width = 512;
static int g_height = 512;
static bool g_initialized = false;

// ═══════════════════════════════════════════════════
// 초기화 및 종료
// ═══════════════════════════════════════════════════

EXPORT int CEF_Initialize(int width, int height, int frameRate) {
    if (g_initialized) return -1;

    g_width = width;
    g_height = height;

    CefMainArgs args;
    CefSettings settings;

    settings.windowless_rendering_enabled = true;
    settings.no_sandbox = true;
    settings.multi_threaded_message_loop = false;
    settings.log_severity = LOGSEVERITY_WARNING;
    settings.windowless_frame_rate = frameRate;

#ifdef _WIN32
    CefString(&settings.browser_subprocess_path).FromASCII("");
#endif

    CefRefPtr<BrowserApp> app(new BrowserApp());

    if (!CefInitialize(args, settings, app, nullptr)) {
        return -2;
    }

    // 렌더 핸들러 및 클라이언트 생성
    g_renderHandler = new OffscreenRenderHandler(width, height);
    g_client = new BrowserClient(g_renderHandler);

    // 오프스크린 브라우저 생성
    CefWindowInfo windowInfo;
    windowInfo.SetAsWindowless(0);

    CefBrowserSettings browserSettings;
    browserSettings.windowless_frame_rate = frameRate;
    browserSettings.javascript = STATE_ENABLED;
    browserSettings.webgl = STATE_ENABLED;

    CefBrowserHost::CreateBrowser(
        windowInfo, g_client, "about:blank", browserSettings, nullptr, nullptr
    );

    g_initialized = true;
    return 0;
}

EXPORT void CEF_Shutdown() {
    if (!g_initialized) return;

    if (g_client && g_client->GetBrowser()) {
        g_client->GetBrowser()->GetHost()->CloseBrowser(true);
    }

    g_client = nullptr;
    g_renderHandler = nullptr;

    CefShutdown();
    g_initialized = false;
}

EXPORT void CEF_DoMessageLoopWork() {
    if (!g_initialized) return;
    CefDoMessageLoopWork();
}

EXPORT bool CEF_IsInitialized() {
    return g_initialized;
}

// ═══════════════════════════════════════════════════
// 페이지 로드
// ═══════════════════════════════════════════════════

EXPORT void CEF_LoadURL(const char* url) {
    if (!g_initialized || !g_client || !g_client->GetBrowser()) return;
    g_client->GetBrowser()->GetMainFrame()->LoadURL(url);
}

EXPORT int CEF_GetCurrentURL(char* buffer, int maxLen) {
    if (!g_initialized || !g_client || !g_client->GetBrowser()) return 0;
    std::string url = g_client->GetBrowser()->GetMainFrame()->GetURL().ToString();
    int len = std::min(static_cast<int>(url.size()), maxLen - 1);
    memcpy(buffer, url.c_str(), len);
    buffer[len] = '\0';
    return len;
}

EXPORT bool CEF_IsLoaded() {
    return g_initialized && g_client && g_client->IsLoaded();
}

EXPORT void CEF_GoBack() {
    if (!g_initialized || !g_client || !g_client->GetBrowser()) return;
    g_client->GetBrowser()->GoBack();
}

EXPORT void CEF_GoForward() {
    if (!g_initialized || !g_client || !g_client->GetBrowser()) return;
    g_client->GetBrowser()->GoForward();
}

EXPORT void CEF_Reload() {
    if (!g_initialized || !g_client || !g_client->GetBrowser()) return;
    g_client->GetBrowser()->Reload();
}

// ═══════════════════════════════════════════════════
// 프레임 획득
// ═══════════════════════════════════════════════════

EXPORT bool CEF_HasNewFrame() {
    return g_initialized && g_renderHandler && g_renderHandler->HasNewFrame();
}

EXPORT void CEF_GetPixelBuffer(void* dest, int* outWidth, int* outHeight) {
    if (!g_initialized || !g_renderHandler) return;
    g_renderHandler->CopyToDestination(dest, outWidth, outHeight);
}

EXPORT const void* CEF_GetSharedBufferPtr() {
    if (!g_initialized || !g_renderHandler) return nullptr;
    return g_renderHandler->GetFrontBufferPtr();
}

EXPORT double CEF_GetFrameTimestamp() {
    if (!g_initialized || !g_renderHandler) return 0;
    return g_renderHandler->GetFrameTimestamp();
}

EXPORT void CEF_CopyToNativeTexture(void* texturePtr, int width, int height) {
    // DirectX/Metal 네이티브 텍스처 기록
    // 플랫폼별 구현이 필요 — 현재는 스텁
    // Phase 1.3 최적화 경로에서 활성화
    (void)texturePtr;
    (void)width;
    (void)height;
}

EXPORT int CEF_GetDirtyRects(int* buffer, int maxRects) {
    if (!g_initialized || !g_renderHandler) return 0;
    return g_renderHandler->GetDirtyRects(buffer, maxRects);
}

// ═══════════════════════════════════════════════════
// 입력 전달
// ═══════════════════════════════════════════════════

EXPORT void CEF_SendMouseEvent(int x, int y, int mouseButton, int eventType) {
    if (!g_initialized || !g_client || !g_client->GetBrowser()) return;

    CefMouseEvent event;
    event.x = x;
    event.y = y;
    event.modifiers = 0;

    auto host = g_client->GetBrowser()->GetHost();

    switch (eventType) {
        case 0: // Move
            host->SendMouseMoveEvent(event, false);
            break;
        case 1: // Down
            host->SendMouseClickEvent(event,
                static_cast<CefBrowserHost::MouseButtonType>(mouseButton),
                false, 1);
            break;
        case 2: // Up
            host->SendMouseClickEvent(event,
                static_cast<CefBrowserHost::MouseButtonType>(mouseButton),
                true, 1);
            break;
    }
}

EXPORT void CEF_SendMouseWheelEvent(int x, int y, int deltaX, int deltaY) {
    if (!g_initialized || !g_client || !g_client->GetBrowser()) return;

    CefMouseEvent event;
    event.x = x;
    event.y = y;

    g_client->GetBrowser()->GetHost()->SendMouseWheelEvent(event, deltaX, deltaY);
}

EXPORT void CEF_SendKeyEvent(int keyCode, int keyType, int modifiers) {
    if (!g_initialized || !g_client || !g_client->GetBrowser()) return;

    CefKeyEvent event;
    event.windows_key_code = keyCode;
    event.modifiers = modifiers;

    switch (keyType) {
        case 0: event.type = KEYEVENT_KEYDOWN; break;
        case 1: event.type = KEYEVENT_KEYUP; break;
        case 2: event.type = KEYEVENT_CHAR; break;
    }

    g_client->GetBrowser()->GetHost()->SendKeyEvent(event);
}

// ═══════════════════════════════════════════════════
// JavaScript
// ═══════════════════════════════════════════════════

EXPORT void CEF_ExecuteJS(const char* script) {
    if (!g_initialized || !g_client || !g_client->GetBrowser()) return;
    auto frame = g_client->GetBrowser()->GetMainFrame();
    frame->ExecuteJavaScript(script, frame->GetURL(), 0);
}

EXPORT void CEF_EvaluateJS(const char* script, int callbackId) {
    if (!g_initialized || !g_client) return;
    g_client->EvaluateJS(script, callbackId);
}

EXPORT bool CEF_GetJSResult(int callbackId, char* buffer, int maxLen) {
    if (!g_initialized || !g_client) return false;
    std::string result;
    if (g_client->GetJSResult(callbackId, result)) {
        int len = std::min(static_cast<int>(result.size()), maxLen - 1);
        memcpy(buffer, result.c_str(), len);
        buffer[len] = '\0';
        return true;
    }
    return false;
}

// ═══════════════════════════════════════════════════
// 리사이즈
// ═══════════════════════════════════════════════════

EXPORT void CEF_Resize(int newWidth, int newHeight) {
    if (!g_initialized || !g_renderHandler || !g_client) return;
    g_width = newWidth;
    g_height = newHeight;
    g_renderHandler->Resize(newWidth, newHeight);
    if (g_client->GetBrowser()) {
        g_client->GetBrowser()->GetHost()->WasResized();
    }
}
