// NativePlugin/src/cef_helper_main.cpp
// ══════════════════════════════════════════════════════════════════════
// Depthweaver Phase 1 — CEF Helper 서브프로세스 진입점
// ══════════════════════════════════════════════════════════════════════
//
// macOS에서 CEF는 다중 프로세스 아키텍처를 사용한다.
// 이 Helper 앱은 GPU, 렌더러, 유틸리티 서브프로세스를 담당한다.
// CefInitialize()가 서브프로세스를 생성할 때 이 실행 파일을 호출한다.

#include "include/cef_app.h"
#include "include/wrapper/cef_library_loader.h"
#include <cstdio>
#include <cstring>

int main(int argc, char* argv[]) {
    // 서브프로세스 타입 로깅 (디버그)
    const char* processType = "unknown";
    for (int i = 0; i < argc; i++) {
        if (strstr(argv[i], "--type=")) {
            processType = argv[i] + 7; // "--type=" 이후
            break;
        }
    }
    fprintf(stderr, "[Depthweaver Helper] Starting subprocess: type=%s pid=%d\n",
            processType, getpid());

    // macOS: CEF 프레임워크 동적 로드
    // Helper에서는 ../../.. 경로로 프레임워크를 탐색한다.
    // cef_helper.app/Contents/MacOS/cef_helper → ../../../ → arm64/
    CefScopedLibraryLoader library_loader;
    if (!library_loader.LoadInHelper()) {
        fprintf(stderr, "[Depthweaver Helper] FATAL: Failed to load CEF framework! type=%s\n",
                processType);
        return 1;
    }
    fprintf(stderr, "[Depthweaver Helper] CEF framework loaded successfully. type=%s\n",
            processType);

    CefMainArgs main_args(argc, argv);

    // 서브프로세스 실행 — 렌더러/GPU/유틸리티 등
    int exit_code = CefExecuteProcess(main_args, nullptr, nullptr);
    fprintf(stderr, "[Depthweaver Helper] Process exiting: type=%s exit_code=%d\n",
            processType, exit_code);
    return exit_code;
}
