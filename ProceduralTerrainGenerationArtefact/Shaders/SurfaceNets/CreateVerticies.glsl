#version 460

//Define size of local workgroup
layout(local_size_x = 8, local_size_y = 8, local_size_z = 8) in;

//Vertex Struct That Defines The Data In The Vertex Buffer
struct vertex{
	vec4 Pos;
	vec4 Color;
	vec4 Normal;
};

//xyz grad w value
layout(std430, binding = 0) buffer point
{
    float points[];
};

layout(std430, binding = 1) buffer ContourPointBuffer
{
    vec4 ContourPoints[];
};

layout(std430, binding = 2) buffer Vertex
{
    vertex verticies[];
};

//Atomic buffer counter used to create an append buffer
layout(binding = 3) uniform atomic_uint counter;

//variables
uniform vec4 resolution;
uniform ivec3 vertexCount;
uniform float surfaceLevel;

//Calculate surface normal
vec4 CalculateNormal(vec4 point1, vec4 point2, vec4 point3){
        vec3 e1 = point2.xyz - point1.xyz;
	    vec3 e2 = point3.xyz - point1.xyz;
        vec3 normal = cross(e1,e2);
        return(vec4(normalize(-normal),1));
}

//Calculate the index into the buffer
int GetIndex(vec3 pos){
    return int(pos.y) * vertexCount.x * vertexCount.z + int(pos.z) * vertexCount.x + int(pos.x);
}

//Check if there is any zero components in a vector3
bool NoZeroComponents(vec3 point){
	return max(point.x,max(point.y,point.z)) != 0;
}

//Voxel veretx offsets relative to the current vertex (0,0,0)
vec3 offsets[9] = {
	{ 1, 0 , 0},
	{ 1, 0 , 1},
	{ 0, 0 , 1},
	{ 0, 1 , 0},
	{ 1, 1 , 0},
	{ 1, 0 , 0},
	{ 0, 0 , 1},
	{ 0, 1 , 1},
	{ 0, 1 , 0}
};

void main()
{ 
	ivec3 id = ivec3(gl_GlobalInvocationID.xyz);

	if(id.x >= vertexCount.x || id.y >= vertexCount.y ||  id.z >= vertexCount.z){
		return;
	}


	vec3 k0 = id;

	if(ContourPoints[GetIndex(id)] == vec4(0,0,0,1)){
		return;
	}

	//Itterate over all 12 possible triangles that can be created
	//This is done 2 Triangles or 1 Quad at a time
	for(int edgeIndex = 0 ; edgeIndex < 9; edgeIndex +=3){

		//Calculate the other positions of the quad
		vec3 k1 = k0 + offsets[edgeIndex];
		vec3 k2 = k0 + offsets[edgeIndex + 1];
		vec3 k3 = k0 + offsets[edgeIndex + 2];

		//Initalize the vertex values of the quad
		vertex value0,value1,value2,value3;
		value0.Color = value1.Color = value2.Color = value3.Color = vec4(.5,.5,.5,1);

		//Get the index of each of the positions in the buffer
		int v0 = GetIndex(k0), v1 = GetIndex(k1), v2 = GetIndex(k2), v3 = GetIndex(k3);

		//Sample the contour points for each of the quads vertex
		value0.Pos = ContourPoints[v0]*resolution;
		value1.Pos = ContourPoints[v1]*resolution;
		value2.Pos = ContourPoints[v2]*resolution;
		value3.Pos = ContourPoints[v3]*resolution;

		//Check to see if any of the contoruing points contain a 0 component
		if (NoZeroComponents(value0.Pos.xyz) && NoZeroComponents(value1.Pos.xyz) && NoZeroComponents(value2.Pos.xyz) && NoZeroComponents(value3.Pos.xyz)){
			int densityIndex = v2;
			//Check if the point is inside or outside the surface
			//This is used to adjust the winding of the triangles we are creating
			if (points[densityIndex] >= surfaceLevel)
			{
				//Increment the atomic counter which allows us to tightly pack the vertex data into the buffer
				uint appendIndex = atomicCounterAdd(counter,6);

				//Insert the triangles in order into the vertex buffer
				verticies[appendIndex] = value0;
				verticies[appendIndex + 1] = value1;
				verticies[appendIndex + 2] = value2;
				verticies[appendIndex + 3] = value2;
				verticies[appendIndex + 4] = value3;
				verticies[appendIndex + 5] = value0;

				//Calculate the triangles normals based on the calculated vertex positions
				verticies[appendIndex].Normal = verticies[appendIndex + 1].Normal = verticies[appendIndex + 2].Normal = CalculateNormal(value0.Pos,value1.Pos,value2.Pos);
				verticies[appendIndex + 3].Normal = verticies[appendIndex + 4].Normal = verticies[appendIndex + 5].Normal = CalculateNormal(value2.Pos,value3.Pos,value0.Pos);
			}
			//Repeat for the inverse winding order 
			else
			{
				value0.Normal = value1.Normal = value2.Normal = CalculateNormal(value0.Pos,value3.Pos,value2.Pos);
				value2.Normal = value3.Normal = value0.Normal = CalculateNormal(value2.Pos,value1.Pos,value0.Pos);

				uint appendIndex = atomicCounterAdd(counter,6);
				verticies[appendIndex] = value0;
				verticies[appendIndex + 1] = value3;
				verticies[appendIndex + 2] = value2;
				verticies[appendIndex + 3] = value2;
				verticies[appendIndex + 4] = value1;
				verticies[appendIndex + 5] = value0;

				verticies[appendIndex].Normal = verticies[appendIndex + 1].Normal = verticies[appendIndex + 2].Normal = CalculateNormal(value0.Pos,value3.Pos,value2.Pos);
				verticies[appendIndex + 3].Normal = verticies[appendIndex + 4].Normal = verticies[appendIndex + 5].Normal = CalculateNormal(value2.Pos,value1.Pos,value0.Pos);
			}
		} 
	}
}

