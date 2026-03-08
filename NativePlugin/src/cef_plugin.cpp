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
#include <cstdarg>
#include <algorithm>
#include <string>

#ifdef __APPLE__
#include <dlfcn.h>
#include <libgen.h>
#include <unistd.h>
#include "include/wrapper/cef_library_loader.h"
#endif

// ═══════════════════════════════════════════════════
// 로그 버퍼 시스템 (Unity Console 출력용)
// ═══════════════════════════════════════════════════
// fprintf(stderr)는 macOS에서 Unity Console에 표시되지 않으므로,
// 링 버퍼에 로그를 저장하고 C#에서 폴링한다.

static std::mutex g_logMutex;
static std::string g_logBuffer;
static const size_t LOG_BUFFER_MAX = 32768; // 32KB

void DW_Log(const char* fmt, ...) {
    char msg[1024];
    va_list args;
    va_start(args, fmt);
    vsnprintf(msg, sizeof(msg), fmt, args);
    va_end(args);

    // stderr에도 출력 (터미널 디버깅용)
    fprintf(stderr, "%s", msg);

    // 링 버퍼에 추가
    std::lock_guard<std::mutex> lock(g_logMutex);
    g_logBuffer.append(msg);
    // 버퍼 오버플로 방지: 앞부분 자르기
    if (g_logBuffer.size() > LOG_BUFFER_MAX) {
        g_logBuffer = g_logBuffer.substr(g_logBuffer.size() - LOG_BUFFER_MAX / 2);
    }
}

// ═══════════════════════════════════════════════════
// 전역 상태
// ═══════════════════════════════════════════════════

static CefRefPtr<OffscreenRenderHandler> g_renderHandler;
static CefRefPtr<BrowserClient> g_client;
static int g_width = 1024;
static int g_height = 1024;
static bool g_initialized = false;       // 현재 세션 활성 여부
static bool g_cefCoreInitialized = false; // CefInitialize가 한 번이라도 호출되었는지 (프로세스 수명)
static std::string g_helperPath;
static std::string g_pluginDir;
static std::string g_pendingURL; // 브라우저 생성 전 요청된 URL 큐

// BrowserApp에서 설정하는 전역 플래그
std::atomic<bool> g_pumpNeeded{false};

#ifdef __APPLE__
static bool g_libraryLoaded = false;

/// 플러그인 자신의 경로를 기반으로 디렉토리 경로를 얻는다.
static std::string GetPluginDirectory() {
    if (!g_pluginDir.empty()) return g_pluginDir;
    Dl_info info;
    if (dladdr((void*)CEF_SetHelperPath, &info) && info.dli_fname) {
        char pathBuf[4096];
        strncpy(pathBuf, info.dli_fname, sizeof(pathBuf) - 1);
        pathBuf[sizeof(pathBuf) - 1] = '\0';
        g_pluginDir = dirname(pathBuf);
        DW_Log("[Depthweaver] Plugin directory: %s\n", g_pluginDir.c_str());
        DW_Log("[Depthweaver] Plugin file: %s\n", info.dli_fname);
    }
    return g_pluginDir;
}

/// CEF 프레임워크를 동적 로드한다 (macOS 필수).
static bool LoadCEFFramework() {
    if (g_libraryLoaded) return true;
    std::string dir = GetPluginDirectory();
    if (dir.empty()) return false;
    std::string frameworkPath = dir +
        "/Chromium Embedded Framework.framework/Chromium Embedded Framework";
    DW_Log("[Depthweaver] Loading CEF framework: %s\n", frameworkPath.c_str());
    if (!cef_load_library(frameworkPath.c_str())) {
        DW_Log("[Depthweaver] cef_load_library FAILED: %s\n", frameworkPath.c_str());
        return false;
    }
    DW_Log("[Depthweaver] CEF framework loaded successfully\n");
    g_libraryLoaded = true;
    return true;
}

/// Helper 앱 경로를 자동 탐지한다.
static std::string AutoDetectHelperPath() {
    std::string dir = GetPluginDirectory();
    if (dir.empty()) return "";
    std::string path = dir + "/cef_helper.app/Contents/MacOS/cef_helper";
    // 파일 존재 여부 확인
    if (access(path.c_str(), X_OK) == 0) {
        DW_Log("[Depthweaver] Helper binary found: %s\n", path.c_str());
    } else {
        DW_Log("[Depthweaver] WARNING: Helper binary NOT found: %s\n", path.c_str());
    }
    return path;
}
#endif

// ═══════════════════════════════════════════════════
// 내부 헬퍼: 즉시 펌핑
// ═══════════════════════════════════════════════════

