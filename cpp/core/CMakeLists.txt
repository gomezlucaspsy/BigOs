add_library(bigos_core STATIC
    src/url.cpp
    src/tab_manager.cpp
)

target_include_directories(bigos_core
    PUBLIC
        ${CMAKE_CURRENT_SOURCE_DIR}/include
)
