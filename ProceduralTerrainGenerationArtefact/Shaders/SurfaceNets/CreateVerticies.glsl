#version 460

//Define size of local workgroup
layout(local_size_x = 8, local_size_y = 8, local_size_z = 8) in;



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


layout(std430, binding = 1) buffer FeaturePoints
{
    vec4 featurePoints[];
};

layout(std430, binding = 2) buffer Vertex
{
    vertex verticies[];
};

layout(binding = 3) uniform atomic_uint counter;

uniform vec4 resolution;
uniform ivec3 vertexCount;
uniform float surfaceLevel;

vec4 CalculateNormal(vec4 point1, vec4 point2, vec4 point3){
        vec3 e1 = point2.xyz - point1.xyz;
	    vec3 e2 = point3.xyz - point1.xyz;
        vec3 normal = cross(e1,e2);
        return(vec4(normalize(-normal),1));
}

int GetIndex(vec3 pos){
    return int(pos.y) * vertexCount.x * vertexCount.z + int(pos.z) * vertexCount.x + int(pos.x);
}

vec3 GetPosition(int index) {
	
		float y = int(floor((index % vertexCount.x)));
		float z = int(floor((index / vertexCount.x) % vertexCount.z));
		float x = int(floor(index / (vertexCount.x * vertexCount.z)));
	return vec3(x,y,z);
}


