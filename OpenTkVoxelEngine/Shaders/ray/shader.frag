#version 330 core
out vec4 FragColor;


uniform sampler2D screenTexture;
in vec2 uv;


void main()
{
    // Set the output color
    FragColor=texture(screenTexture,uv);
}