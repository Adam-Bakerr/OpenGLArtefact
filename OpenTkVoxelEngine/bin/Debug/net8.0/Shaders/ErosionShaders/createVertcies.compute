#version 430


//Define size of local workgroup
layout(local_size_x = 8, local_size_y = 8) in;

struct vertex{
	vec4 Pos;
	vec4 Color;
	vec4 Normal;
};

uniform ivec2 vertexCount;
uniform vec2 resolution;


layout(std430, binding = 0) buffer Vertex
{
    vertex verticies[];
};

layout(std430, binding = 1) buffer HeightBuffer
{
    float heightMap[];
};

vec4 CalculateNormal(int index){
	vec3 normal = vec3(0);

	vec3 p1 = verticies[index].Pos.xyz;
	vec3 p2 = verticies[index + 1].Pos.xyz;
	vec3 p3 = verticies[index + vertexCount.x].Pos.xyz;

	vec3 e1 = p2 - p1;
	vec3 e2 = p3 - p1;
	
	normal = cross(e1,e2);

	normal = normalize(-normal);

    return vec4(normal,1);

}


void main()
{ 

	ivec2 id = ivec2(gl_GlobalInvocationID.xy);

	if(id.x >= vertexCount.x || id.y >= vertexCount.y){
		return;
	}


	int index = id.y * vertexCount.x + id.x;

	//Initalise position vectors
	verticies[index].Pos = vec4(id.x * resolution.x, heightMap[index], id.y * resolution.y,0);

	//Initalize the normal vectors
	verticies[index].Normal = CalculateNormal(index);
}