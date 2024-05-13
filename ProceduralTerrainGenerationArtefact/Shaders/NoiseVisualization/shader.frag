#version 330 core
out vec4 FragColor;


uniform float time;
uniform vec2 dimensions;
uniform vec3 iro; // ray origin
uniform vec3 cameraForward;
uniform vec3 cameraUp;
uniform vec3 cameraRight;

//FBM values
uniform int seed;
uniform int NumLayers;
uniform vec3 centre;
uniform float baseRoughness;
uniform float roughness;
uniform float persistence;
uniform float strength;
uniform float scale;
uniform float lacunicity;
uniform float jitter;

uniform int currentNoiseType;

// Constants
#define MAX_STEPS 100
#define MAX_DIST 100.
#define SURFACE_DIST .01


//All Noise functions are taken from https://gist.github.com/patriciogonzalezvivo/670c22f3966e662d2f83

/////////////////////////////
////// Perlin Noise    //////
/////////////////////////////

vec4 permute(vec4 x){return mod(((x*34.0)+1.0)*x, 289.0);}
vec4 taylorInvSqrt(vec4 r){return 1.79284291400159 - 0.85373472095314 * r;}
vec3 fade(vec3 t) {return t*t*t*(t*(t*6.0-15.0)+10.0);}

float pnoise(vec3 P){
  vec3 Pi0 = floor(P); // Integer part for indexing
  vec3 Pi1 = Pi0 + vec3(1.0); // Integer part + 1
  Pi0 = mod(Pi0, 289.0);
  Pi1 = mod(Pi1, 289.0);
  vec3 Pf0 = fract(P); // Fractional part for interpolation
  vec3 Pf1 = Pf0 - vec3(1.0); // Fractional part - 1.0
  vec4 ix = vec4(Pi0.x, Pi1.x, Pi0.x, Pi1.x);
  vec4 iy = vec4(Pi0.yy, Pi1.yy);
  vec4 iz0 = Pi0.zzzz;
  vec4 iz1 = Pi1.zzzz;

  vec4 ixy = permute(permute(ix) + iy);
  vec4 ixy0 = permute(ixy + iz0);
  vec4 ixy1 = permute(ixy + iz1);

  vec4 gx0 = ixy0 / 7.0;
  vec4 gy0 = fract(floor(gx0) / 7.0) - 0.5;
  gx0 = fract(gx0);
  vec4 gz0 = vec4(0.5) - abs(gx0) - abs(gy0);
  vec4 sz0 = step(gz0, vec4(0.0));
  gx0 -= sz0 * (step(0.0, gx0) - 0.5);
  gy0 -= sz0 * (step(0.0, gy0) - 0.5);

  vec4 gx1 = ixy1 / 7.0;
  vec4 gy1 = fract(floor(gx1) / 7.0) - 0.5;
  gx1 = fract(gx1);
  vec4 gz1 = vec4(0.5) - abs(gx1) - abs(gy1);
  vec4 sz1 = step(gz1, vec4(0.0));
  gx1 -= sz1 * (step(0.0, gx1) - 0.5);
  gy1 -= sz1 * (step(0.0, gy1) - 0.5);

  vec3 g000 = vec3(gx0.x,gy0.x,gz0.x);
  vec3 g100 = vec3(gx0.y,gy0.y,gz0.y);
  vec3 g010 = vec3(gx0.z,gy0.z,gz0.z);
  vec3 g110 = vec3(gx0.w,gy0.w,gz0.w);
  vec3 g001 = vec3(gx1.x,gy1.x,gz1.x);
  vec3 g101 = vec3(gx1.y,gy1.y,gz1.y);
  vec3 g011 = vec3(gx1.z,gy1.z,gz1.z);
  vec3 g111 = vec3(gx1.w,gy1.w,gz1.w);

  vec4 norm0 = taylorInvSqrt(vec4(dot(g000, g000), dot(g010, g010), dot(g100, g100), dot(g110, g110)));
  g000 *= norm0.x;
  g010 *= norm0.y;
  g100 *= norm0.z;
  g110 *= norm0.w;
  vec4 norm1 = taylorInvSqrt(vec4(dot(g001, g001), dot(g011, g011), dot(g101, g101), dot(g111, g111)));
  g001 *= norm1.x;
  g011 *= norm1.y;
  g101 *= norm1.z;
  g111 *= norm1.w;

  float n000 = dot(g000, Pf0);
  float n100 = dot(g100, vec3(Pf1.x, Pf0.yz));
  float n010 = dot(g010, vec3(Pf0.x, Pf1.y, Pf0.z));
  float n110 = dot(g110, vec3(Pf1.xy, Pf0.z));
  float n001 = dot(g001, vec3(Pf0.xy, Pf1.z));
  float n101 = dot(g101, vec3(Pf1.x, Pf0.y, Pf1.z));
  float n011 = dot(g011, vec3(Pf0.x, Pf1.yz));
  float n111 = dot(g111, Pf1);

  vec3 fade_xyz = fade(Pf0);
  vec4 n_z = mix(vec4(n000, n100, n010, n110), vec4(n001, n101, n011, n111), fade_xyz.z);
  vec2 n_yz = mix(n_z.xy, n_z.zw, fade_xyz.y);
  float n_xyz = mix(n_yz.x, n_yz.y, fade_xyz.x); 
  return 2.2 * n_xyz;
}

