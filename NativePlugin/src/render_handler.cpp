// NativePlugin/src/render_handler.cpp

#include "render_handler.h"
#include <cstdarg>

// cef_plugin.cpp에 정의된 로그 함수 사용
extern void DW_Log(const char* fmt, ...);

OffscreenRenderHandler::OffscreenRenderHandler(int width, int height)
    : width_(width), height_(height), frameReady_(false)
{
    int bufferSize = width * height * 4; // BGRA32
    frontBuffer_ = new uint8_t[bufferSize];
    backBuffer_ = new uint8_t[bufferSize];
    memset(frontBuffer_, 0, bufferSize);
    memset(backBuffer_, 0, bufferSize);
}

OffscreenRenderHandler::~OffscreenRenderHandler() {
    delete[] frontBuffer_;
    delete[] backBuffer_;
}

void OffscreenRenderHandler::GetViewRect(CefRefPtr<CefBrowser> browser, CefRect& rect) {
    rect.Set(0, 0, width_, height_);
    viewRectCount_++;
    if (viewRectCount_ <= 5) {
        DW_Log("[Depthweaver RH] GetViewRect #%d: %dx%d\n",
                viewRectCount_, width_, height_);
    }
}

bool OffscreenRenderHandler::GetScreenInfo(CefRefPtr<CefBrowser> browser, CefScreenInfo& screen_info) {
    // device_scale_factor = 1.0 을 강제하여
    // macOS Retina에서 2x 렌더링을 방지한다.
    // 이 설정이 없으면 OnPaint에 width*2, height*2 크기가 전달되어
    // 사이즈 불일치로 프레임이 유실된다.
    screen_info.device_scale_factor = 1.0f;
    screen_info.depth = 32;
    screen_info.depth_per_component = 8;
    screen_info.is_monochrome = 0;
    screen_info.rect = CefRect(0, 0, width_, height_);
    screen_info.available_rect = screen_info.rect;
    return true;
}

void OffscreenRenderHandler::OnPaint(
    CefRefPtr<CefBrowser> browser,
    PaintElementType type,
    const RectList& dirtyRects,
    const void* buffer,
    int width, int height)
{
    paintCount_++;
    if (paintCount_ <= 3) {
        DW_Log("[Depthweaver RH] OnPaint #%d: type=%d size=%dx%d dirtyRects=%d\n",
                paintCount_, (int)type, width, height, (int)dirtyRects.size());
    }

    if (type != PET_VIEW) return;

    // 사이즈 불일치 시 동적 리사이즈 (DPI 변경 등 대응)
    if (width != width_ || height != height_) {
        sizeMismatchCount_++;
        Resize(width, height);
    }

    // 더티 영역만 backBuffer에 복사 (X-flip + Y-flip 적용)
    // CEF: top→bottom, left→right (원점=좌상단)
    // Unity 메시 UV: bottom→top, right→left (스크린 전면 기준)
    {
        std::lock_guard<std::mutex> lock(bufferMutex_);

        const uint8_t* src = static_cast<const uint8_t*>(buffer);

        for (const auto& rect : dirtyRects) {
            for (int y = rect.y; y < rect.y + rect.height; y++) {
                int flippedY = (height - 1 - y);
                for (int x = rect.x; x < rect.x + rect.width; x++) {
                    int flippedX = (width - 1 - x);
                    int srcIdx = (y * width + x) * 4;
                    int dstIdx = (flippedY * width + flippedX) * 4;
                    // BGRA 4바이트 복사
                    backBuffer_[dstIdx + 0] = src[srcIdx + 0];
                    backBuffer_[dstIdx + 1] = src[srcIdx + 1];
                    backBuffer_[dstIdx + 2] = src[srcIdx + 2];
                    backBuffer_[dstIdx + 3] = src[srcIdx + 3];
                }
            }
        }

        // front/back 스왑
        std::swap(frontBuffer_, backBuffer_);
        // backBuffer를 frontBuffer 내용으로 동기화 (다음 더티 업데이트의 베이스라인)
        memcpy(backBuffer_, frontBuffer_, width * height * 4);
    }

    // 더티 영역 기록
    {
        std::lock_guard<std::mutex> lock(dirtyMutex_);
        dirtyRects_.clear();
        for (const auto& rect : dirtyRects) {
            dirtyRects_.push_back({rect.x, rect.y, rect.width, rect.height});
        }
    }

    frameReady_.store(true, std::memory_order_release);
    frameTimestamp_ = GetCurrentTimeMs();
}

bool OffscreenRenderHandler::HasNewFrame() const {
    return frameReady_.load(std::memory_order_acquire);
}

void OffscreenRenderHandler::CopyToDestination(void* dest, int* outWidth, int* outHeight) {
    std::lock_guard<std::mutex> lock(bufferMutex_);
    memcpy(dest, frontBuffer_, width_ * height_ * 4);
    *outWidth = width_;
    *outHeight = height_;
    frameReady_.store(false, std::memory_order_release);
}

const void* OffscreenRenderHandler::GetFrontBufferPtr() const {
    return frontBuffer_;
}

double OffscreenRenderHandler::GetFrameTimestamp() const {
    return frameTimestamp_;
}

int OffscreenRenderHandler::GetDirtyRects(int* buffer, int maxRects) {
    std::lock_guard<std::mutex> lock(dirtyMutex_);
    int count = std::min(static_cast<int>(dirtyRects_.size()), maxRects);
    for (int i = 0; i < count; i++) {
        buffer[i * 4 + 0] = dirtyRects_[i].x;
        buffer[i * 4 + 1] = dirtyRects_[i].y;
        buffer[i * 4 + 2] = dirtyRects_[i].width;
        buffer[i * 4 + 3] = dirtyRects_[i].height;
    }
    return count;
}

void OffscreenRenderHandler::Resize(int newWidth, int newHeight) {
    std::lock_guard<std::mutex> lock(bufferMutex_);
    delete[] frontBuffer_;
    delete[] backBuffer_;
    width_ = newWidth;
    height_ = newHeight;
    int bufferSize = width_ * height_ * 4;
    frontBuffer_ = new uint8_t[bufferSize];
    backBuffer_ = new uint8_t[bufferSize];
    memset(frontBuffer_, 0, bufferSize);
    memset(backBuffer_, 0, bufferSize);
}

double OffscreenRenderHandler::GetCurrentTimeMs() const {
    using namespace std::chrono;
    return duration_cast<duration<double, std::milli>>(
        high_resolution_clock::now().time_since_epoch()
    ).count();
}
