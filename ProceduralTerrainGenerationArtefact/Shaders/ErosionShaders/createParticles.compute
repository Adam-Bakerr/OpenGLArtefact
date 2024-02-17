#version 430

//Define size of local workgroup
layout(local_size_x = 8, local_size_y = 8) in;

struct droplet{
	vec2 pos;
};

uniform int sqrtParticleCount;
uniform ivec2 vertexCount;
uniform float totalTime;
uniform int borderSize;
uniform float randomHashOffset;

layout(std430, binding = 0) buffer Particle
{
    vec2 Particles[];
};

float hash( float n )
{
    return fract(sin(n)*43758.5453);
}

float noise( vec3 x )
{
    // The noise function returns a value in the range -1.0f -> 1.0f

    vec3 p = floor(x);
    vec3 f = fract(x);

    f       = f*f*(3.0-2.0*f);
    float n = p.x + p.y*57.0 + 113.0*p.z;

    return mix(mix(mix( hash(n+0.0), hash(n+1.0),f.x),
                   mix( hash(n+57.0), hash(n+58.0),f.x),f.y),
               mix(mix( hash(n+113.0), hash(n+114.0),f.x),
                   mix( hash(n+170.0), hash(n+171.0),f.x),f.y),f.z);
}


void main()
{ 

	ivec2 id = ivec2(gl_GlobalInvocationID.xy);

    if(id.x >= sqrtParticleCount || id.y >= sqrtParticleCount){
    return;
    }

	int index = id.y * (sqrtParticleCount) + id.x;

	//Pseudo Random Particle Position
	float x = clamp(noise(vec3(id.x,id.y,randomHashOffset + totalTime)) * vertexCount.x,borderSize,vertexCount.x - borderSize);
	float y = clamp(noise(vec3(id.y,id.x,randomHashOffset * totalTime)) * vertexCount.y,borderSize,vertexCount.y - borderSize);

	Particles[index] = vec2(x,y);
	
}