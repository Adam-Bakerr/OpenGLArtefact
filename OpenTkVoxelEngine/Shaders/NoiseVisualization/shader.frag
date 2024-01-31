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


// Constants
#define MAX_STEPS 100
#define MAX_DIST 100.
#define SURFACE_DIST .01

 
/////////////////////////////
// Smooth blending operators
/////////////////////////////
vec4 smoothUnionSDF(vec4 distA, vec4 distB, float k ) {
  float h = clamp(0.5 + 0.5*(distA.w-distB.w)/k, 0., 1.);
  return mix(distA, distB, h) - k*h*(1.-h); 
}
/////////////////////////



/////////////////////////////
////// Noise Functions //////
/////////////////////////////

vec4 permute(vec4 x) { return mod(((x * 34.0) + 1.0) * x, 289.0); }
vec4 taylorInvSqrt(vec4 r) { return 1.79284291400159 - 0.85373472095314 * r; }

float noise(vec3 v) {
	const vec2  C = vec2(1.0 / 6.0, 1.0 / 3.0);
	const vec4  D = vec4(0.0, 0.5, 1.0, 2.0);

	// First corner
	vec3 i = floor(v + dot(v, C.yyy));
	vec3 x0 = v - i + dot(i, C.xxx);

	// Other corners
	vec3 g = step(x0.yzx, x0.xyz);
	vec3 l = 1.0 - g;
	vec3 i1 = min(g.xyz, l.zxy);
	vec3 i2 = max(g.xyz, l.zxy);

	//  x0 = x0 - 0. + 0.0 * C 
	vec3 x1 = x0 - i1 + 1.0 * C.xxx;
	vec3 x2 = x0 - i2 + 2.0 * C.xxx;
	vec3 x3 = x0 - 1. + 3.0 * C.xxx;

	// Permutations
	i = mod(i, 289.0);
	vec4 p = permute(permute(permute(
		i.z + vec4(0.0, i1.z, i2.z, 1.0))
		+ i.y + vec4(0.0, i1.y, i2.y, 1.0))
		+ i.x + vec4(0.0, i1.x, i2.x, 1.0));

	// Gradients
	// ( N*N points uniformly over a square, mapped onto an octahedron.)
	float n_ = 1.0 / 7.0; // N=7
	vec3  ns = n_ * D.wyz - D.xzx;

	vec4 j = p - 49.0 * floor(p * ns.z * ns.z);  //  mod(p,N*N)

	vec4 x_ = floor(j * ns.z);
	vec4 y_ = floor(j - 7.0 * x_);    // mod(j,N)

	vec4 x = x_ * ns.x + ns.yyyy;
	vec4 y = y_ * ns.x + ns.yyyy;
	vec4 h = 1.0 - abs(x) - abs(y);

	vec4 b0 = vec4(x.xy, y.xy);
	vec4 b1 = vec4(x.zw, y.zw);

	vec4 s0 = floor(b0) * 2.0 + 1.0;
	vec4 s1 = floor(b1) * 2.0 + 1.0;
	vec4 sh = -step(h, vec4(0.0));

	vec4 a0 = b0.xzyw + s0.xzyw * sh.xxyy;
	vec4 a1 = b1.xzyw + s1.xzyw * sh.zzww;

	vec3 p0 = vec3(a0.xy, h.x);
	vec3 p1 = vec3(a0.zw, h.y);
	vec3 p2 = vec3(a1.xy, h.z);
	vec3 p3 = vec3(a1.zw, h.w);

	//Normalise gradients
	vec4 norm = taylorInvSqrt(vec4(dot(p0, p0), dot(p1, p1), dot(p2, p2), dot(p3, p3)));
	p0 *= norm.x;
	p1 *= norm.y;
	p2 *= norm.z;
	p3 *= norm.w;

	// Mix final noise value
	vec4 m = max(0.6 - vec4(dot(x0, x0), dot(x1, x1), dot(x2, x2), dot(x3, x3)), 0.0);
	m = m * m;
	return 42.0 * dot(m * m, vec4(dot(p0, x0), dot(p1, x1),
		dot(p2, x2), dot(p3, x3)));
}

float fbm(vec3 x) {
	float v = 0.0;
	float a = baseRoughness;
	float freq = scale;
	for (int i = 0; i < NumLayers; ++i) {
		float eval = a * noise((x*freq) + centre);
		v += (eval + 1) * .5f;
		x = x * roughness;
		a *= persistence;
		freq *= 2;
	}
	v = max(0, v);
	return v * strength;
}

/////////////////////////////


vec4 GetDist(vec3 p)
{
    vec4 planeDist = vec4(vec3(fbm(p)),p.y);
    return vec4(planeDist);
}

vec3 CalcRayDirection(vec2 uv){
 return normalize(uv.x * cameraRight + uv.y * cameraUp + 1.0 * cameraForward);
}

vec3 GetNormal(vec3 p)
{ 
    float d = GetDist(p).w; // Distance
    vec2 e = vec2(.01,0); // Epsilon
    vec3 n = d - vec3(
    GetDist(p-e.xyy).w,  
    GetDist(p-e.yxy).w,
    GetDist(p-e.yyx).w);
   
    return normalize(n);
}

float RayMarch(vec3 ro, vec3 rd, inout vec3 color){
    float dO = 0.; //Distane Origin

    for(int i=0;i<MAX_STEPS;i++)
    {
        vec3 p = ro + rd * dO;
        vec4 ds = GetDist(p); // ds is Distance Scene
        dO += ds.w;
        if(dO > MAX_DIST || ds.w< SURFACE_DIST){
        color = ds.xyz;
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