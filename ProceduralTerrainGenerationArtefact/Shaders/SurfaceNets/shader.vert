#version 330 core
layout (location = 0) in vec4 aPosition;
layout (location = 1) in vec4 aColor;
layout (location = 2) in vec4 aNormal;

uniform mat4 model;
uniform mat4 view;
uniform mat4 projection;

out vec4 color;
out vec4 normal;
out vec3 FragPos;

uniform float _GrassSlopeThreshold;
uniform float _GrassBlendAmount;

void main()
{
  

    color = aColor;
    normal = aNormal;
    gl_Position = aPosition * model * view * projection;
    FragPos = vec3(model * vec4(aPosition.xyz,1.0));
}