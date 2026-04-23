#pragma once

#include <string>
#include <iostream>
#include <fstream>

void loadFromFile(std::string url, char*& buf)
{
    std::ifstream stream(url, std::ios::binary);
    if (!stream.is_open()) return;

    stream.seekg(0, stream.end);
    std::streamoff total = stream.tellg();
    stream.seekg(0, stream.beg);

    // Read the whole file into a temporary buffer
    char* temp = new char[total];
    stream.read(temp, total);
    stream.close();

    int offset = 0;
    // Check for UTF-8 BOM: 0xEF, 0xBB, 0xBF
    if (total >= 3 &&
        static_cast<unsigned char>(temp[0]) == 0xEF &&
        static_cast<unsigned char>(temp[1]) == 0xBB &&
        static_cast<unsigned char>(temp[2]) == 0xBF)
    {
        offset = 3;
    }

    std::streamoff actualSize = total - offset;
    buf = new char[actualSize + 1];
    memcpy(buf, temp + offset, actualSize);
    buf[actualSize] = '\0';

    delete[] temp;
}

// utility function for checking shader compilation/linking errors.
    // ------------------------------------------------------------------------
void checkCompileErrors(GLuint shader, std::string type)
{
    GLint success;
    GLchar infoLog[1024];
    if (type != "PROGRAM")
    {
        glGetShaderiv(shader, GL_COMPILE_STATUS, &success);
        if (!success)
        {
            glGetShaderInfoLog(shader, 1024, NULL, infoLog);
            std::cout << "ERROR::SHADER_COMPILATION_ERROR of type: " << type << "\n" << infoLog << "\n -- --------------------------------------------------- -- " << std::endl;
        }
    }
    else
    {
        glGetProgramiv(shader, GL_LINK_STATUS, &success);
        if (!success)
        {
            glGetProgramInfoLog(shader, 1024, NULL, infoLog);
            std::cout << "ERROR::PROGRAM_LINKING_ERROR of type: " << type << "\n" << infoLog << "\n -- --------------------------------------------------- -- " << std::endl;
        }
    }
}