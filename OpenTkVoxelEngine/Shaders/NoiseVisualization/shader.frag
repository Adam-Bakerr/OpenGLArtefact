#version 330 core
out vec4 FragColor;


uniform float time;
uniform vec2 dimensions;
uniform vec3 iro; // ray origin
uniform vec3 cameraForward;
uniform vec3 cameraUp;
uniform vec3 cameraRight;
// Constants
#define PI 3.1415925359
#define TWO_PI 6.2831852
#define MAX_STEPS 100
#define MAX_DIST 100.
#define SURFACE_DIST .01


struct sphere{
    vec3 Position;
    vec3 Color;
};


///noise

float hash(vec3 p)  
{
    p  = 17.0*fract( p*0.3183099+vec3(.11,.17,.13) );
    return fract( p.x*p.y*p.z*(p.x+p.y+p.z) );
}

float sph( ivec3 i, vec3 f, ivec3 c )
{
   // random radius at grid vertex i+c
   float rad = 0.5*hash(i+c);
   // distance to sphere at grid vertex i+c
   return length(f-vec3(c)) - rad; 
}

float sdBase( vec3 p )
{
   ivec3 i = ivec3(floor(p));
    vec3 f =       fract(p);
   // distance to the 8 corners spheres
   return min(min(min(sph(i,f,ivec3(0,0,0)),
                      sph(i,f,ivec3(0,0,1))),
                  min(sph(i,f,ivec3(0,1,0)),
                      sph(i,f,ivec3(0,1,1)))),
              min(min(sph(i,f,ivec3(1,0,0)),
                      sph(i,f,ivec3(1,0,1))),
                  min(sph(i,f,ivec3(1,1,0)),
                      sph(i,f,ivec3(1,1,1)))));
}

// https://iquilezles.org/articles/smin
float smin( float a, float b, float k )
{
    float h = max(k-abs(a-b),0.0);
    return min(a, b) - h*h*0.25/k;
}

// https://iquilezles.org/articles/smin
float smax( float a, float b, float k )
{
    float h = max(k-abs(a-b),0.0);
    return max(a, b) + h*h*0.25/k;
}


float sdFbm( vec3 p, float d )
{
   float s = 1.0;
   for( int i=0; i<int(sin(time)*7); i++ )
   {
       // evaluate new octave
       float n = s*sdBase(p);
	
       // add
       n = smax(n,d-0.1*s,0.3*s);
       d = smin(n,d      ,0.3*s);
	
       // prepare next octave
       p = mat3( 0.00, 1.60, 1.20,
                -1.60, 0.72,-0.96,
                -1.20,-0.96, 1.28 )*p;
       s = 0.5*s;
   }
   return d;
}


///////////////////////
// Boolean Operators
///////////////////////
  
 
vec4 intersectSDF(vec4 a, vec4 b) {
    return a.w > b.w ? a : b;
}
  
vec4 unionSDF(vec4 a, vec4 b) {
    return a.w < b.w? a : b;
}
 
vec4 differenceSDF(vec4 a, vec4 b) {
    return a.w > -b.w? a : vec4(b.rgb,-b.w);
}
 
/////////////////////////////
// Smooth blending operators
/////////////////////////////
 
vec4 smoothIntersectSDF(vec4 distA, vec4 distB, float k ) 
{
  float h = clamp(0.5 - 0.5*(distA.w-distB.w)/k, 0., 1.);
  return mix(distA, distB, h ) + k*h*(1.-h); 
}
 
vec4 smoothUnionSDF(vec4 distA, vec4 distB, float k ) {
  float h = clamp(0.5 + 0.5*(distA.w-distB.w)/k, 0., 1.);
  return mix(distA, distB, h) - k*h*(1.-h); 
}
 
vec4 smoothDifferenceSDF(vec4 distA, vec4 distB, float k) {
  float h = clamp(0.5 - 0.5*(distB.w+distA.w)/k, 0., 1.);
  return mix(distA, -distB, h ) + k*h*(1.-h); 
}

float sdBoxFrame( vec3 p, vec3 b, float e )
{
       p = abs(p  )-b;
  vec3 q = abs(p+e)-e;
  return min(min(
      length(max(vec3(p.x,q.y,q.z),0.0))+min(max(p.x,max(q.y,q.z)),0.0),
      length(max(vec3(q.x,p.y,q.z),0.0))+min(max(q.x,max(p.y,q.z)),0.0)),
      length(max(vec3(q.x,q.y,p.z),0.0))+min(max(q.x,max(q.y,p.z)),0.0));
}
 
/////////////////////////

vec4 GetDist(vec3 p)
{
    sphere testSphere = sphere(vec3(0,sin(time)*3.,.5),vec3(0,1,0));
    vec4 s = vec4(testSphere.Position,1); //Sphere. xyz is position w is radius
    vec4 sphereDist = vec4(testSphere.Color,length(p-s.xyz) - s.w);

  

    vec4 planeDist = vec4(1,1,1,p.y);
    vec4 d = smoothUnionSDF(sphereDist,planeDist,.5);
    vec4 noiseSdf = vec4(1,1,1,sdFbm(p,d.w));
    d = smoothUnionSDF(noiseSdf,d,.5);
    return vec4(d);
}

float sdTriPrism( vec3 p, vec2 h )
{
  vec3 q = abs(p);
  return max(q.z-h.y,max(q.x*0.866025+p.y*0.5,-p.y)-h.x*0.5);
}

float sdBox( vec3 p, vec3 b )
{
  vec3 q = abs(p) - b;
  return length(max(q,0.0)) + min(max(q.x,max(q.y,q.z)),0.0);
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