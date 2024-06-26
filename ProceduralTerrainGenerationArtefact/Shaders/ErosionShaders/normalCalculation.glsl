#version 430


//Define size of local workgroup
layout(local_size_x = 8, local_size_y = 8) in;

struct vertex{
	vec4 Pos;
	vec4 Color;
	vec4 Normal;
};


layout(std430, binding = 0) buffer Vertex
{
    vertex verticies[];
};

uniform ivec2 vertexCount;

void main()
{ 
	ivec2 id = ivec2(gl_GlobalInvocationID.xy);

	if(id.x >= vertexCount.x || id.y >= vertexCount.y){
		return;
	}

	int index = id.y * vertexCount.x + id.x;

	vec3 normal = vec3(0);

	vec3 p1 = verticies[index].Pos.xyz;
	vec3 p2 = verticies[index + 1].Pos.xyz;
	vec3 p3 = verticies[index + vertexCount.x].Pos.xyz;

	vec3 e1 = p2 - p1;
	vec3 e2 = p3 - p1;
	
	normal = cross(e1,e2);

	normal = normalize(-normal);

	verticies[index].Normal = vec4(normal,1);
}