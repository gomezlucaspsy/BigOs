if (NOT WIN32)
    message(FATAL_ERROR "bigos-desktop currently supports Windows only.")
endif()

if (NOT DEFINED WEBVIEW2_SDK_PATH)
    set(WEBVIEW2_SDK_PATH "$ENV{WEBVIEW2_SDK_PATH}")
endif()

if (NOT WEBVIEW2_SDK_PATH)
    file(GLOB WEBVIEW2_SDK_CANDIDATES "${CMAKE_SOURCE_DIR}/deps/Microsoft.Web.WebView2.*")
    foreach(candidate ${WEBVIEW2_SDK_CANDIDATES})
        if (IS_DIRECTORY "${candidate}" AND EXISTS "${candidate}/build/native/include/WebView2.h")
            set(WEBVIEW2_SDK_PATH "${candidate}")
            break()
        endif()
    endforeach()
endif()

find_path(WEBVIEW2_INCLUDE_DIR WebView2.h
    HINTS
        "${WEBVIEW2_SDK_PATH}/build/native/include"
        "${WEBVIEW2_SDK_PATH}/include"
)

find_library(WEBVIEW2_LOADER_LIBRARY WebView2LoaderStatic
    HINTS
        "${WEBVIEW2_SDK_PATH}/build/native/x64"
        "${WEBVIEW2_SDK_PATH}/lib/x64"
)

if (NOT WEBVIEW2_INCLUDE_DIR OR NOT WEBVIEW2_LOADER_LIBRARY)
    message(FATAL_ERROR
        "WebView2 SDK not found. Set WEBVIEW2_SDK_PATH to the SDK root.\n"
        "Expected include at <SDK>/build/native/include/WebView2.h and\n"
        "lib at <SDK>/build/native/x64/WebView2LoaderStatic.lib"
    )
endif()

add_executable(bigos-desktop WIN32
    src/main.cpp
)

target_link_libraries(bigos-desktop
    PRIVATE
        bigos_core
        ${WEBVIEW2_LOADER_LIBRARY}
)

target_include_directories(bigos-desktop
    PRIVATE
        ${WEBVIEW2_INCLUDE_DIR}
)

target_compile_definitions(bigos-desktop PRIVATE UNICODE _UNICODE NOMINMAX WIN32_LEAN_AND_MEAN)

target_link_libraries(bigos-desktop PRIVATE user32 ole32)
