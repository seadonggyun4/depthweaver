// NativePlugin/src/browser_client.cpp

#include "browser_client.h"
#include <cstring>
#include <cstdlib>

// cef_plugin.cpp에 정의된 로그 함수
extern void DW_Log(const char* fmt, ...);

// ═══════════════════════════════════════════════════
// Base64 디코딩 (RFC 4648)
// ═══════════════════════════════════════════════════

static const uint8_t B64_DECODE_TABLE[256] = {
    64,64,64,64,64,64,64,64,64,64,64,64,64,64,64,64,
    64,64,64,64,64,64,64,64,64,64,64,64,64,64,64,64,
    64,64,64,64,64,64,64,64,64,64,64,62,64,64,64,63,
    52,53,54,55,56,57,58,59,60,61,64,64,64,65,64,64,
    64, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9,10,11,12,13,14,
    15,16,17,18,19,20,21,22,23,24,25,64,64,64,64,64,
    64,26,27,28,29,30,31,32,33,34,35,36,37,38,39,40,
    41,42,43,44,45,46,47,48,49,50,51,64,64,64,64,64,
    64,64,64,64,64,64,64,64,64,64,64,64,64,64,64,64,
    64,64,64,64,64,64,64,64,64,64,64,64,64,64,64,64,
    64,64,64,64,64,64,64,64,64,64,64,64,64,64,64,64,
    64,64,64,64,64,64,64,64,64,64,64,64,64,64,64,64,
    64,64,64,64,64,64,64,64,64,64,64,64,64,64,64,64,
    64,64,64,64,64,64,64,64,64,64,64,64,64,64,64,64,
    64,64,64,64,64,64,64,64,64,64,64,64,64,64,64,64,
    64,64,64,64,64,64,64,64,64,64,64,64,64,64,64,64
};

std::vector<uint8_t> BrowserClient::Base64Decode(const std::string& encoded) {
    std::vector<uint8_t> result;
    size_t len = encoded.size();
    if (len == 0) return result;

    // 패딩 제외 길이
    size_t padding = 0;
    if (len > 0 && encoded[len - 1] == '=') padding++;
    if (len > 1 && encoded[len - 2] == '=') padding++;

    result.reserve((len / 4) * 3 - padding);

    uint32_t buf = 0;
    int bits = 0;

    for (size_t i = 0; i < len; i++) {
        uint8_t c = static_cast<uint8_t>(encoded[i]);
        uint8_t val = B64_DECODE_TABLE[c];
        if (val > 63) continue; // 패딩 '=' 또는 잘못된 문자 건너뛰기

        buf = (buf << 6) | val;
        bits += 6;

        if (bits >= 8) {
            bits -= 8;
            result.push_back(static_cast<uint8_t>((buf >> bits) & 0xFF));
        }
    }

    return result;
}

BrowserClient::BrowserClient(CefRefPtr<OffscreenRenderHandler> renderHandler)
    : renderHandler_(renderHandler) {}

void BrowserClient::OnAfterCreated(CefRefPtr<CefBrowser> browser) {
    browser_ = browser;
    DW_Log("[Depthweaver] OnAfterCreated: browser ID=%d\n", browser->GetIdentifier());
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
    DW_Log("[Depthweaver] OnLoadEnd: status=%d isMain=%d url=%s\n",
            httpStatusCode, frame->IsMain(), frame->GetURL().ToString().c_str());
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
    DW_Log("[Depthweaver] OnLoadError: code=%d text=%s url=%s\n",
            (int)errorCode, errorText.ToString().c_str(), failedUrl.ToString().c_str());
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

// ═══════════════════════════════════════════════════
// Phase 2: OnConsoleMessage — 깊이 데이터 수신
// ═══════════════════════════════════════════════════
//
// depth-extractor.js가 깊이 맵 렌더링 후:
//   console.log("__DEPTH:512:<base64 R-channel data>")
// 형식으로 전송한다.
//
// 프로토콜:
//   __DEPTH:<size>:<base64>
//   - size: 깊이 맵 한 변 크기 (예: 512)
//   - base64: size×size 바이트의 R-channel 데이터 (그레이스케일)

bool BrowserClient::OnConsoleMessage(
    CefRefPtr<CefBrowser> browser,
    cef_log_severity_t level,
    const CefString& message,
    const CefString& source,
    int line)
{
    std::string msg = message.ToString();

    // __DEPTH: 프리픽스 감지
    const char* DEPTH_PREFIX = "__DEPTH:";
    const size_t PREFIX_LEN = 8;

    if (msg.size() > PREFIX_LEN && msg.compare(0, PREFIX_LEN, DEPTH_PREFIX) == 0) {
        // __DEPTH:<size>:<base64> 파싱
        size_t secondColon = msg.find(':', PREFIX_LEN);

        if (secondColon == std::string::npos) {
            DW_Log("[Depthweaver] Invalid depth message format (missing second colon)\n");
            return true; // 메시지 소비
        }

        int size = std::atoi(msg.substr(PREFIX_LEN, secondColon - PREFIX_LEN).c_str());
        if (size <= 0 || size > 4096) {
            DW_Log("[Depthweaver] Invalid depth size: %d\n", size);
            return true;
        }

        std::string base64Data = msg.substr(secondColon + 1);
        std::vector<uint8_t> decoded = Base64Decode(base64Data);

        int expectedSize = size * size;
        if (static_cast<int>(decoded.size()) != expectedSize) {
            DW_Log("[Depthweaver] Depth data size mismatch: expected=%d got=%d\n",
                    expectedSize, (int)decoded.size());
            return true;
        }

        // 깊이 버퍼에 저장
        {
            std::lock_guard<std::mutex> lock(depthMutex_);
            depthSize_ = size;
            depthBuffer_ = std::move(decoded);
        }
        depthReady_.store(true, std::memory_order_release);
        depthFrameCount_++;

        if (depthFrameCount_ <= 3) {
            DW_Log("[Depthweaver] Depth frame #%d received: %dx%d (%d bytes)\n",
                    depthFrameCount_, size, size, expectedSize);
        }

        return true; // 깊이 메시지는 콘솔에 표시하지 않음
    }

    // 일반 콘솔 메시지는 기본 처리 (false 반환)
    return false;
}

// ═══════════════════════════════════════════════════
// Phase 2: 깊이 데이터 접근
// ═══════════════════════════════════════════════════

bool BrowserClient::HasNewDepthFrame() const {
    return depthReady_.load(std::memory_order_acquire);
}

bool BrowserClient::CopyDepthPixels(uint8_t* dest, int* outSize) {
    if (!dest || !outSize) return false;

    std::lock_guard<std::mutex> lock(depthMutex_);
    if (depthBuffer_.empty()) return false;

    memcpy(dest, depthBuffer_.data(), depthBuffer_.size());
    *outSize = depthSize_;
    depthReady_.store(false, std::memory_order_release);
    return true;
}
