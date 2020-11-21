Shader "Custom/POM"
{
    Properties {
        // normal map texture on the material,
        // default to dummy "flat surface" normalmap
        [KeywordEnum(PLAIN, NORMAL, BUMP, POM, POM_SHADOWS)] MODE("Overlay mode", Float) = 0
        
        _NormalMap("Normal Map", 2D) = "bump" {}
        _MainTex("Texture", 2D) = "grey" {}
        _HeightMap("Height Map", 2D) = "white" {}
        _MaxHeight("Max Height", Range(0.0001, 0.02)) = 0.01
        _StepLength("Step Length", Float) = 0.000001
        _MaxStepCount("Max Step Count", Int) = 64
        
        _Reflectivity("Reflectivity", Range(1, 100)) = 0.5
    }
    
    CGINCLUDE
    #include "UnityCG.cginc"
    #include "UnityLightingCommon.cginc"
    
    inline float LinearEyeDepthToOutDepth(float z)
    {
        return (1 - _ZBufferParams.w * z) / (_ZBufferParams.z * z);
    }

    struct v2f {
        float3 worldPos : TEXCOORD0;
        // texture coordinate for the normal map
        float2 uv : TEXCOORD4;
        float4 clip : SV_POSITION;

        float3 T : TEXCOORD1;
        float3 B : TEXCOORD2;
        float3 N : TEXCOORD3;
    };

    // Vertex shader now also gets a per-vertex tangent vector.
    // In Unity tangents are 4D vectors, with the .w component used to indicate direction of the bitangent vector.
    v2f vert (float4 vertex : POSITION, float3 normal : NORMAL, float4 tangent : TANGENT, float2 uv : TEXCOORD0)
    {
        v2f o;
        o.clip = UnityObjectToClipPos(vertex);
        o.worldPos = mul(unity_ObjectToWorld, vertex).xyz;
        half3 wNormal = UnityObjectToWorldNormal(normal);
        half3 wTangent = UnityObjectToWorldDir(tangent.xyz);
        
        o.uv = uv;
        
        // compute bitangent from cross product of normal and tangent and output it
        
        half3 wBitangent = cross(wNormal, wTangent) * tangent.w * unity_WorldTransformParams.w;
        o.T = wTangent;
        o.B = wBitangent;
        o.N = normal;

        return o;
    }

    // normal map texture from shader properties
    sampler2D _NormalMap;
    sampler2D _MainTex;
    sampler2D _HeightMap;
    
    // The maximum depth in which the ray can go.
    uniform float _MaxHeight;
    // Step size
    uniform float _StepLength;
    // Count of steps
    uniform int _MaxStepCount;
    
    float _Reflectivity;

    void frag (in v2f i, out half4 outColor : COLOR, out float outDepth : DEPTH)
    {
        float2 uv = i.uv;
        
        float3 worldViewDir = normalize(UnityWorldSpaceViewDir(i.worldPos));

        float3x3 toTangentBasis = float3x3(i.T, i.B, i.N);
        float3x3 fromTangentBasis = transpose(toTangentBasis);
        
        float3 tangentViewDir = mul(toTangentBasis, worldViewDir);
#if MODE_BUMP
        float height = (1 - tex2D(_HeightMap, uv).x) *  _MaxHeight;
        uv -= tangentViewDir.xy / tangentViewDir.z * height;
#endif   
    
        float depthDif = 0;
#if MODE_POM | MODE_POM_SHADOWS    
        float delta1, delta2;
        float3 shift1, shift2;

        for (int j = 0, stop = 0; j < _MaxStepCount; ++j) {
            float3 shift = tangentViewDir * _StepLength * j;
            float height = tex2D(_HeightMap, uv + shift.xy).x * _MaxHeight;
            if (!stop) {
                shift2 = shift1; shift1 = shift;
                delta2 = delta1; delta1 = height - shift.z;
                if (delta1 < 0) stop = 1;
            }
        }

        uv += lerp(shift1, shift2, delta1 / (delta1 - delta2)); // linear interpolation, delta1 < 0, delta2 > 0
        depthDif = (1 - tex2D(_HeightMap, uv).x) * _MaxHeight;
#endif

        float3 worldLightDir = normalize(_WorldSpaceLightPos0.xyz);
        float3 tangentLightDir = mul(toTangentBasis, worldLightDir);
        float shadow = 0;
#if MODE_POM_SHADOWS
        float height0 = _MaxHeight - depthDif;
        for (int j = 1; j <= _MaxStepCount; ++j) {
            float3 shift = tangentLightDir * _StepLength * j;
            float db = tex2D(_HeightMap, uv + shift.xy).r * _MaxHeight;
            float delta = db - (height0 + shift.z);
            shadow += max(0, delta) / (db + (height0 + shift.z)); // a La soft shadows
        }
#endif
        
        half3 normal = i.N;
#if !MODE_PLAIN
        // Implement Normal Mapping
        normal = mul(fromTangentBasis, UnpackNormal(tex2D(_NormalMap, uv)));
#endif

        // Diffuse lightning
        half cosTheta = max(0, dot(normal, worldLightDir));
        half3 diffuseLight = max(0, cosTheta) * _LightColor0 * max(0, 1 - shadow);
        
        // Specular lighting (ad-hoc)
        half specularLight = pow(max(0, dot(worldViewDir, reflect(worldLightDir, normal))), _Reflectivity) * _LightColor0 * max(0, 1 - shadow); 

        // Ambient lighting
        half3 ambient = ShadeSH9(half4(UnityObjectToWorldNormal(normal), 1));

        // Return resulting color
        float3 texColor = tex2D(_MainTex, uv);
        outColor = half4((diffuseLight + specularLight + ambient) * texColor, 0);
        outDepth = LinearEyeDepthToOutDepth(LinearEyeDepth(i.clip.z - depthDif));
    }
    ENDCG
    
    SubShader
    {    
        Pass
        {
            Name "MAIN"
            Tags { "LightMode" = "ForwardBase" }
        
            ZTest Less
            ZWrite On
            Cull Back
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #pragma multi_compile_local MODE_PLAIN MODE_NORMAL MODE_BUMP MODE_POM MODE_POM_SHADOWS
            ENDCG
            
        }
    }
}