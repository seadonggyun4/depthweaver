# NativePlugin/CMakeLists.txt
# ══════════════════════════════════════════════════════════════════════
# Depthweaver Phase 1 — CEF 네이티브 플러그인 + Helper 앱 빌드 시스템
# ══════════════════════════════════════════════════════════════════════
#
# 빌드 방법:
#
# Windows:
#   cd NativePlugin && mkdir build/windows && cd build/windows
#   cmake ../.. -G "Visual Studio 17 2022" -A x64 -DCEF_ROOT="C:/path/to/cef_binary"
#   cmake --build . --config Release
#
# macOS (Apple Silicon):
#   cd NativePlugin && mkdir build/macos && cd build/macos
#   cmake ../.. -DCEF_ROOT="/path/to/cef_binary" -DCMAKE_OSX_ARCHITECTURES=arm64
#   cmake --build . --config Release
#
# CEF 바이너리 다운로드: https://cef-builds.spotifycdn.com/index.html

cmake_minimum_required(VERSION 3.19)
project(cef_plugin LANGUAGES CXX OBJCXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# 출력 경로 변수
set(UNITY_PLUGIN_DIR "${CMAKE_SOURCE_DIR}/../Assets/Plugins/CEF")

# ═══════════════════════════════════════════════════
# CEF 경로 설정
# ═══════════════════════════════════════════════════

set(CEF_ROOT "${CMAKE_SOURCE_DIR}/../cef_binary" CACHE PATH "CEF binary distribution path")

if(NOT EXISTS "${CEF_ROOT}")
    message(FATAL_ERROR
        "CEF binary distribution not found at: ${CEF_ROOT}\n"
        "Download from: https://cef-builds.spotifycdn.com/index.html\n"
        "Set -DCEF_ROOT=/path/to/cef_binary"
    )
endif()

# ═══════════════════════════════════════════════════
# libcef_dll_wrapper (CEF C++ 래퍼 정적 라이브러리)
# ═══════════════════════════════════════════════════

file(GLOB_RECURSE CEF_WRAPPER_SOURCES
    "${CEF_ROOT}/libcef_dll/*.cc"
    "${CEF_ROOT}/libcef_dll/*.cpp"
    "${CEF_ROOT}/libcef_dll/*.mm"
)

add_library(libcef_dll_wrapper STATIC ${CEF_WRAPPER_SOURCES})

target_include_directories(libcef_dll_wrapper PUBLIC
    ${CEF_ROOT}
    ${CEF_ROOT}/include
)

target_compile_features(libcef_dll_wrapper PUBLIC cxx_std_20)
target_compile_definitions(libcef_dll_wrapper PRIVATE WRAPPING_CEF_SHARED)

if(NOT MSVC)
    target_compile_options(libcef_dll_wrapper PRIVATE -Wno-unused-parameter -Wno-undefined-var-template)
endif()

# ═══════════════════════════════════════════════════
# 메인 플러그인 소스 파일
# ═══════════════════════════════════════════════════

set(SOURCES
    src/cef_plugin.h
    src/cef_plugin.cpp
    src/browser_app.h
    src/browser_app.cpp
    src/render_handler.h
    src/render_handler.cpp
    src/browser_client.h
    src/browser_client.cpp
)

# ═══════════════════════════════════════════════════
# 메인 공유 라이브러리 빌드 (cef_plugin)
# ═══════════════════════════════════════════════════

add_library(cef_plugin SHARED ${SOURCES})

target_include_directories(cef_plugin PRIVATE
    ${CEF_ROOT}
    ${CEF_ROOT}/include
    src/
)

target_link_libraries(cef_plugin PRIVATE libcef_dll_wrapper)
target_compile_definitions(cef_plugin PRIVATE USING_CEF_SHARED)

# ═══════════════════════════════════════════════════
# CEF Helper 앱 (macOS 전용 — 서브프로세스)
# ═══════════════════════════════════════════════════

if(APPLE)
    add_executable(cef_helper src/cef_helper_main.cpp)

    target_include_directories(cef_helper PRIVATE
        ${CEF_ROOT}
        ${CEF_ROOT}/include
    )

    target_link_libraries(cef_helper PRIVATE
        libcef_dll_wrapper
        "${CEF_ROOT}/Release/Chromium Embedded Framework.framework/Chromium Embedded Framework"
    )

    target_compile_definitions(cef_helper PRIVATE USING_CEF_SHARED)

    # .app 번들로 빌드
    set_target_properties(cef_helper PROPERTIES
        MACOSX_BUNDLE TRUE
        MACOSX_BUNDLE_INFO_PLIST "${CMAKE_SOURCE_DIR}/src/helper/Info.plist"
        RUNTIME_OUTPUT_DIRECTORY "${UNITY_PLUGIN_DIR}/macOS/arm64"
    )

    if(NOT MSVC)
        target_compile_options(cef_helper PRIVATE -Wall -Wno-unused-parameter)
    endif()
endif()

# ═══════════════════════════════════════════════════
# 플랫폼별 링크 및 출력 경로
# ═══════════════════════════════════════════════════

if(WIN32)
    target_link_libraries(cef_plugin PRIVATE
        "${CEF_ROOT}/Release/libcef.lib"
    )
    set_target_properties(cef_plugin PROPERTIES
        RUNTIME_OUTPUT_DIRECTORY_RELEASE "${UNITY_PLUGIN_DIR}/Windows/x86_64"
        RUNTIME_OUTPUT_DIRECTORY_DEBUG "${UNITY_PLUGIN_DIR}/Windows/x86_64"
    )
endif()

if(APPLE)
    target_link_libraries(cef_plugin PRIVATE
        "${CEF_ROOT}/Release/Chromium Embedded Framework.framework/Chromium Embedded Framework"
    )

    set_target_properties(cef_plugin PROPERTIES
        LIBRARY_OUTPUT_DIRECTORY "${UNITY_PLUGIN_DIR}/macOS/arm64"
        SUFFIX ".bundle"
        MACOSX_BUNDLE FALSE
    )
endif()

# ═══════════════════════════════════════════════════
# 컴파일 옵션
# ═══════════════════════════════════════════════════

if(MSVC)
    target_compile_options(cef_plugin PRIVATE /W3 /WX-)
else()
    target_compile_options(cef_plugin PRIVATE -Wall -Wno-unused-parameter)
endif()

# ═══════════════════════════════════════════════════
# macOS 빌드 후 처리 (프레임워크 참조 수정)
# ═══════════════════════════════════════════════════

if(APPLE)
    # 플러그인: @loader_path로 프레임워크 참조 수정
    add_custom_command(TARGET cef_plugin POST_BUILD
        COMMAND install_name_tool -change
            "@executable_path/../Frameworks/Chromium Embedded Framework.framework/Chromium Embedded Framework"
            "@loader_path/Chromium Embedded Framework.framework/Chromium Embedded Framework"
            "$<TARGET_FILE:cef_plugin>"
        COMMENT "Fixing framework path in cef_plugin"
    )

    # Helper: @executable_path 기반으로 프레임워크 참조 수정
    # Helper 실행 파일 위치: cef_helper.app/Contents/MacOS/cef_helper
    # 프레임워크 위치: ../../../Chromium Embedded Framework.framework/
    add_custom_command(TARGET cef_helper POST_BUILD
        COMMAND install_name_tool -change
            "@executable_path/../Frameworks/Chromium Embedded Framework.framework/Chromium Embedded Framework"
            "@executable_path/../../../Chromium Embedded Framework.framework/Chromium Embedded Framework"
            "$<TARGET_BUNDLE_CONTENT_DIR:cef_helper>/MacOS/cef_helper"
        COMMENT "Fixing framework path in cef_helper"
    )

    # ═══════════════════════════════════════════════════
    # macOS 코드 서명 (ad-hoc) — CEF IPC 검증 오류 방지
    # ═══════════════════════════════════════════════════

    # 플러그인 번들 ad-hoc 서명
    add_custom_command(TARGET cef_plugin POST_BUILD
        COMMAND codesign --force --sign - "$<TARGET_FILE:cef_plugin>"
        COMMENT "Ad-hoc codesigning cef_plugin.bundle"
    )

    # Unity .meta 파일 제거 → ad-hoc 서명 (macOS 코드 서명은 번들 내 비표준 파일을 거부함)
    add_custom_command(TARGET cef_helper POST_BUILD
        COMMAND find "${UNITY_PLUGIN_DIR}/macOS/arm64/cef_helper.app" -name "*.meta" -delete
        COMMAND codesign --force --sign -
            "${UNITY_PLUGIN_DIR}/macOS/arm64/cef_helper.app"
        COMMENT "Removing .meta files and codesigning cef_helper.app"
    )
endif()
