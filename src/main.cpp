#include <iostream>
#include <array>

#include <glad/gl.h>
#include <GLFW/glfw3.h>
#include <glm/glm.hpp>

constexpr GLuint WIDTH = 800, HEIGHT = 600;
const auto TITLE = "StartingPoint";

void key_callback(GLFWwindow* window, int key, int scancode, int action, int mode) {
    if (key == GLFW_KEY_ESCAPE && action == GLFW_PRESS)
        glfwSetWindowShouldClose(window, GL_TRUE);
}

int main() {
    glfwInit();

    glfwWindowHint(GLFW_CONTEXT_VERSION_MAJOR, 3);
    glfwWindowHint(GLFW_CONTEXT_VERSION_MINOR, 2);
    glfwWindowHint(GLFW_OPENGL_PROFILE, GLFW_OPENGL_CORE_PROFILE);

    GLFWwindow* window = glfwCreateWindow(WIDTH, HEIGHT, TITLE, nullptr, nullptr);
    glfwMakeContextCurrent(window);

    glfwSetKeyCallback(window, key_callback);
    gladLoadGL(glfwGetProcAddress);

    constexpr int colorAmount = 3;
    constexpr std::array<glm::vec3, colorAmount> colors{
        {
            glm::vec3(1.0f, 0.0f, 0.0f),
            glm::vec3(0.0f, 1.0f, 0.0f),
            glm::vec3(0.0f, 0.0f, 1.0f),
        }};

    int index = 0;
    int nextIndex = index + 1;
    if (nextIndex >= colorAmount)
        nextIndex = 0;

    float positionT = 0.0f;

    glm::vec3 current = colors[index];
    glm::vec3 next = colors[nextIndex];
    glm::vec3 final = glm::mix(current, next, positionT);

    while (!glfwWindowShouldClose(window)) {
        glfwPollEvents();

        glClearColor(final.r, final.g, final.b, 1.0f);
        glClear(GL_COLOR_BUFFER_BIT);

        glfwSwapBuffers(window);

        positionT += 0.01f;
        if (positionT >= 1.0f) {
            positionT = 0.0f;

            index += 1;
            if (index >= colorAmount)
                index = 0;

            nextIndex = index + 1;
            if (nextIndex >= colorAmount)
                nextIndex = 0;

            current = colors[index];
            next = colors[nextIndex];
        }
        final = glm::mix(current, next, positionT);
    }

    glfwTerminate();

    return 0;
}