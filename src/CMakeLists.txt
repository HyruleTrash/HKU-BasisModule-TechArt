
add_executable(${PROJECT_NAME} main.cpp utils.hpp)

#target_sources(${PROJECT_NAME} IDE doesnt need this
#    FILE_SET SHADERS
#        TYPE HEADERS
#        BASE_DIRS shaders
#        FILES
#        shaders/new_vert.glsl
#        shaders/new_frag.glsl
#)

add_custom_command(TARGET ${PROJECT_NAME} POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E copy_directory
    "${CMAKE_CURRENT_SOURCE_DIR}/shaders"
    "$<TARGET_FILE_DIR:${PROJECT_NAME}>/shaders"
    COMMENT "Copying shaders to executable directory..."
)

target_include_directories(${PROJECT_NAME} PRIVATE ${glm_SOURCE_DIR})

target_link_libraries(${PROJECT_NAME} PRIVATE
    glfw
    glad_gl_core_46
    glm::glm)