float pfbm(vec3 x) {
	float v = 0.0;
	float a = baseRoughness;
	float freq = scale;
	for (int i = 0; i < NumLayers; ++i) {
		float eval = a * pnoise((x*freq) + centre);
		v += (eval + 1) * .5f;
		x = x * roughness;
		a *= persistence;
		freq *= 2;
	}
	v = max(0, v);
	return v * strength;
}

/////////////////////////////


/////////////////////////////
////// Simplex Noise    //////
/////////////////////////////

//	Simplex 3D Noise 
//	by Ian McEwan, Ashima Arts
//
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

float sfbm(vec3 x) {
	float v = 0.0;
	float a = baseRoughness;
	float freq = scale;
	for (int i = 0; i < NumLayers; ++i) {
		float eval = a * snoise((x*freq) + centre);
		v += (eval + 1) * .5f;
		x = x * roughness;
		a *= persistence;
		freq *= 2;
	}
	v = max(0, v);
	return v * strength;
}

//


//1/7
#define K 0.142857142857
//3/7
#define Ko 0.428571428571

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

vec3 Permutation(vec3 x)
{
    return mod((34.0 * x + 1.0) * x, 289.0);
}

vec2 vnoise(vec2 P, float jitter)
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

float vfbm(vec2 x, float jitter) {
	float v = 0.0;
	float a = baseRoughness;
	float freq = scale;
	for (int i = 0; i < NumLayers; ++i) {
		float eval = a * vnoise((x*freq) + centre.xz ,jitter).x;
		v += (eval + 1) * .5f;
		x = x * roughness;
		a *= persistence;
		freq *= 2;
	}
	v = max(0, v);
	return v * strength;
}

float GetDist(vec3 p)
{
    float planeDist = p.y;
    return float(planeDist);
}

vec3 CalcRayDirection(vec2 uv){
 return normalize(uv.x * cameraRight + uv.y * cameraUp + 1.0 * cameraForward);
}

//Normal noise
vec3 normalNoise(vec2 _st, float _zoom, float _speed){
	vec2 v1 = _st;
	vec2 v2 = _st;
	vec2 v3 = _st;
	float expon = pow(10.0, _zoom*2.0);
	v1 /= 1.0*expon;
	v2 /= 0.62*expon;
	v3 /= 0.83*expon;
	float n = time*_speed;
	float nr = (snoise(vec3(v1, n)) + snoise(vec3(v2, n)) + snoise(vec3(v3, n))) / 6.0 + 0.5;
	n = time * _speed + 1000.0;
	float ng = (snoise(vec3(v1, n)) + snoise(vec3(v2, n)) + snoise(vec3(v3, n))) / 6.0 + 0.5;
	return vec3(nr,ng,0.5);
}

//Curl noise1

vec3 snoise3( vec3 x ){
    float s  = snoise(vec3( x ));
    float s1 = snoise(vec3( x.y - 19.1 , x.z + 33.4 , x.x + 47.2 ));
    float s2 = snoise(vec3( x.z + 74.2 , x.x - 124.5 , x.y + 99.4 ));
    return vec3( s , s1 , s2 );
}

