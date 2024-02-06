#version 330 core
layout (location = 0) in vec3 aPosition;
layout (location = 1) in vec3 aNormal;

uniform mat4 model;
uniform mat4 view;
uniform mat4 projection;


out vec3 normal;
out vec2 texCoord;
out ivec3 textureDims;

float AsPercent(float p , float a , float b){
    return ((p - a) / (b - a));
}

vec2 GetTexCoord(vec2 point){
    return vec2(
    AsPercent(point.x,0,1),
    AsPercent(point.y,0,1)
    );
}

void main()
{
    gl_Position = vec4(aPosition,1.0) * model * view * projection;
}