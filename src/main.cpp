#include <iostream>
#include <array>

#include <glad/gl.h>
#include <GLFW/glfw3.h>
#include <glm/glm.hpp>
#include "utils.hpp"

constexpr GLuint WIDTH = 800, HEIGHT = 600;
const auto TITLE = "StartingPoint";

void key_callback(GLFWwindow* window, int key, int scancode, int action, int mode) {
    if (key == GLFW_KEY_ESCAPE && action == GLFW_PRESS)
        glfwSetWindowShouldClose(window, GL_TRUE);
}

GLuint createTriangle() {
    constexpr GLsizei stride = 3 * sizeof(float);

    GLuint VAO; // vertex array obj
    glGenVertexArrays(1, &VAO);
    glBindVertexArray(VAO);

    // define what the object should be
    float vertices[] = {
        -0.5f, -0.5f, 0.5f,
        0.5f, -0.5f, 0.5f,
        0.0f, 0.5f, 0.5f
    };

    GLuint VBO; // vertex buffer obj
    glGenBuffers(1, &VBO);

    glBindBuffer(GL_ARRAY_BUFFER, VBO);
    glBufferData(GL_ARRAY_BUFFER, sizeof(vertices), vertices, GL_STATIC_DRAW);

    glVertexAttribPointer(0, 3, GL_FLOAT, GL_FALSE, stride, nullptr);
    glEnableVertexAttribArray(0);

    glBindVertexArray(0);

    return VAO;
}

GLuint createShaders() {
    char* vertexSource;
    char* fragmentSource;
    loadFromFile("shaders/new_vert.glsl", vertexSource);
    loadFromFile("shaders/new_frag.glsl", fragmentSource);

    // VERT
    GLuint vert = glCreateShader(GL_VERTEX_SHADER);
    glShaderSource(vert, 1, &vertexSource, nullptr);
    glCompileShader(vert);
    checkCompileErrors(vert, "VERTEX");

    // FRAG
    GLuint frag = glCreateShader(GL_FRAGMENT_SHADER);
    glShaderSource(frag, 1, &fragmentSource, nullptr);
    glCompileShader(frag);
    checkCompileErrors(frag, "FRAGMENT");

    // PROGRAM
    GLuint program = glCreateProgram();
    glAttachShader(program, vert);
    glAttachShader(program, frag);
    glLinkProgram(program);
    checkCompileErrors(program, "PROGRAM");

    return program;
}

int main() {
    glfwInit();

    glfwWindowHint(GLFW_CONTEXT_VERSION_MAJOR, 4);
    glfwWindowHint(GLFW_CONTEXT_VERSION_MINOR, 6);
    glfwWindowHint(GLFW_OPENGL_PROFILE, GLFW_OPENGL_CORE_PROFILE);

    GLFWwindow* window = glfwCreateWindow(WIDTH, HEIGHT, TITLE, nullptr, nullptr);
    glfwMakeContextCurrent(window);

    glfwSetKeyCallback(window, key_callback);
    gladLoadGL(glfwGetProcAddress);

    // CREATE ASSET
    // create triangle
    const GLuint triangle = createTriangle();

    // create shaders
    const GLuint shader = createShaders();

    // render loop
    while (!glfwWindowShouldClose(window)) {
        // clear screen
        glClearColor(100.0f, 0.0f, 0.0f, 1.0f);
        glClear(GL_COLOR_BUFFER_BIT);

        // use shaders
        glUseProgram(shader);

        // render triangle
        glBindVertexArray(triangle);
        glDrawArrays(GL_TRIANGLES, 0, 3);

        glfwSwapBuffers(window);
        glfwPollEvents();
    }

    glfwTerminate();

    return 0;
}