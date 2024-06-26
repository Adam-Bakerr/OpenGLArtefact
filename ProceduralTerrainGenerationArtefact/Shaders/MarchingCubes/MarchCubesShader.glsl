#version 460

//Define size of local workgroup
layout(local_size_x = 8, local_size_y = 8, local_size_z = 8) in;

#pragma include "triangle_table.compute"

struct Vertex{
	vec4 Pos;
	vec4 Color;
	vec4 Normal;
};

struct Triangle{
	Vertex vertexA;
	Vertex vertexB;
	Vertex vertexC;
};

layout(std430, binding = 0) buffer point
{
    float points[];
};

layout(std430, binding = 1) buffer vertex
{
    Vertex vertices[];
};

layout(std430, binding = 2) buffer triangletable
{
    int triTable[];
};

layout(binding = 3) uniform atomic_uint counter;

uniform vec3 resolution;
uniform ivec3 vertexCount;
uniform float surfaceLevel;


// Values from http://paulbourke.net/geometry/polygonise/
int edges[256] =
{
    0x0,
    0x109,
    0x203,
    0x30a,
    0x406,
    0x50f,
    0x605,
    0x70c,
    0x80c,
    0x905,
    0xa0f,
    0xb06,
    0xc0a,
    0xd03,
    0xe09,
    0xf00,
    0x190,
    0x99,
    0x393,
    0x29a,
    0x596,
    0x49f,
    0x795,
    0x69c,
    0x99c,
    0x895,
    0xb9f,
    0xa96,
    0xd9a,
    0xc93,
    0xf99,
    0xe90,
    0x230,
    0x339,
    0x33,
    0x13a,
    0x636,
    0x73f,
    0x435,
    0x53c,
    0xa3c,
    0xb35,
    0x83f,
    0x936,
    0xe3a,
    0xf33,
    0xc39,
    0xd30,
    0x3a0,
    0x2a9,
    0x1a3,
    0xaa,
    0x7a6,
    0x6af,
    0x5a5,
    0x4ac,
    0xbac,
    0xaa5,
    0x9af,
    0x8a6,
    0xfaa,
    0xea3,
    0xda9,
    0xca0,
    0x460,
    0x569,
    0x663,
    0x76a,
    0x66,
    0x16f,
    0x265,
    0x36c,
    0xc6c,
    0xd65,
    0xe6f,
    0xf66,
    0x86a,
    0x963,
    0xa69,
    0xb60,
    0x5f0,
    0x4f9,
    0x7f3,
    0x6fa,
    0x1f6,
    0xff,
    0x3f5,
    0x2fc,
    0xdfc,
    0xcf5,
    0xfff,
    0xef6,
    0x9fa,
    0x8f3,
    0xbf9,
    0xaf0,
    0x650,
    0x759,
    0x453,
    0x55a,
    0x256,
    0x35f,
    0x55,
    0x15c,
    0xe5c,
    0xf55,
    0xc5f,
    0xd56,
    0xa5a,
    0xb53,
    0x859,
    0x950,
    0x7c0,
    0x6c9,
    0x5c3,
    0x4ca,
    0x3c6,
    0x2cf,
    0x1c5,
    0xcc,
    0xfcc,
    0xec5,
    0xdcf,
    0xcc6,
    0xbca,
    0xac3,
    0x9c9,
    0x8c0,
    0x8c0,
    0x9c9,
    0xac3,
    0xbca,
    0xcc6,
    0xdcf,
    0xec5,
    0xfcc,
    0xcc,
    0x1c5,
    0x2cf,
    0x3c6,
    0x4ca,
    0x5c3,
    0x6c9,
    0x7c0,
    0x950,
    0x859,
    0xb53,
    0xa5a,
    0xd56,
    0xc5f,
    0xf55,
    0xe5c,
    0x15c,
    0x55,
    0x35f,
    0x256,
    0x55a,
    0x453,
    0x759,
    0x650,
    0xaf0,
    0xbf9,
    0x8f3,
    0x9fa,
    0xef6,
    0xfff,
    0xcf5,
    0xdfc,
    0x2fc,
    0x3f5,
    0xff,
    0x1f6,
    0x6fa,
    0x7f3,
    0x4f9,
    0x5f0,
    0xb60,
    0xa69,
    0x963,
    0x86a,
    0xf66,
    0xe6f,
    0xd65,
    0xc6c,
    0x36c,
    0x265,
    0x16f,
    0x66,
    0x76a,
    0x663,
    0x569,
    0x460,
    0xca0,
    0xda9,
    0xea3,
    0xfaa,
    0x8a6,
    0x9af,
    0xaa5,
    0xbac,
    0x4ac,
    0x5a5,
    0x6af,
    0x7a6,
    0xaa,
    0x1a3,
    0x2a9,
    0x3a0,
    0xd30,
    0xc39,
    0xf33,
    0xe3a,
    0x936,
    0x83f,
    0xb35,
    0xa3c,
    0x53c,
    0x435,
    0x73f,
    0x636,
    0x13a,
    0x33,
    0x339,
    0x230,
    0xe90,
    0xf99,
    0xc93,
    0xd9a,
    0xa96,
    0xb9f,
    0x895,
    0x99c,
    0x69c,
    0x795,
    0x49f,
    0x596,
    0x29a,
    0x393,
    0x99,
    0x190,
    0xf00,
    0xe09,
    0xd03,
    0xc0a,
    0xb06,
    0xa0f,
    0x905,
    0x80c,
    0x70c,
    0x605,
    0x50f,
    0x406,
    0x30a,
    0x203,
    0x109,
    0x0
};

        


