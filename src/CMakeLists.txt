
add_executable(${PROJECT_NAME} main.cpp)

target_include_directories(${PROJECT_NAME} PRIVATE ${glm_SOURCE_DIR})

target_link_libraries(${PROJECT_NAME} PRIVATE
    glfw
    glad_gl_core_46
    glm::glm)