/// CEF 메시지 루프를 여러 번 펌핑하여 대기 작업을 소진한다.
static void PumpMessageLoop(int maxIterations = 50) {
    for (int i = 0; i < maxIterations; i++) {
        g_pumpNeeded.store(false, std::memory_order_release);
        CefDoMessageLoopWork();
        if (!g_pumpNeeded.load(std::memory_order_acquire)) break;
    }
}

// ═══════════════════════════════════════════════════
// 초기화 및 종료
// ═══════════════════════════════════════════════════

EXPORT void CEF_SetHelperPath(const char* path) {
    if (path) g_helperPath = path;
}

EXPORT int CEF_Initialize(int width, int height, int frameRate) {
    DW_Log("[Depthweaver] CEF_Initialize called: %dx%d @ %dfps\n", width, height, frameRate);

    // 이미 초기화된 상태면 기존 브라우저를 닫고 재생성
    if (g_initialized) {
        DW_Log("[Depthweaver] Already initialized, shutting down first\n");
        CEF_Shutdown();
    }

    g_width = width;
    g_height = height;

    // ─── CEF 코어 초기화 (프로세스당 1회만) ───
    if (!g_cefCoreInitialized) {
#ifdef __APPLE__
        // macOS: CEF 프레임워크 동적 로드 (모든 CEF API 호출 전에 필수)
        if (!LoadCEFFramework()) {
            return -4; // 프레임워크 로드 실패
        }
#endif

        CefMainArgs args;
        CefSettings settings;

        settings.windowless_rendering_enabled = true;
        settings.no_sandbox = true;
        settings.multi_threaded_message_loop = false;
        settings.external_message_pump = true;
        settings.log_severity = LOGSEVERITY_VERBOSE;

#ifdef __APPLE__
        std::string dir = GetPluginDirectory();

        if (g_helperPath.empty()) {
            g_helperPath = AutoDetectHelperPath();
        }
        if (!g_helperPath.empty()) {
            CefString(&settings.browser_subprocess_path).FromString(g_helperPath);
            DW_Log("[Depthweaver] subprocess_path: %s\n", g_helperPath.c_str());
        }

        std::string frameworkDir = dir + "/Chromium Embedded Framework.framework";
        CefString(&settings.framework_dir_path).FromString(frameworkDir);
        DW_Log("[Depthweaver] framework_dir: %s\n", frameworkDir.c_str());

        std::string mainBundlePath = dir + "/cef_helper.app";
        CefString(&settings.main_bundle_path).FromString(mainBundlePath);

        std::string projectRoot = dir + "/../../../../..";
        std::string cachePath = projectRoot + "/Library/cef_cache";
        CefString(&settings.root_cache_path).FromString(cachePath);
        DW_Log("[Depthweaver] cache_path: %s\n", cachePath.c_str());

        std::string logFile = projectRoot + "/Library/cef_debug.log";
        CefString(&settings.log_file).FromString(logFile);
#endif

        CefRefPtr<BrowserApp> app(new BrowserApp());

        DW_Log("[Depthweaver] Calling CefInitialize...\n");
        if (!CefInitialize(args, settings, app, nullptr)) {
            DW_Log("[Depthweaver] CefInitialize FAILED!\n");
            return -2;
        }
        DW_Log("[Depthweaver] CefInitialize succeeded\n");

        g_cefCoreInitialized = true;

        // 초기화 직후 메시지 펌핑 — 서브프로세스 생성 등 초기 작업 처리
        PumpMessageLoop(100);
    }

    // ─── 브라우저 인스턴스 생성 (Play마다) ───
    g_renderHandler = new OffscreenRenderHandler(width, height);
    g_client = new BrowserClient(g_renderHandler);

    CefWindowInfo windowInfo;
    windowInfo.SetAsWindowless(0);

    CefBrowserSettings browserSettings;
    browserSettings.windowless_frame_rate = frameRate;
    browserSettings.javascript = STATE_ENABLED;
    browserSettings.webgl = STATE_ENABLED;

    // data: URL로 시작 — 네트워크 의존성 제거, 기본 렌더링 검증
    std::string initialURL = "data:text/html,<body style='background:#2196F3;margin:0'>"
                             "<h1 style='color:white;padding:40px;font-family:Arial'>"
                             "Depthweaver CEF OK</h1></body>";

    DW_Log("[Depthweaver] Creating browser with initial URL: %s\n", initialURL.c_str());
    CefBrowserHost::CreateBrowser(
        windowInfo, g_client, initialURL, browserSettings, nullptr, nullptr
    );

    g_initialized = true;

    // CreateBrowser는 비동기 — 즉시 펌핑으로 브라우저 생성 가속
    DW_Log("[Depthweaver] Pumping messages after CreateBrowser...\n");
    PumpMessageLoop(200);

    // 브라우저 생성 완료 확인
    if (g_client && g_client->GetBrowser()) {
        DW_Log("[Depthweaver] Browser created immediately after pump! ID=%d\n",
                g_client->GetBrowser()->GetIdentifier());
    } else {
        DW_Log("[Depthweaver] Browser not yet created (will be created in subsequent pumps)\n");
    }

    return 0;
}

