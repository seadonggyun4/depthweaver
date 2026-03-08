// NativePlugin/src/render_handler.h
// ══════════════════════════════════════════════════════════════════════
// Depthweaver Phase 1 — CEF 오프스크린 렌더 핸들러
// ══════════════════════════════════════════════════════════════════════
//
// CefRenderHandler를 구현하여 오프스크린 렌더링된 프레임을 수신한다.
// 이중 버퍼링(front/back)으로 스레드 안전한 프레임 전달을 보장한다.
// OnPaint에서 더티 영역만 복사하여 대역폭을 최소화한다.

#pragma once

#include "include/cef_render_handler.h"
#include <atomic>
#include <mutex>
#include <vector>
#include <chrono>
#include <cstring>

struct DirtyRect {
    int x, y, width, height;
};

class OffscreenRenderHandler : public CefRenderHandler {
public:
    OffscreenRenderHandler(int width, int height);
    ~OffscreenRenderHandler();

    // CefRenderHandler
    void GetViewRect(CefRefPtr<CefBrowser> browser, CefRect& rect) override;
    bool GetScreenInfo(CefRefPtr<CefBrowser> browser, CefScreenInfo& screen_info) override;
    void OnPaint(CefRefPtr<CefBrowser> browser,
                 PaintElementType type,
                 const RectList& dirtyRects,
                 const void* buffer,
                 int width, int height) override;

    // 프레임 접근
    bool HasNewFrame() const;
    void CopyToDestination(void* dest, int* outWidth, int* outHeight);
    const void* GetFrontBufferPtr() const;
    double GetFrameTimestamp() const;

    // 더티 영역 접근
    int GetDirtyRects(int* buffer, int maxRects);

    // 리사이즈
    void Resize(int newWidth, int newHeight);

    int GetWidth() const { return width_; }
    int GetHeight() const { return height_; }

    // 진단 카운터
    int GetPaintCount() const { return paintCount_; }
    int GetViewRectCount() const { return viewRectCount_; }

private:
    int width_, height_;
    uint8_t* frontBuffer_;
    uint8_t* backBuffer_;
    mutable std::mutex bufferMutex_;
    std::atomic<bool> frameReady_;
    double frameTimestamp_ = 0;

    // 더티 영역 추적
    std::vector<DirtyRect> dirtyRects_;
    std::mutex dirtyMutex_;

    double GetCurrentTimeMs() const;

    int paintCount_ = 0;
    int viewRectCount_ = 0;
    int sizeMismatchCount_ = 0;

    IMPLEMENT_REFCOUNTING(OffscreenRenderHandler);
    DISALLOW_COPY_AND_ASSIGN(OffscreenRenderHandler);
};
