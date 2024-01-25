#version 430


//Define size of local workgroup
layout(local_size_x = 8, local_size_y = 8) in;

struct vertex{
	vec4 Pos;
	vec4 Color;
	vec4 Normal;
};


layout(std430, binding = 1) buffer Vertex
{
    vertex verticies[];
};

layout(rgba32f, binding = 0) uniform image2D biomeTexture;

uniform ivec2 vertexCount;

//Based On https://slideplayer.com/slide/3447433/12/images/14/Robert+Whittaker,+Cornell+Uni..jpg
int[] biomeLookup = int[36](9,8,3,0,0,0
,9,8,3,0,0,0
,9,8,4,4,1,1
,9,8,7,4,1,1
,9,8,7,5,2,2
,9,8,7,6,2,2);

//Desert
//Savanna
//TropicalRainforest
//Grassland
//Woodland
//SeasonalForest
//TemperateRainforest
//BorealForest
//Tundra
//Ice

vec4[] biomeColors = vec4[](vec4(238/255.0, 218/255.0, 130/255.0, 1)
,vec4(177/255.0, 209/255.0, 110/255.0, 1)
,vec4(66/255.0, 123/255.0, 25/255.0, 1)
,vec4(164/255.0, 225/255.0, 99/255.0, 1)
,vec4(139/255.0, 175/255.0, 90/255.0, 1)
,vec4(73/255.0, 100/255.0, 35/255.0, 1)
,vec4(29/255.0, 73/255.0, 40/255.0, 1)
,vec4(95/255.0, 115/255.0, 62/255.0, 1)
,vec4(96/255.0, 131/255.0, 112/255.0, 1)
,vec4(1,1,1,1));

void main()
{ 

	ivec2 id = ivec2(gl_GlobalInvocationID.xy);

	if(id.x >= vertexCount.x || id.y >= vertexCount.y){
		return;
	}

	int index = id.y * vertexCount.x + id.x;

	vec4 biomeMapSample = imageLoad(biomeTexture,id);

	int lookupValue = int(biomeMapSample.b) * 6 + int(biomeMapSample.r);

    vec4 color = biomeColors[biomeLookup[lookupValue]];

	verticies[index].Color = color; 


}