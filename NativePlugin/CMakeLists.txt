# NativePlugin/CMakeLists.txt
# ══════════════════════════════════════════════════════════════════════
# Depthweaver Phase 1 — CEF 네이티브 플러그인 빌드 시스템
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
project(cef_plugin LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

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
# 소스 파일
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
# 공유 라이브러리 빌드
# ═══════════════════════════════════════════════════

add_library(cef_plugin SHARED ${SOURCES})

target_include_directories(cef_plugin PRIVATE
    ${CEF_ROOT}
    ${CEF_ROOT}/include
    src/
)

# ═══════════════════════════════════════════════════
# 플랫폼별 링크 및 출력 경로
# ═══════════════════════════════════════════════════

if(WIN32)
    target_link_libraries(cef_plugin
        "${CEF_ROOT}/Release/libcef.lib"
    )
    set_target_properties(cef_plugin PROPERTIES
        RUNTIME_OUTPUT_DIRECTORY_RELEASE "${CMAKE_SOURCE_DIR}/../Assets/Plugins/CEF/Windows/x86_64"
        RUNTIME_OUTPUT_DIRECTORY_DEBUG "${CMAKE_SOURCE_DIR}/../Assets/Plugins/CEF/Windows/x86_64"
    )
endif()

if(APPLE)
    find_library(CEF_FRAMEWORK
        "Chromium Embedded Framework"
        PATHS "${CEF_ROOT}/Release"
    )
    if(CEF_FRAMEWORK)
        target_link_libraries(cef_plugin ${CEF_FRAMEWORK})
    else()
        message(WARNING "CEF Framework not found, link manually")
    endif()

    set_target_properties(cef_plugin PROPERTIES
        LIBRARY_OUTPUT_DIRECTORY "${CMAKE_SOURCE_DIR}/../Assets/Plugins/CEF/macOS/arm64"
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
