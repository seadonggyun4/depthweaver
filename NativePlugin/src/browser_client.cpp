// NativePlugin/src/browser_client.cpp

#include "browser_client.h"

BrowserClient::BrowserClient(CefRefPtr<OffscreenRenderHandler> renderHandler)
    : renderHandler_(renderHandler) {}

void BrowserClient::OnAfterCreated(CefRefPtr<CefBrowser> browser) {
    browser_ = browser;
}

void BrowserClient::OnLoadStart(CefRefPtr<CefBrowser> browser,
                                 CefRefPtr<CefFrame> frame,
                                 TransitionType transition_type) {
    if (frame->IsMain()) {
        isLoaded_ = false;
    }
}

void BrowserClient::OnLoadEnd(CefRefPtr<CefBrowser> browser,
                               CefRefPtr<CefFrame> frame,
                               int httpStatusCode) {
    if (frame->IsMain()) {
        isLoaded_ = true;
        InjectDepthExtractor(frame);
    }
}

void BrowserClient::OnLoadError(CefRefPtr<CefBrowser> browser,
                                 CefRefPtr<CefFrame> frame,
                                 ErrorCode errorCode,
                                 const CefString& errorText,
                                 const CefString& failedUrl) {
    lastError_ = errorText.ToString();
}

void BrowserClient::EvaluateJS(const std::string& script, int callbackId) {
    if (!browser_ || !browser_->GetMainFrame()) return;

    // fire-and-forget 실행 (결과는 CEF V8 콜백으로 수신해야 하지만,
    // 현재는 간소화된 구현으로 ExecuteJavaScript 사용)
    browser_->GetMainFrame()->ExecuteJavaScript(
        script, browser_->GetMainFrame()->GetURL(), 0
    );
}

bool BrowserClient::GetJSResult(int callbackId, std::string& result) {
    std::lock_guard<std::mutex> lock(jsResultsMutex_);
    auto it = jsResults_.find(callbackId);
    if (it != jsResults_.end()) {
        result = it->second;
        jsResults_.erase(it);
        return true;
    }
    return false;
}

void BrowserClient::InjectDepthExtractor(CefRefPtr<CefFrame> frame) {
    if (depthExtractorScript_.empty()) {
        // Phase 2 미구현 시 스텁
        frame->ExecuteJavaScript(
            "console.log('[UIShader] depth-extractor.js injection point');",
            frame->GetURL(), 0
        );
    } else {
        frame->ExecuteJavaScript(
            depthExtractorScript_,
            frame->GetURL(), 0
        );
    }
}
