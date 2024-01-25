#version 430


//Define size of local workgroup
layout(local_size_x = 8, local_size_y = 8) in;

struct vertex{
	vec4 Pos;
	vec4 Color;
	vec4 Normal;
};

uniform ivec2 vertexCount;


layout(std430, binding = 0) buffer inVertex
{
    vertex inVerticies[];
};

layout(std430, binding = 1) buffer outVertex
{
    vertex outVerticies[];
};



void main()
{ 

	ivec2 id = ivec2(gl_GlobalInvocationID.xy);

	if(id.x >= vertexCount.x || id.y >= vertexCount.y){
		return;
	}

	int index = id.y * vertexCount.x + id.x;

	outVerticies[index] = inVerticies[index];

}