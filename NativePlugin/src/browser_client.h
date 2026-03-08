// NativePlugin/src/browser_client.h
// ══════════════════════════════════════════════════════════════════════
// Depthweaver Phase 1+2 — CEF 클라이언트 (브라우저 이벤트 핸들러)
// ══════════════════════════════════════════════════════════════════════
//
// CefClient를 구현하여 렌더 핸들러, 라이프스팬, 로드 이벤트를 관리한다.
// 페이지 로드 완료 시 depth-extractor.js 자동 주입.
//
// Phase 2 확장:
//   CefDisplayHandler를 추가하여 OnConsoleMessage로 깊이 데이터를 수신한다.
//   JS의 depth-extractor.js가 console.log("__DEPTH:512:<base64>") 형태로
//   R-channel 깊이 데이터를 전송하면, C++에서 디코딩하여 버퍼에 저장한다.

#pragma once

#include "include/cef_client.h"
#include "include/cef_life_span_handler.h"
#include "include/cef_load_handler.h"
#include "include/cef_display_handler.h"
#include "render_handler.h"
#include <unordered_map>
#include <mutex>
#include <string>
#include <atomic>
#include <vector>

class BrowserClient : public CefClient,
                      public CefLifeSpanHandler,
                      public CefLoadHandler,
                      public CefDisplayHandler {
public:
    explicit BrowserClient(CefRefPtr<OffscreenRenderHandler> renderHandler);

    // CefClient
    CefRefPtr<CefRenderHandler> GetRenderHandler() override { return renderHandler_; }
    CefRefPtr<CefLifeSpanHandler> GetLifeSpanHandler() override { return this; }
    CefRefPtr<CefLoadHandler> GetLoadHandler() override { return this; }
    CefRefPtr<CefDisplayHandler> GetDisplayHandler() override { return this; }

    // CefLifeSpanHandler
    void OnAfterCreated(CefRefPtr<CefBrowser> browser) override;

    // CefLoadHandler
    void OnLoadStart(CefRefPtr<CefBrowser> browser,
                     CefRefPtr<CefFrame> frame,
                     TransitionType transition_type) override;
    void OnLoadEnd(CefRefPtr<CefBrowser> browser,
                   CefRefPtr<CefFrame> frame,
                   int httpStatusCode) override;
    void OnLoadError(CefRefPtr<CefBrowser> browser,
                     CefRefPtr<CefFrame> frame,
                     ErrorCode errorCode,
                     const CefString& errorText,
                     const CefString& failedUrl) override;

    // CefDisplayHandler — 깊이 데이터 수신 (Phase 2)
    bool OnConsoleMessage(CefRefPtr<CefBrowser> browser,
                          cef_log_severity_t level,
                          const CefString& message,
                          const CefString& source,
                          int line) override;

    // 접근자
    CefRefPtr<CefBrowser> GetBrowser() { return browser_; }
    bool IsLoaded() const { return isLoaded_; }
    std::string GetLastError() const { return lastError_; }

    // JavaScript 평가
    void EvaluateJS(const std::string& script, int callbackId);
    bool GetJSResult(int callbackId, std::string& result);

    // depth-extractor.js 주입 스크립트 설정
    void SetDepthExtractorScript(const std::string& script) { depthExtractorScript_ = script; }

    // ═══════════════════════════════════════════════════
    // Phase 2: 깊이 데이터 접근 API
    // ═══════════════════════════════════════════════════

    /// 새로운 깊이 프레임이 있는지 확인
    bool HasNewDepthFrame() const;

    /// 깊이 픽셀 데이터를 복사 (R-channel only, size×size 바이트)
    /// 반환: 복사 성공 여부
    bool CopyDepthPixels(uint8_t* dest, int* outSize);

    /// 깊이 프레임 수신 횟수 (진단용)
    int GetDepthFrameCount() const { return depthFrameCount_; }

private:
    void InjectDepthExtractor(CefRefPtr<CefFrame> frame);

    // base64 디코딩
    static std::vector<uint8_t> Base64Decode(const std::string& encoded);

    CefRefPtr<OffscreenRenderHandler> renderHandler_;
    CefRefPtr<CefBrowser> browser_;
    bool isLoaded_ = false;
    std::string lastError_;

    // depth-extractor.js 스크립트
    std::string depthExtractorScript_;

    // JS 결과 관리
    std::unordered_map<int, std::string> jsResults_;
    std::mutex jsResultsMutex_;

    // ═══════════════════════════════════════════════════
    // Phase 2: 깊이 데이터 버퍼
    // ═══════════════════════════════════════════════════

    std::vector<uint8_t> depthBuffer_;    // R-channel 깊이 데이터
    int depthSize_ = 0;                   // 깊이 맵 한 변 크기 (예: 512)
    std::mutex depthMutex_;
    std::atomic<bool> depthReady_{false};
    int depthFrameCount_ = 0;

    IMPLEMENT_REFCOUNTING(BrowserClient);
    DISALLOW_COPY_AND_ASSIGN(BrowserClient);
};
