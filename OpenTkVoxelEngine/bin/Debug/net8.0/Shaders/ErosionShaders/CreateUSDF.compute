#version 430

//Define size of local workgroup
layout(local_size_x = 4, local_size_y = 4, local_size_z = 4) in;

//Define our type of image2D so we can write to any pixel we want
layout(binding = 0, r32ui) uniform uimage3D texImage;



uniform vec3 Offset;
uniform ivec3 textureSize;
uniform int distance;

vec4 permute(vec4 x){return mod(((x*34.0)+1.0)*x, 289.0);}
vec4 taylorInvSqrt(vec4 r){return 1.79284291400159 - 0.85373472095314 * r;}

float snoise(vec3 v){ 
  const vec2  C = vec2(1.0/6.0, 1.0/3.0) ;
  const vec4  D = vec4(0.0, 0.5, 1.0, 2.0);

// First corner
  vec3 i  = floor(v + dot(v, C.yyy) );
  vec3 x0 =   v - i + dot(i, C.xxx) ;

// Other corners
  vec3 g = step(x0.yzx, x0.xyz);
  vec3 l = 1.0 - g;
  vec3 i1 = min( g.xyz, l.zxy );
  vec3 i2 = max( g.xyz, l.zxy );

  //  x0 = x0 - 0. + 0.0 * C 
  vec3 x1 = x0 - i1 + 1.0 * C.xxx;
  vec3 x2 = x0 - i2 + 2.0 * C.xxx;
  vec3 x3 = x0 - 1. + 3.0 * C.xxx;

// Permutations
  i = mod(i, 289.0 ); 
  vec4 p = permute( permute( permute( 
             i.z + vec4(0.0, i1.z, i2.z, 1.0 ))
           + i.y + vec4(0.0, i1.y, i2.y, 1.0 )) 
           + i.x + vec4(0.0, i1.x, i2.x, 1.0 ));

// Gradients
// ( N*N points uniformly over a square, mapped onto an octahedron.)
  float n_ = 1.0/7.0; // N=7
  vec3  ns = n_ * D.wyz - D.xzx;

  vec4 j = p - 49.0 * floor(p * ns.z *ns.z);  //  mod(p,N*N)

  vec4 x_ = floor(j * ns.z);
  vec4 y_ = floor(j - 7.0 * x_ );    // mod(j,N)

  vec4 x = x_ *ns.x + ns.yyyy;
  vec4 y = y_ *ns.x + ns.yyyy;
  vec4 h = 1.0 - abs(x) - abs(y);

  vec4 b0 = vec4( x.xy, y.xy );
  vec4 b1 = vec4( x.zw, y.zw );

  vec4 s0 = floor(b0)*2.0 + 1.0;
  vec4 s1 = floor(b1)*2.0 + 1.0;
  vec4 sh = -step(h, vec4(0.0));

  vec4 a0 = b0.xzyw + s0.xzyw*sh.xxyy ;
  vec4 a1 = b1.xzyw + s1.xzyw*sh.zzww ;

  vec3 p0 = vec3(a0.xy,h.x);
  vec3 p1 = vec3(a0.zw,h.y);
  vec3 p2 = vec3(a1.xy,h.z);
  vec3 p3 = vec3(a1.zw,h.w);

//Normalise gradients
  vec4 norm = taylorInvSqrt(vec4(dot(p0,p0), dot(p1,p1), dot(p2, p2), dot(p3,p3)));
  p0 *= norm.x;
  p1 *= norm.y;
  p2 *= norm.z;
  p3 *= norm.w;

// Mix final noise value
  vec4 m = max(0.6 - vec4(dot(x0,x0), dot(x1,x1), dot(x2,x2), dot(x3,x3)), 0.0);
  m = m * m;
  return 42.0 * dot( m*m, vec4( dot(p0,x0), dot(p1,x1), 
                                dot(p2,x2), dot(p3,x3) ) );
}

float fbm(vec3 x) {
	float v = 0.0;
	float a = 0.5;
	vec3 shift = vec3(100);
	for (int i = 0; i < 6; ++i) {
		v += a * snoise(x * 0.005);
		x = x * 2.0 + shift;
		a *= 0.5;
	}
	return v;
}

float map(float value, float min1, float max1, float min2, float max2) {
  return min2 + (value - min1) * (max2 - min2) / (max1 - min1);
}

void main()
{
	ivec3 id = ivec3(gl_GlobalInvocationID.xyz);

	float value = map(round(fbm(id+Offset)),0,1,1,0);

	//Store All Solid Voxels
    imageStore(texImage, ivec3(id), uvec4(value) * 32);


	if(value > .1){
	return;
	}






	int zz = distance;
	int nzz = -distance;

	int yy = distance;
	int nyy = -distance;

	int xx = distance;
	int nxx = -distance;


	//Check if the direction is of the screen if so we dont calculate the points and check if the adjacent point will be calculated
	// its quicker to rerun the fbm than it is to sample the texture for adjacent and more reliable
	//X
	if(id.x + 1 >= textureSize.x || map(round(fbm(ivec3(id.x + 1,id.y,id.z)+Offset)),0,1,1,0) < .9){
	xx = 0;
	}
	if(id.x - 1 < 0 ){
	nxx = 0;
	}
	if(id.y + 1 >= textureSize.y || map(round(fbm(ivec3(id.x ,id.y + 1,id.z)+Offset)),0,1,1,0) < .9){
	yy = 0;
	}
	if(id.x - 1 < 0){
	nyy = 0;
	}
	if(id.z + 1 >= textureSize.z || map(round(fbm(ivec3(id.x ,id.y,id.z + 1)+Offset)),0,1,1,0) < .9){
	zz = 0;
	}
	if(id.x - 1 < 0){
	nzz = 0;
	}

		for(int z = nzz ; z <= zz; z++ ){
			for(int y = nyy ; y <= yy; y++ ){
				for(int x = nxx ; x <= xx; x++ ){

				//Calculate Distance
				float distance = length(vec3(id.x,id.y,id.z)-vec3(id.x + x,id.y + y,id.z + z));
				ivec3 Coord = ivec3(id.x + x, id.y + y, id.z + z);


				//This would cause artifacts due to two pixels being wrote at the same time
				//uint CurrentValue = imageLoad(texImage,ivec3(Coord)).r;
				//imageStore(texImage,ivec3(Coord),uvec4(min(distance,imageLoad(texImage,ivec3(Coord)).r)));

				imageAtomicMin(texImage, Coord,uint((distance)));
				}
			}
		}
	
}