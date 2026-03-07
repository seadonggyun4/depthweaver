// NativePlugin/src/browser_client.h
// ══════════════════════════════════════════════════════════════════════
// Depthweaver Phase 1 — CEF 클라이언트 (브라우저 이벤트 핸들러)
// ══════════════════════════════════════════════════════════════════════
//
// CefClient를 구현하여 렌더 핸들러, 라이프스팬, 로드 이벤트를 관리한다.
// 페이지 로드 완료 시 depth-extractor.js 자동 주입 (Phase 2 확장 지점).

#pragma once

#include "include/cef_client.h"
#include "include/cef_life_span_handler.h"
#include "include/cef_load_handler.h"
#include "render_handler.h"
#include <unordered_map>
#include <mutex>
#include <string>

class BrowserClient : public CefClient,
                      public CefLifeSpanHandler,
                      public CefLoadHandler {
public:
    explicit BrowserClient(CefRefPtr<OffscreenRenderHandler> renderHandler);

    // CefClient
    CefRefPtr<CefRenderHandler> GetRenderHandler() override { return renderHandler_; }
    CefRefPtr<CefLifeSpanHandler> GetLifeSpanHandler() override { return this; }
    CefRefPtr<CefLoadHandler> GetLoadHandler() override { return this; }

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

    // 접근자
    CefRefPtr<CefBrowser> GetBrowser() { return browser_; }
    bool IsLoaded() const { return isLoaded_; }
    std::string GetLastError() const { return lastError_; }

    // JavaScript 평가
    void EvaluateJS(const std::string& script, int callbackId);
    bool GetJSResult(int callbackId, std::string& result);

    // depth-extractor.js 주입 스크립트 설정
    void SetDepthExtractorScript(const std::string& script) { depthExtractorScript_ = script; }

private:
    void InjectDepthExtractor(CefRefPtr<CefFrame> frame);

    CefRefPtr<OffscreenRenderHandler> renderHandler_;
    CefRefPtr<CefBrowser> browser_;
    bool isLoaded_ = false;
    std::string lastError_;

    // depth-extractor.js 스크립트
    std::string depthExtractorScript_;

    // JS 결과 관리
    std::unordered_map<int, std::string> jsResults_;
    std::mutex jsResultsMutex_;

    IMPLEMENT_REFCOUNTING(BrowserClient);
    DISALLOW_COPY_AND_ASSIGN(BrowserClient);
};