EXPORT void CEF_Shutdown() {
    if (!g_initialized) return;

    DW_Log("[Depthweaver] CEF_Shutdown called\n");

    // 브라우저만 닫고 CEF 코어는 유지 (프로세스 수명 동안 재초기화 불가)
    if (g_client && g_client->GetBrowser()) {
        g_client->GetBrowser()->GetHost()->CloseBrowser(true);
        // 브라우저 종료 처리를 위해 펌핑
        PumpMessageLoop(50);
    }

    g_client = nullptr;
    g_renderHandler = nullptr;
    g_initialized = false;
    g_pendingURL.clear();

    // CefShutdown() / cef_unload_library()는 호출하지 않음.
    // CEF는 프로세스 종료 시 자동 정리.
}

EXPORT void CEF_DoMessageLoopWork() {
    if (!g_initialized) return;

    // CEF 메시지 루프 반복 펌핑 — 대기 태스크를 완전히 소진
    PumpMessageLoop(30);

    // 브라우저 생성 완료 후 대기 중인 URL 로드
    if (!g_pendingURL.empty() && g_client && g_client->GetBrowser()) {
        DW_Log("[Depthweaver] Loading pending URL: %s\n", g_pendingURL.c_str());
        g_client->GetBrowser()->GetMainFrame()->LoadURL(g_pendingURL);
        g_pendingURL.clear();
    }
}

EXPORT bool CEF_IsInitialized() {
    return g_initialized;
}

// ═══════════════════════════════════════════════════
// 페이지 로드
// ═══════════════════════════════════════════════════

EXPORT void CEF_LoadURL(const char* url) {
    if (!g_initialized || !url) return;

    DW_Log("[Depthweaver] CEF_LoadURL: %s\n", url);

    // 브라우저 미생성 시 큐에 저장 (CreateBrowser는 비동기)
    if (!g_client || !g_client->GetBrowser()) {
        g_pendingURL = url;
        DW_Log("[Depthweaver] Browser not ready, URL queued\n");
        return;
    }

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

// ═══════════════════════════════════════════════════
// 진단
// ═══════════════════════════════════════════════════

EXPORT void CEF_GetDiagnostics(int* buffer, int maxLen) {
    if (!buffer || maxLen < 4) return;
    buffer[0] = g_renderHandler ? g_renderHandler->GetPaintCount() : -1;
    buffer[1] = g_renderHandler ? g_renderHandler->GetViewRectCount() : -1;
    buffer[2] = (g_client && g_client->GetBrowser()) ? 1 : 0;
    buffer[3] = g_cefCoreInitialized ? 1 : 0;
}

// ═══════════════════════════════════════════════════
// 깊이 데이터 API (Phase 2)
// ═══════════════════════════════════════════════════

EXPORT bool CEF_HasNewDepthFrame() {
    return g_initialized && g_client && g_client->HasNewDepthFrame();
}

EXPORT bool CEF_GetDepthPixels(void* dest, int* outSize) {
    if (!g_initialized || !g_client || !dest || !outSize) return false;
    return g_client->CopyDepthPixels(static_cast<uint8_t*>(dest), outSize);
}

EXPORT int CEF_GetDepthFrameCount() {
    if (!g_initialized || !g_client) return 0;
    return g_client->GetDepthFrameCount();
}

// ═══════════════════════════════════════════════════
// 로그 시스템 API
// ═══════════════════════════════════════════════════

EXPORT int CEF_GetLogMessages(char* buffer, int maxLen) {
    if (!buffer || maxLen <= 0) return 0;

    std::lock_guard<std::mutex> lock(g_logMutex);
    if (g_logBuffer.empty()) return 0;

    int copyLen = std::min(static_cast<int>(g_logBuffer.size()), maxLen - 1);
    memcpy(buffer, g_logBuffer.c_str(), copyLen);
    buffer[copyLen] = '\0';
    g_logBuffer.clear();
    return copyLen;
}
