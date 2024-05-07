#version 430


//Define size of local workgroup
layout(local_size_x = 1024) in;

struct vertex{
	vec4 Pos;
	vec4 Color;
	vec4 Normal;
};

struct droplet{
	vec2 pos;
};

uniform ivec2 vertexCount;
uniform int borderSize;
uniform int brushLength;
uniform int particleCount;

uniform int maxLifetime;
uniform float inertia;
uniform float sedimentCapacityFactor;
uniform float minSedimentCapacity;
uniform float depositSpeed;
uniform float erodeSpeed;

uniform float evaporateSpeed;
uniform float gravity;
uniform float startSpeed;
uniform float startWater;


layout(std430, binding = 0) buffer HeightMap
{
    float map[];
};

layout(std430, binding = 1) buffer Particle
{
    vec2 Particles[];
};

layout(std430, binding = 2) buffer brushIndexBuffer
{
    int brushIndices[];
};

layout(std430, binding = 3) buffer brushWeightBuffer
{
    float brushWeights[];
};

layout(std430, binding = 4) buffer vertexBuffer
{
    vertex verticies[];
};


// Returns xGradient yGradient Height
vec3 CalculateHeightAndGradient (vec2 pos) {
    //Get x&y coord from current cell
    ivec2 coord = ivec2(floor(pos));

    //Calculate the dropless position from within the cell, to get the offset for bi interpolation
    vec2 dropletPos = pos - coord;

    // Calculate heights of the four nodes of the droplet's cell
    int flooredNodeIndex = coord.y * vertexCount.x + coord.x;

    //Get the height values of the 4 nodes of this "quadrent"
    float heightNW = map[flooredNodeIndex];
    float heightNE = map[flooredNodeIndex + 1];
    float heightSW = map[flooredNodeIndex + vertexCount.x];
    float heightSE = map[flooredNodeIndex + vertexCount.x + 1];

    // Calculate droplet's direction of flow with bilinear interpolation of height difference along the edges
    float X = (heightNE - heightNW) * (1 - dropletPos.y) + (heightSE - heightSW) * dropletPos.y;
    float Y = (heightSW - heightNW) * (1 - dropletPos.x) + (heightSE - heightNE) * dropletPos.x;

    // Calculate height with bilinear interpolation of the heights of the nodes of the cell
    float height = heightNW * (1 - dropletPos.x) * (1 - dropletPos.y) + heightNE * dropletPos.x * (1 - dropletPos.y) + heightSW * (1 - dropletPos.x) * dropletPos.y + heightSE * dropletPos.x * dropletPos.y;

    return vec3(X,Y,height);
}

void main()
{ 
    ivec2 id = ivec2(gl_GlobalInvocationID.xy);

    if(id.x >= particleCount){
        return;
    }

    vec2 pos = Particles[id.x];
    float posX = pos.x;
    float posY = pos.y;

    vec2 dir = vec2(0);
    float speed = startSpeed;
    float water = startWater;
    float sediment = 0;

    int dropletIndex;
    float cellOffsetX = 0;
    float cellOffsetY = 0;

    for (int lifetime = 0; lifetime < maxLifetime; lifetime ++) {
        int nodeX = int(floor(posX));
        int nodeY = int(floor(posY));
        dropletIndex = nodeY * vertexCount.x + nodeX;

        // Calculate droplet's offset inside the cell
        cellOffsetX = posX - nodeX;
        cellOffsetY = posY - nodeY;

        // Calculate droplet's height and direction of flow with interpolation of in respect to the neighbouring heights
        vec3 heightAndGradient = CalculateHeightAndGradient (vec2(posX,posY));

        // we update the droplets positon based of its current speed
        dir.x = (dir.x * inertia - heightAndGradient.x * (1 - inertia));
        dir.y = (dir.y * inertia - heightAndGradient.y * (1 - inertia));
        dir = normalize(dir);

        //Move the droplet
        posX += dir.x;
        posY += dir.y;


        //break condition if we are too close to the edge of the map of if we are not moving anymore
        if ((dir.x == 0 && dir.y == 0) || posX < borderSize || posX > vertexCount.x - borderSize || posY < borderSize || posY > vertexCount.x - borderSize) {
            map[dropletIndex] += sediment * (1 - cellOffsetX) * (1 - cellOffsetY);
            map[dropletIndex + 1] += sediment * cellOffsetX * (1 - cellOffsetY);
            map[dropletIndex + vertexCount.x] += sediment * (1 - cellOffsetX) * cellOffsetY;
            map[dropletIndex + vertexCount.x + 1] += sediment * cellOffsetX * cellOffsetY;
            break;
        }


        //Get the height after travel and compare to the previous height to calculate the possible sediment we can take
        float newHeight = CalculateHeightAndGradient (vec2(posX, posY)).z;
        float deltaHeight = newHeight - heightAndGradient.z;

        //Calculate the current capacity of the droplet (not the current sediment held)
        float sedimentCapacity = max(-deltaHeight * speed * water * sedimentCapacityFactor, minSedimentCapacity);
        
        //We check if we are moving up hill or if we have too much sediment in the droplet
         if (sediment > sedimentCapacity || deltaHeight > 0) {
            float amountToDeposit = (deltaHeight > 0) ? min(deltaHeight, sediment) : (sediment - sedimentCapacity) * depositSpeed;
            sediment -= amountToDeposit;

            //Deposit some of the sediment ensuring we never deposit more ore less than the droplet has, interoprlating it across the 4 positons of the current quadrent
            map[dropletIndex] += clamp(amountToDeposit * (1 - cellOffsetX) * (1 - cellOffsetY),0,1);
            map[dropletIndex + 1] += clamp(amountToDeposit * cellOffsetX * (1 - cellOffsetY),0,1);
            map[dropletIndex + vertexCount.x] += clamp(amountToDeposit * (1 - cellOffsetX) * cellOffsetY,0,1);
            map[dropletIndex + vertexCount.x + 1] += clamp(amountToDeposit * cellOffsetX * cellOffsetY,0,1);
        }
        else {
            //We erode some of the sediment that the drop has off a given radius of terrain
            //and use a terrain brush to effect multiple heights at once, to accuratly represent a drop of water on a larger scale instead of per point
            float amountToErode = min ((sedimentCapacity - sediment) * erodeSpeed, -deltaHeight);
            
            for (int i = 0; i < brushLength; i ++) {
                int erodeIndex = dropletIndex + brushIndices[i];

                float weightedErodeAmount = amountToErode * brushWeights[i];
                float deltaSediment = (map[erodeIndex] < weightedErodeAmount) ? map[erodeIndex] : weightedErodeAmount;
                map[erodeIndex] -= clamp(deltaSediment,0,1);
                sediment += deltaSediment;
            }
        }

        //Update the speed and voulme of our current droplet
        speed = sqrt (max(0,speed * speed + deltaHeight * gravity));
        water *= (1 - evaporateSpeed);
    }
}