void main()
{ 
	ivec3 id = ivec3(gl_GlobalInvocationID.xyz);

	if(id.x >= vertexCount.x || id.y >= vertexCount.y ||  id.z >= vertexCount.z){
		return;
	}

  
    vec3 k0 = GetPosition(GetIndex(id));
	vec3 k1 = k0 + vec3(1, 0, 0);
	vec3 k2 = k0 + vec3(1, 0, 1);
	vec3 k3 = k0 + vec3(0, 0, 1);


	vertex value0;
	vertex value1;
	vertex value2;
	vertex value3;



	value0.Color = value1.Color = value2.Color = value3.Color = vec4(.5,.5,.5,1);


	int v0 = GetIndex(k0);
	int v1 = GetIndex(k1);
	int v2 = GetIndex(k2);
	int v3 = GetIndex(k3);
	value0.Pos = featurePoints[v0]*resolution;
	value1.Pos = featurePoints[v1]*resolution;
	value2.Pos = featurePoints[v2]*resolution;
	value3.Pos = featurePoints[v3]*resolution;


	if (value0.Pos.x != 0 && value0.Pos.y != 0 && value0.Pos.z != 0 &&
		value1.Pos.x != 0 && value1.Pos.y != 0 && value1.Pos.z != 0 &&
		value2.Pos.x != 0 && value2.Pos.y != 0 && value2.Pos.z != 0 &&
		value3.Pos.x != 0 && value3.Pos.y != 0 && value3.Pos.z != 0 ){

		int densityIndex = GetIndex(k2);

		if (points[densityIndex] > surfaceLevel)
		{
			uint appendIndex = atomicCounterAdd(counter,6);
			verticies[appendIndex] = value0;
			verticies[appendIndex + 1] = value1;
			verticies[appendIndex + 2] = value2;
			verticies[appendIndex + 3] = value2;
			verticies[appendIndex + 4] = value3;
			verticies[appendIndex + 5] = value0;

			vec4 Normal1 = CalculateNormal(value0.Pos,value1.Pos,value2.Pos);
			vec4 Normal2 = CalculateNormal(value2.Pos,value3.Pos,value0.Pos);

			verticies[appendIndex].Normal = verticies[appendIndex + 1].Normal = verticies[appendIndex + 2].Normal = Normal1;
			verticies[appendIndex + 3].Normal = verticies[appendIndex + 4].Normal = verticies[appendIndex + 5].Normal = Normal2;

		}
		else
		{
			uint appendIndex = atomicCounterAdd(counter,6);
			verticies[appendIndex] = value0;
			verticies[appendIndex + 1] = value3;
			verticies[appendIndex + 2] = value2;
			verticies[appendIndex + 3] = value2;
			verticies[appendIndex + 4] = value1;
			verticies[appendIndex + 5] = value0;

			vec4 Normal1 = CalculateNormal(value2.Pos,value3.Pos,value0.Pos);
			vec4 Normal2 = CalculateNormal(value0.Pos,value1.Pos,value2.Pos);

			verticies[appendIndex].Normal = verticies[appendIndex + 1].Normal = verticies[appendIndex + 2].Normal = Normal1;
			verticies[appendIndex + 3].Normal = verticies[appendIndex + 4].Normal = verticies[appendIndex + 5].Normal = Normal2;


		}
	} 


	k1 = k0 + vec3(0, 1, 0);
	k2 = k0 + vec3(1, 1, 0);
	k3 = k0 + vec3(1, 0, 0);



	value0.Pos = featurePoints[GetIndex(k0)]*resolution;
	value1.Pos = featurePoints[GetIndex(k1)]*resolution;
	value2.Pos = featurePoints[GetIndex(k2)]*resolution;
	value3.Pos = featurePoints[GetIndex(k3)]*resolution;

	if (value0.Pos.x != 0 && value0.Pos.y != 0 && value0.Pos.z != 0 &&
		value1.Pos.x != 0 && value1.Pos.y != 0 && value1.Pos.z != 0 &&
		value2.Pos.x != 0 && value2.Pos.y != 0 && value2.Pos.z != 0 &&
		value3.Pos.x != 0 && value3.Pos.y != 0 && value3.Pos.z != 0 ){

		int densityIndex = GetIndex(k2);

		if (points[densityIndex] > surfaceLevel)
		{
			uint appendIndex = atomicCounterAdd(counter,6);
			verticies[appendIndex] = value0;
			verticies[appendIndex + 1] = value1;
			verticies[appendIndex + 2] = value2;
			verticies[appendIndex + 3] = value2;
			verticies[appendIndex + 4] = value3;
			verticies[appendIndex + 5] = value0;

			vec4 Normal1 = CalculateNormal(value0.Pos,value1.Pos,value2.Pos);
			vec4 Normal2 = CalculateNormal(value2.Pos,value3.Pos,value0.Pos);

			verticies[appendIndex].Normal = verticies[appendIndex + 1].Normal = verticies[appendIndex + 2].Normal = Normal1;
			verticies[appendIndex + 3].Normal = verticies[appendIndex + 4].Normal = verticies[appendIndex + 5].Normal = Normal2;

		}
		else
		{
			uint appendIndex = atomicCounterAdd(counter,6);
			verticies[appendIndex] = value0;
			verticies[appendIndex + 1] = value3;
			verticies[appendIndex + 2] = value2;
			verticies[appendIndex + 3] = value2;
			verticies[appendIndex + 4] = value1;
			verticies[appendIndex + 5] = value0;

			vec4 Normal1 = CalculateNormal(value2.Pos,value3.Pos,value0.Pos);
			vec4 Normal2 = CalculateNormal(value0.Pos,value1.Pos,value2.Pos);

			verticies[appendIndex].Normal = verticies[appendIndex + 1].Normal = verticies[appendIndex + 2].Normal = Normal1;
			verticies[appendIndex + 3].Normal = verticies[appendIndex + 4].Normal = verticies[appendIndex + 5].Normal = Normal2;


		}
	} 

	k1 = k0 + vec3(0, 0, 1);
	k2 = k0 + vec3(0, 1, 1);
	k3 = k0 + vec3(0, 1, 0);

	value0.Pos = featurePoints[GetIndex(k0)]*resolution;
	value1.Pos = featurePoints[GetIndex(k1)]*resolution;
	value2.Pos = featurePoints[GetIndex(k2)]*resolution;
	value3.Pos = featurePoints[GetIndex(k3)]*resolution;

	if (value0.Pos.xyz != vec3(0) && value1.Pos.xyz != vec3(0) && value2.Pos.xyz != vec3(0) && value3.Pos.xyz != vec3(0)){

		int densityIndex = GetIndex(k2);

		if (points[densityIndex] > surfaceLevel)
		{
			uint appendIndex = atomicCounterAdd(counter,6);
			verticies[appendIndex] = value0;
			verticies[appendIndex + 1] = value1;
			verticies[appendIndex + 2] = value2;
			verticies[appendIndex + 3] = value2;
			verticies[appendIndex + 4] = value3;
			verticies[appendIndex + 5] = value0;

			vec4 Normal1 = CalculateNormal(value0.Pos,value1.Pos,value2.Pos);
			vec4 Normal2 = CalculateNormal(value2.Pos,value3.Pos,value0.Pos);

			verticies[appendIndex].Normal = verticies[appendIndex + 1].Normal = verticies[appendIndex + 2].Normal = Normal1;
			verticies[appendIndex + 3].Normal = verticies[appendIndex + 4].Normal = verticies[appendIndex + 5].Normal = Normal2;


		}
		else
		{
			uint appendIndex = atomicCounterAdd(counter,6);
			verticies[appendIndex] = value0;
			verticies[appendIndex + 1] = value3;
			verticies[appendIndex + 2] = value2;
			verticies[appendIndex + 3] = value2;
			verticies[appendIndex + 4] = value1;
			verticies[appendIndex + 5] = value0;

			vec4 Normal1 = CalculateNormal(value2.Pos,value3.Pos,value0.Pos);
			vec4 Normal2 = CalculateNormal(value0.Pos,value1.Pos,value2.Pos);

			verticies[appendIndex].Normal = verticies[appendIndex + 1].Normal = verticies[appendIndex + 2].Normal = Normal1;
			verticies[appendIndex + 3].Normal = verticies[appendIndex + 4].Normal = verticies[appendIndex + 5].Normal = Normal2;
		}
	} 

}

