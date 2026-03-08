// NativePlugin/src/browser_app.h
// ══════════════════════════════════════════════════════════════════════
// Depthweaver Phase 1 — CEF 애플리케이션 핸들러
// ══════════════════════════════════════════════════════════════════════

#pragma once

#include "include/cef_app.h"
#include "include/cef_command_line.h"
#include <atomic>
#include <cstdio>

// 전역 플래그: CEF가 즉시 펌핑을 요청했는지 여부
extern std::atomic<bool> g_pumpNeeded;

// cef_plugin.cpp에 정의된 로그 함수
extern void DW_Log(const char* fmt, ...);

class BrowserApp : public CefApp,
                   public CefBrowserProcessHandler {
public:
    BrowserApp() = default;

    // CefApp — 명령줄 스위치 추가
    void OnBeforeCommandLineProcessing(
        const CefString& process_type,
        CefRefPtr<CefCommandLine> command_line) override
    {
        // ═══ 단일 프로세스 모드 ═══
        // 렌더러, GPU, 네트워크 모든 서브프로세스를 브라우저 프로세스 내부에서 실행.
        // macOS에서 Helper 서브프로세스가 코드 서명/Gatekeeper로 인해
        // 시작되지 않는 문제를 완전히 우회한다.
        command_line->AppendSwitch("single-process");

        // GPU 프로세스를 브라우저 프로세스 내부에서 실행
        command_line->AppendSwitch("in-process-gpu");

        // macOS 키체인 접근 문제 방지
        command_line->AppendSwitch("use-mock-keychain");

        // 샌드박스 비활성화 (Unity 플러그인 환경)
        command_line->AppendSwitch("disable-gpu-sandbox");
        command_line->AppendSwitch("no-sandbox");

        DW_Log("[Depthweaver] Command line switches added: "
                "single-process, in-process-gpu, use-mock-keychain, "
                "disable-gpu-sandbox, no-sandbox\n");
    }

    CefRefPtr<CefBrowserProcessHandler> GetBrowserProcessHandler() override {
        return this;
    }

    // CefBrowserProcessHandler
    void OnContextInitialized() override {
        DW_Log("[Depthweaver] OnContextInitialized called\n");
    }

    // external_message_pump = true 일 때 호출됨.
    // delay_ms=0이면 즉시 펌핑 필요, >0이면 지연 후 펌핑 필요.
    void OnScheduleMessagePumpWork(int64_t delay_ms) override {
        g_pumpNeeded.store(true, std::memory_order_release);
    }

private:
    IMPLEMENT_REFCOUNTING(BrowserApp);
    DISALLOW_COPY_AND_ASSIGN(BrowserApp);
};
