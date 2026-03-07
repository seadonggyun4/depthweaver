// NativePlugin/src/browser_app.h
// ══════════════════════════════════════════════════════════════════════
// Depthweaver Phase 1 — CEF 애플리케이션 핸들러
// ══════════════════════════════════════════════════════════════════════

#pragma once

#include "include/cef_app.h"

class BrowserApp : public CefApp,
                   public CefBrowserProcessHandler {
public:
    BrowserApp() = default;

    // CefApp
    CefRefPtr<CefBrowserProcessHandler> GetBrowserProcessHandler() override {
        return this;
    }

    // CefBrowserProcessHandler
    void OnContextInitialized() override {}

private:
    IMPLEMENT_REFCOUNTING(BrowserApp);
    DISALLOW_COPY_AND_ASSIGN(BrowserApp);
};