int cornerIndexAFromEdge[12] =
{
    0,
    1,
    2,
    3,
    4,
    5,
    6,
    7,
    0,
    1,
    2,
    3
};

int cornerIndexBFromEdge[12] =
{
    1,
    2,
    3,
    0,
    5,
    6,
    7,
    4,
    4,
    5,
    6,
    7
};

uniform float _GrassSlopeThreshold;
uniform float _GrassBlendAmount;

vec4 permute(vec4 x) { return mod(((x * 34.0) + 1.0) * x, 289.0); }
vec4 taylorInvSqrt(vec4 r) { return 1.79284291400159 - 0.85373472095314 * r; }


vec4 interpolateVerts(vec4 v1, vec4 v2)
{
    float t = (surfaceLevel - v1.w) / (v2.w - v1.w);
    return vec4(v1.xyz + t * (v2.xyz - v1.xyz),1);
}

vec4 positionAndValueFromIndex(ivec3 id){
    int index = id.y * vertexCount.x * vertexCount.z + id.z * vertexCount.x + id.x;
    return vec4(id.x*resolution.x,id.y*resolution.y,id.z*resolution.z,points[index]);
}

vec4 CalculateNormal(vec4 point1, vec4 point2, vec4 point3){
        vec3 e1 = point2.xyz - point1.xyz;
	    vec3 e2 = point3.xyz - point1.xyz;
        vec3 normal = cross(e1,e2);
        return(vec4(normalize(-normal),1));
}

void AddVertex(vec4 pos, vec4 norm, vec4 color, uint counterValue, uint offset){
    //uint counterValue = atomicCounterIncrement(counter);
    vertices[counterValue + offset].Pos = pos;
    vertices[counterValue + offset].Normal = norm;
    vertices[counterValue + offset].Color = color;
}

void main()
{ 

	ivec3 id = ivec3(gl_GlobalInvocationID.xyz);

	if(id.x >= vertexCount.x - 1 || id.y >= vertexCount.y - 1 || id.z >= vertexCount.z - 1){
		return;
	}

    int index = id.y * vertexCount.x * vertexCount.z + id.z * vertexCount.x + id.x;
	
	// 8 corners of the current cube
    vec4 cubeCorners[8] =
    {
        positionAndValueFromIndex(id),
        positionAndValueFromIndex(ivec3(id.x + 1, id.y, id.z)),
        positionAndValueFromIndex(ivec3(id.x + 1, id.y, id.z + 1)),
        positionAndValueFromIndex(ivec3(id.x, id.y, id.z + 1)),
        positionAndValueFromIndex(ivec3(id.x, id.y + 1, id.z)),
        positionAndValueFromIndex(ivec3(id.x + 1, id.y + 1, id.z)),
        positionAndValueFromIndex(ivec3(id.x + 1, id.y + 1, id.z + 1)),
        positionAndValueFromIndex(ivec3(id.x, id.y + 1, id.z + 1))
    };

    int cubeIndex = 0;

   	for (int v = 0; v < 8; v++)
    {
		if (cubeCorners[v].w <= surfaceLevel) cubeIndex |= 1 << v;
    }

    //Dont try to add verticies if we are fully contained or not solid
    if(cubeIndex == 0 || cubeIndex == 255) return;

    // Create triangles for current cube configuration
    for (int i = 0; i < 15; i += 3)
    {
        if(triTable[cubeIndex * 16 + i] == -1){
            break;
        }

        int a0 = cornerIndexAFromEdge[triTable[cubeIndex * 16 + i]];
        int b0 = cornerIndexBFromEdge[triTable[cubeIndex * 16 + i]];

        int a1 = cornerIndexAFromEdge[triTable[cubeIndex * 16 + (i + 1)]];
        int b1 = cornerIndexBFromEdge[triTable[cubeIndex * 16 + (i + 1)]];

        int a2 = cornerIndexAFromEdge[triTable[cubeIndex * 16 + (i + 2)]];
        int b2 = cornerIndexBFromEdge[triTable[cubeIndex * 16 + (i + 2)]];


        //Positions
        vec4 p1 = interpolateVerts(cubeCorners[a0], cubeCorners[b0]);
	    vec4 p2 = interpolateVerts(cubeCorners[a1], cubeCorners[b1]);
	    vec4 p3 = interpolateVerts(cubeCorners[a2], cubeCorners[b2]);

        //Normal
        vec4 normal = CalculateNormal(p1,p2,p3);

        //Color
        float slope = 1-normal.y; // slope = 0 when terrain is completely flat
        float grassBlendHeight = _GrassSlopeThreshold * (1-_GrassBlendAmount);
        float grassWeight = 1-clamp((slope-grassBlendHeight)/(_GrassSlopeThreshold-grassBlendHeight),0,1);
        vec4 color = vec4(0,1,0,1) * grassWeight + vec4(.5,.3,.3,1) * (1-grassWeight);
        color = mix(color,vec4(.7,.7,.7,1),abs(dot(normal.xyz,vec3(1,0,1))));

        uint counterValue = atomicCounterAdd(counter,3);

        AddVertex(p1,normal,color,counterValue,0);
        AddVertex(p2,normal,color,counterValue,1);
        AddVertex(p3,normal,color,counterValue,2);
    }


}

