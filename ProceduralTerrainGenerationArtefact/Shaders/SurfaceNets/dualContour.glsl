#version 460

//Define size of local workgroup
layout(local_size_x = 8, local_size_y = 8, local_size_z = 8) in;


layout(std430, binding = 0) buffer point
{
    float points[];
};


layout(std430, binding = 1) buffer ContourPointBuffer
{
    vec4 ContourPoints[];
};

struct Edge {
	int Index1;
	int Index2;
};

uniform vec3 resolution;
uniform ivec3 vertexCount;
uniform float surfaceLevel;

Edge edges[12] = {
	{ 0, 1 },
	{ 0, 2 },
	{ 0, 4 },
	{ 1, 3 },

	{ 1, 5 },
	{ 2, 3 },
	{ 2, 6 },
	{ 4, 5 },

	{ 4, 6 },
	{ 3, 7 },
	{ 6, 7 },
	{ 5, 7 }
};

vec3 interpolateVerts(vec4 v1, vec4 v2)
{
    float t = (surfaceLevel - v1.w) / (v2.w - v1.w);
    return v1.xyz + t * (v2.xyz - v1.xyz);
}


int CheckSign(float value){
    if(value >= surfaceLevel ) return 1;
    if(value < surfaceLevel) return -1;
}


int GetIndex(vec3 pos){
    return int(pos.y) * vertexCount.x * vertexCount.z + int(pos.z) * vertexCount.x + int(pos.x);
}

vec3 GetPosition(int index) {
	
		float x = int((index % vertexCount.x));
		float z = int((index / vertexCount.x) % vertexCount.z);
		float y = int(index / (vertexCount.x * vertexCount.z));
		return vec3(x,y,z);
}


void main()
{ 
	ivec3 id = ivec3(gl_GlobalInvocationID.xyz);

	//Ensure the shader only runs on data we have access to, this avoids undefined behaviour from occuring
	if(id.x >= vertexCount.x || id.y >= vertexCount.y || id.z >= vertexCount.z){
		return;
	}

    int index = id.y * vertexCount.x * vertexCount.z + id.z * vertexCount.x + id.x;
	

    vec3 averageContourPoint = vec3(0);
    float edgeCrossing = 0.0;

	//Calculate the index of each of the 8 vertices that make up the voxel
    int indices[8] = {
		index,
		index + GetIndex(vec3(0,0,1)),
		index + GetIndex(vec3(0,1,0)),
		index + GetIndex(vec3(0,1,1)),

		index + GetIndex(vec3(1,0,0)),
		index + GetIndex(vec3(1,0,1)),
		index + GetIndex(vec3(1,1,0)),
		index + GetIndex(vec3(1,1,1))
	};

	//Itterate Over All Possible Edge Pairs In The Current Voxel
    for(int i = 0 ; i < 12 ; i++){

		//Retreive the data from buffers
        int index1 = indices[edges[i].Index1];
        int index2 = indices[edges[i].Index2];

        vec3 pos1 = GetPosition(index1);
        vec3 pos2 = GetPosition(index2);

        float density1 = points[index1];
		float density2 = points[index2];

		//If Both Vertices Of This Edge Pair Are On The Same Side Of The
		//surface we ignore it as the edge never crosses the surface itself
        if (CheckSign(density1) == CheckSign(density2))
		{
			continue;
		}

		//Add The crossing point to the average
        averageContourPoint += interpolateVerts(vec4(pos1,density1), vec4(pos2,density2));
		edgeCrossing++;
    }

	//Divide the average by the total number of times a edge crossed the surface to find the mean
	averageContourPoint /= max(1.0,edgeCrossing);
    ContourPoints[GetIndex(id)] = vec4(averageContourPoint,1);

}