vec3 curl( vec3 p ){
    const float e = .1;
    vec3 dx = vec3( e   , 0.0 , 0.0 );
    vec3 dy = vec3( 0.0 , e   , 0.0 );
    vec3 dz = vec3( 0.0 , 0.0 , e   );

    vec3 p_x0 = snoise3( p - dx );
    vec3 p_x1 = snoise3( p + dx );
    vec3 p_y0 = snoise3( p - dy );
    vec3 p_y1 = snoise3( p + dy );
    vec3 p_z0 = snoise3( p - dz );
    vec3 p_z1 = snoise3( p + dz );

    float x = p_y1.z - p_y0.z - p_z1.y + p_z0.y;
    float y = p_z1.x - p_z0.x - p_x1.z + p_x0.z;
    float z = p_x1.y - p_x0.y - p_y1.x + p_y0.x;

    const float divisor = 1.0 / ( 2.0 * e );
    #ifndef CURL_UNNORMALIZED
    return normalize( vec3( x , y , z ) * divisor );
    #else
    return vec3( x , y , z ) * divisor;
    #endif
}

vec3 cfbm(vec3 x) {
	vec3 v = vec3(0);
	float a = baseRoughness;
	float freq = scale;
	for (int i = 0; i < NumLayers; ++i) {
		vec3 eval = a * curl((x*freq) + centre);
		v += (eval + 1) * .5f;
		x = x * roughness;
		a *= persistence;
		freq *= 2;
	}
	v = max(vec3(0), v);
	return v * strength;
}

vec3 GetNormal(vec3 p)
{ 
    float d = GetDist(p); // Distance
    vec2 e = vec2(.01,0); // Epsilon
    vec3 n = d - vec3(
    GetDist(p-e.xyy),  
    GetDist(p-e.yxy),
    GetDist(p-e.yyx));
   
    return normalize(n);
}


vec3 DomainWarping(vec3 point){
    float x = pfbm(point + pfbm(point + pfbm(point)));
    return vec3(x);
}

// 0 - perlin 1 - perlinfbm 2 - Vornori 3 - Vornorifbm 4 - Curl 5 - Curlfbm 6 - snoise 7 - snoisefbm 8 - Domainwarping 9 - normalnoise
vec3 SampleColorWithNoise(vec3 point){
	vec3 color = vec3(0);
    float value = 0;

	if(currentNoiseType == 0){
		color = vec3(pnoise(point));
	}else if (currentNoiseType == 1){
		color = vec3(pfbm(point));
	}else if (currentNoiseType == 2){
        color = vec3(vnoise(point.xz,1),1);
	}else if (currentNoiseType == 3){
        color = vec3(vfbm(point.xz,jitter) / NumLayers);
	}else if (currentNoiseType == 4){
        color = vec3(curl(point));
	}else if (currentNoiseType == 5){
        color = vec3(cfbm(point));
	}else if (currentNoiseType == 6){
        color = vec3(snoise(point));
	}else if (currentNoiseType == 7){
        color = vec3(sfbm(point));
	}else if (currentNoiseType == 8){
        color = DomainWarping(point);
	}else if (currentNoiseType == 9){
        color = normalNoise(point.xz,.1,1);
	}

    



	return vec3(color);
}

float RayMarch(vec3 ro, vec3 rd, inout vec3 color){
    float dO = 0.; //Distane Origin

    for(int i=0;i<MAX_STEPS;i++)
    {
        vec3 p = ro + rd * dO;
        float ds = GetDist(p); // ds is Distance Scene
        dO += ds;
        if(dO > MAX_DIST || ds < SURFACE_DIST){
        color = SampleColorWithNoise(p);
        break;
        };
    }
    return dO;
}

vec3 difColor = vec3(1,1,1);

vec3 GetLight(vec3 p, vec3 c)
{
    // Diffuse Color
    vec3 color = c.rgb * 1;
 
    // Directional light
    vec3 lightPos=vec3(5.*sin(time),5.,6.+5.*cos(time));// Light Position
 
    vec3 l=normalize(lightPos-p);// Light Vector
    vec3 n=GetNormal(p);// Normal Vector
     
    float dif=dot(n,l);// Diffuse light
    dif=clamp(dif,0.,1.);// Clamp so it doesnt go below 0
     
    // Shadows
    float d=RayMarch(p+n*SURFACE_DIST*2.,l,difColor);
     
    if(d<length(lightPos-p))dif*=.1;
     
    return color * dif;
}

void main()
{
    vec2 uv = (gl_FragCoord.xy - .5 *  dimensions.xy) / dimensions.y;
    vec3 rd = CalcRayDirection(uv);
    vec3 surfaceColor = vec3(0);
    float d = RayMarch(iro,rd,surfaceColor);// Distance
     
    vec3 p = iro+rd*d;
    vec3 color = GetLight(p,surfaceColor);// Diffuse lighting
     
    // Set the output color
    FragColor=vec4(color,1.);
}