#version 330 core
layout (location = 0) in vec4 aPosition;
layout (location = 1) in vec4 aColor;
layout (location = 2) in vec4 aNormal;

uniform mat4 model;
uniform mat4 view;
uniform mat4 projection;
uniform vec2 gridDimensions;
uniform float minHeight;
uniform sampler2D biomeMap;

out vec3 color;
out vec3 normal;
out vec3 FragPos;
out vec2 texCoord;



void main()
{
    gl_Position = vec4(aPosition.xyz, 1.0) * model * view * projection;
    texCoord = vec2(aPosition.x / gridDimensions.x, aPosition.z / gridDimensions.y);
    vec4 biomeMapSample = texture(biomeMap,texCoord);
    int Index = int(biomeMapSample.b) * 6 + int(biomeMapSample.r);
    color = aColor.xyz;
    //color = biomeColors[biomeLookup[Index]].xyz;
    normal = aNormal.xyz * mat3(transpose(inverse(model)));
    FragPos = vec3(model * vec4(aPosition.xyz,1.0));
    
}