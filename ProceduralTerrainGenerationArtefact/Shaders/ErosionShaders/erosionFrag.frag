#version 430 core

in vec3 color;
in vec3 normal;
in vec3 fragPos;
in vec2 texCoord;
out vec4 FragColor;


 uniform vec3 LightColor;
 uniform vec3 viewPos;

 struct PointLight {
    vec3 position;  

    vec3 ambient;
    vec3 diffuse;
    vec3 specular;
	
    float constant;
    float linear;
    float quadratic;
}; 

uniform PointLight light;


void main()
{
    // ambient
    vec3 ambient = light.ambient;
    
    // diffuse 
    vec3 norm = normalize(normal);
    vec3 lightDir = normalize(light.position - fragPos);
    float diff = max(dot(norm, lightDir), 0.0);
    vec3 diffuse = light.diffuse * diff;  
    
    // specular
    vec3 viewDir = normalize(viewPos - fragPos);
    vec3 reflectDir = reflect(-lightDir, norm);  
    float spec = pow(max(dot(viewDir, reflectDir), 0.0), 52);
    vec3 specular = light.specular * spec;  
    
    // attenuation
    float Distance = length(light.position - fragPos);
    float attenuation = 1.0 / (light.constant + light.linear * Distance + light.quadratic * (Distance * Distance));
    ambient  *= attenuation; 
    diffuse   *= attenuation;
    specular *= attenuation;   
        
    vec3 result = (ambient + diffuse + specular) * color;
    FragColor = vec4(result, 1.0);
}