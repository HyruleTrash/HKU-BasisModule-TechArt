include(FetchContent)

set(GLFW_VERSION 3.4)
set(GLAD_VERSION "v2.0.8")
set(GLM_VERSION 1.0.1)

#GLFW
FetchContent_Declare(
    GLFW
    GIT_REPOSITORY https://github.com/glfw/glfw.git
    GIT_TAG ${GLFW_VERSION}
    GIT_SHALLOW TRUE
)

set(GLFW_BUILD_EXAMPLES OFF CACHE INTERNAL "")
set(GLFW_BUILD_TESTS OFF CACHE INTERNAL "")
set(GLFW_BUILD_DOCS OFF CACHE INTERNAL "")
set(GLFW_INSTALL OFF CACHE INTERNAL "")

FetchContent_MakeAvailable(glfw)

# GLAD
# 1. Find the Python interpreter
find_package(Python3 REQUIRED COMPONENTS Interpreter)

# 2. Try to install jinja2 automatically
message(STATUS "Ensuring Python dependencies (jinja2, click) are installed...")
execute_process(
    COMMAND ${Python3_EXECUTABLE} -m pip install jinja2 click
    RESULT_VARIABLE PIP_RESULT
    OUTPUT_QUIET
    ERROR_QUIET
)

if(NOT PIP_RESULT EQUAL 0)
    message(FATAL_ERROR "Failed to install jinja2, click via pip.")
endif()

FetchContent_Declare(
    glad
    GIT_REPOSITORY https://github.com/Dav1dde/glad.git
    GIT_TAG        ${GLAD_VERSION}
    GIT_PROGRESS   TRUE
    SOURCE_SUBDIR  cmake
)

FetchContent_MakeAvailable(glad)
glad_add_library(glad_gl_core_46 STATIC REPRODUCIBLE LOADER API gl:core=4.6)

#GLM math library
FetchContent_Declare(
    glm
    GIT_REPOSITORY https://github.com/g-truc/glm.git
    GIT_TAG ${GLM_VERSION}
    GIT_SHALLOW TRUE
)

FetchContent_MakeAvailable(glm)