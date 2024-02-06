#version 430 core
#define FLT_max 3.402823466e+38
#define FLT_MIN 1.175494351e-38
uniform sampler3D voxelTexture;

uniform ivec2 dimensions;
uniform ivec3 textureDims;

uniform vec3 cameraForward;
uniform vec3 cameraRight;
uniform vec3 cameraUp;
uniform vec3 cameraPos;


uniform vec3 position;
uniform float voxelSize;

out vec4 FragColor;




vec3 CalcRayDirection(vec2 uv){
 return normalize(uv.x * cameraRight + uv.y * cameraUp + 1.0 * cameraForward);
}

float maxv3(vec3 v) { return max (max(v.x, v.y), v.z); }
float minv3(vec3 v) { return min (min(v.x, v.y), v.z); }

bool intersectAABB(vec3 rayOrigin, vec3 rayDir, vec3 boxMin, vec3 boxMax , inout float tNear, inout float tFar) {
    vec3 tMin = (boxMin - rayOrigin) / rayDir;
    vec3 tMax = (boxMax - rayOrigin) / rayDir;
    vec3 t1 = min(tMin, tMax);
    vec3 t2 = max(tMin, tMax);
    tNear = max(max(t1.x, t1.y), t1.z);
    tFar = min(min(t2.x, t2.y), t2.z);
    return tNear <= tFar;
};

ivec3 CalculateDirectionSteps(vec3 rd){
    int x = rd.x >= 0 ? 1 : -1;
    int y = rd.y >= 0 ? 1 : -1;
    int z = rd.z >= 0 ? 1 : -1;
    return ivec3(x,y,z);
}

ivec3 RoundToVoxel(vec3 p){
    
    float x = floor(p.x / (voxelSize));
    float y = floor(p.y / (voxelSize));
    float z = floor(p.z / (voxelSize));
    return ivec3(x,y,z);
}


bool SampleColorAtVoxel(ivec3 p, out vec4 color){
    color = texture(voxelTexture, vec3(p) * voxelSize);
    if(color.x > 0 || color.y > 0 || color.z > 0){
        return true;
    }
    return false;
}

bool InBounds(ivec3 p){
    return(
    p.x >= 0 && p.x < textureDims.x &&
    p.y >= 0 && p.y < textureDims.y &&
    p.z >= 0 && p.z < textureDims.z
    );
}

void main()
{
    vec2 uv = (gl_FragCoord.xy - dimensions.xy * .5) / dimensions.y;

    vec3 rd = CalcRayDirection(uv);

    vec3 ro = cameraPos;

    float near; // t at which we first enter the voxel bound
    float far;  // t at which we first exit the voxel bound


    vec3 minb = position;
    vec3 maxb = position + (vec3(1));
   

    if(!intersectAABB(ro,rd,minb,maxb,near,far))return;
   
    vec3 pointOfEntry = ro + rd * near;
    pointOfEntry = max(pointOfEntry,minb);

    vec3 pointOfExit = ro + rd * far;
    pointOfExit = min(pointOfExit,maxb);

    //convert points relative to the bounds
    pointOfEntry -= position;
    pointOfExit -= position;

    
    vec3 ray = normalize(pointOfExit - pointOfEntry);

    ivec3 CurrentVoxel = RoundToVoxel(pointOfEntry);
    ivec3 ExitVoxel =  RoundToVoxel(pointOfExit);

    ivec3 steps = CalculateDirectionSteps(rd);

    //Max amount of time taken to cross a boundary in each direction
    float nextBoundaryx = (CurrentVoxel.x + steps.x) * voxelSize;
    float nextBoundaryy = (CurrentVoxel.y + steps.y) * voxelSize;
    float nextBoundaryz = (CurrentVoxel.z + steps.z) * voxelSize;


    float tmaxX = ray.x != 0 ? (nextBoundaryx - pointOfEntry.x)/ray.x : (nextBoundaryx - pointOfEntry.x) * 0.0001;
    float tmaxY = ray.y != 0 ? (nextBoundaryy - pointOfEntry.y)/ray.y : (nextBoundaryy - pointOfEntry.y) * 0.0001;
    float tmaxZ = ray.z != 0 ? (nextBoundaryz - pointOfEntry.z)/ray.z : (nextBoundaryz - pointOfEntry.z) * 0.0001;

    float tdeltaX = ray.x != 0 ? voxelSize / ray.x * steps.x : voxelSize / 0.0001 * steps.x;
    float tdeltaY = ray.y != 0 ? voxelSize / ray.y * steps.y : voxelSize / 0.0001 * steps.y;
    float tdeltaZ = ray.z != 0 ? voxelSize / ray.z * steps.z : voxelSize / 0.0001 * steps.z;


    if (CurrentVoxel[0]!=ExitVoxel[0] && steps.x == -1) { CurrentVoxel.x--; }
    if (CurrentVoxel[1]!=ExitVoxel[1] && steps.y == -1) { CurrentVoxel.y--; }
    if (CurrentVoxel[2]!=ExitVoxel[2] && steps.z == -1) { CurrentVoxel.z--; }

    vec3 n = vec3(0);
    while(InBounds(CurrentVoxel)) {
    vec4 color = vec4(0);
    if(SampleColorAtVoxel(CurrentVoxel, color)){
        FragColor = vec4((vec3(n) + vec3(.5)) / 2.0,1);
        return;
    }
    if (tmaxX < tmaxY) {
      if (tmaxX < tmaxZ) {
        CurrentVoxel[0] += steps.x;
        tmaxX += tdeltaX;
        n = vec3(steps.x,0,0);
      } else {
        CurrentVoxel[2] += steps.z;
        tmaxZ += tdeltaZ;
        n = vec3(0,0,steps.z);
      }
    } else {
      if (tmaxY < tmaxZ) {
        CurrentVoxel[1] += steps.y;
        tmaxY += tdeltaY;
        n = vec3(0,steps.y,0);
      } else {
        CurrentVoxel[2] += steps.z;
        tmaxZ += tdeltaZ;
        n = vec3(0,0,steps.z);
      }
    }
  }

}