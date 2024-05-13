#version 330 core
out vec4 FragColor;

in vec4 color;
in vec4 normal;
in vec3 fragPos;

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
    vec3 norm = normalize(normal.xyz);
    vec3 lightDir = normalize(light.position - fragPos);
    float diff = max(dot(norm, lightDir), 0.0);
    vec3 diffuse = light.diffuse * diff;  
    
    // specular
    vec3 viewDir = normalize(viewPos - fragPos);
    vec3 reflectDir = reflect(-lightDir, norm);  
    float spec = pow(max(dot(viewDir, reflectDir), 0.0), 1);
    vec3 specular = light.specular * spec;  
    
    // attenuation
    float Distance = length(light.position - fragPos);
    float attenuation = 1.0 / (light.constant + light.linear * Distance + light.quadratic * (Distance * Distance));
    ambient  *= attenuation; 
    diffuse   *= attenuation;
    specular *= attenuation;   
        
    vec3 result = (ambient + diffuse + specular) * color.xyz;
    FragColor = vec4(result,1);
    //FragColor = normal * .8 + vec4(1) * .2;
}