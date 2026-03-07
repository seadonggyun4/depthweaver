// NativePlugin/src/render_handler.cpp

#include "render_handler.h"

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
}

void OffscreenRenderHandler::OnPaint(
    CefRefPtr<CefBrowser> browser,
    PaintElementType type,
    const RectList& dirtyRects,
    const void* buffer,
    int width, int height)
{
    if (type != PET_VIEW) return;
    if (width != width_ || height != height_) return;

    // 더티 영역만 backBuffer에 복사
    {
        std::lock_guard<std::mutex> lock(bufferMutex_);

        int stride = width * 4;

        for (const auto& rect : dirtyRects) {
            for (int y = rect.y; y < rect.y + rect.height; y++) {
                int srcOffset = y * stride + rect.x * 4;
                int copySize = rect.width * 4;
                memcpy(
                    backBuffer_ + srcOffset,
                    static_cast<const uint8_t*>(buffer) + srcOffset,
                    copySize
                );
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
