#version 430


//Define size of local workgroup
layout(local_size_x = 8, local_size_y = 8) in;

uniform int trianglesPerRow;


layout(std430, binding = 0) buffer Index
{
    uint indices[];
};



void main()
{ 
	
	ivec2 id = ivec2(gl_GlobalInvocationID.xy);

	
	if(id.x >= trianglesPerRow || id.y >= trianglesPerRow ){
		return;
	}

	int index = (id.y * trianglesPerRow + id.x);
	int idx = (id.y * (trianglesPerRow + 1) + id.x);



	indices[index * 6 + 0] = idx + 0;
	indices[index * 6 + 1] = idx + (trianglesPerRow + 1);
	indices[index * 6 + 2] = idx + 1;
	indices[index * 6 + 4] = idx + 1;
	indices[index * 6 + 5] = idx + (trianglesPerRow + 1);
	indices[index * 6 + 3] = idx + (trianglesPerRow + 1) + 1;
}