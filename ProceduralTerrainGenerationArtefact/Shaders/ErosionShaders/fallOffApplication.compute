#version 430


//Define size of local workgroup
layout(local_size_x = 8, local_size_y = 8) in;

struct vertex{
	vec4 Pos;
	vec4 Color;
	vec4 Normal;
};


layout(std430, binding = 0) buffer HeightMap
{
    float heightMap[];
};

//0 = min 1 = max
layout(std430, binding = 1) buffer vertexInfo
{
    int minMax[];
};

uniform ivec2 vertexCount;
uniform vec2 resolution;
uniform int minMaxPrecisionFactor;

//FBM values
uniform int seed;
uniform int NumLayers;
uniform vec3 centre;
uniform float baseRoughness;
uniform float roughness;
uniform float persistence;
uniform float minValue;
uniform float strength;
uniform float scale;
uniform float minHeight;
uniform float maxHeight;
uniform float lacunicity;
uniform float jitter;

//https://github.com/Scrawk/GPU-Voronoi-Noise/blob/master/Assets/GPUVoronoiNoise/Shader/GPUVoronoiNoise2D.cginc

//1/7
#define K 0.142857142857
//3/7
#define Ko 0.428571428571

vec3 mod(vec3 x, float y)
{
    return x - y * floor(x / y);
}
vec2 mod(vec2 x, float y)
{
    return x - y * floor(x / y);
}

// Permutation polynomial: (34x^2 + x) mod 289
vec3 Permutation(vec3 x)
{
    return mod((34.0 * x + 1.0) * x, 289.0);
}

float frac(float v)
{
  return v - floor(v);
}

vec2 frac(vec2 v)
{
  return vec2(v.x - floor(v.x),v.y - floor(v.y));
}

vec3 frac(vec3 v)
{
  return vec3(v.x - floor(v.x),v.y - floor(v.y),v.z - floor(v.z));
}

vec2 inoise(vec2 P, float jitter)
{
    vec2 Pi = mod(floor(P), 289.0);
    vec2 Pf = frac(P);
    vec3 oi = vec3(-1.0, 0.0, 1.0);
    vec3 of = vec3(-0.5, 0.5, 1.5);
    vec3 px = Permutation(Pi.x + oi);
	
    vec3 p, ox, oy, dx, dy;
    vec2 F = vec2(1e6);
	
    for (int i = 0; i < 3; i++)
    {
        p = Permutation(px[i] + Pi.y + oi); // pi1, pi2, pi3
        ox = frac(p * K) - Ko;
        oy = mod(floor(p * K), 7.0) * K - Ko;
        dx = Pf.x - of[i] + jitter * ox;
        dy = Pf.y - of + jitter * oy;
		
        vec3 d = dx * dx + dy * dy; // di1, di2 and di3, squared
		
		//find the lowest and second lowest distances
        for (int n = 0; n < 3; n++)
        {
            if (d[n] < F[0])
            {
                F[1] = F[0];
                F[0] = d[n];
            }
            else if (d[n] < F[1])
            {
                F[1] = d[n];
            }
        }
    }
	
    return F * strength;
}

float fBm_F0(vec2 p)
{
    float freq = scale, amp = baseRoughness;
    float sum = 0;
    for (int i = 0; i < NumLayers; i++)
    {
        vec2 F = inoise(p * freq, jitter) * amp;
		
        sum += 0.1 + sqrt(F[0]);
		
        freq *= lacunicity;
        amp *= persistence;
    }
    return clamp(sum, minHeight, maxHeight);
}

void main()
{ 

	ivec2 id = ivec2(gl_GlobalInvocationID.xy);

	if(id.x >= vertexCount.x || id.y >= vertexCount.y){
		return;
	}


	int index = id.y * vertexCount.x + id.x;

	float height = heightMap[index];
    
	//Vornori Fall Off
    height = height * fBm_F0(vec2(id.x * resolution.x,id.y* resolution.y) + centre.xz);
    
    height = clamp(height,minHeight,maxHeight);
    
    
    //Apply New Vertex Data
    heightMap[index] = height;

    	int infoValue = int(heightMap[index] * minMaxPrecisionFactor);
	minMax[0] = atomicMin(minMax[0],infoValue);
	minMax[1] = atomicMax(minMax[1],infoValue);

}