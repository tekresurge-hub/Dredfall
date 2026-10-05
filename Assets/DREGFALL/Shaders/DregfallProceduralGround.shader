Shader "DREGFALL/ProceduralGround"
{
    Properties
    {
        _GrassTex ("Grass", 2D) = "white" {}
        _MudTex ("Mud", 2D) = "white" {}
        _SwampTex ("Forest Floor", 2D) = "white" {}
        _RockTex ("Rock", 2D) = "white" {}
        _Tiling ("Tiling", Float) = 0.18
        _VariationScale ("Variation Scale", Float) = 0.012
        _RockSlopeStart ("Rock Slope Start", Range(0,1)) = 0.32
        _RockSlopeEnd ("Rock Slope End", Range(0,1)) = 0.68
        _Brightness ("Brightness", Range(0.5,1.5)) = 0.92
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_GrassTex); SAMPLER(sampler_GrassTex);
            TEXTURE2D(_MudTex); SAMPLER(sampler_MudTex);
            TEXTURE2D(_SwampTex); SAMPLER(sampler_SwampTex);
            TEXTURE2D(_RockTex); SAMPLER(sampler_RockTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _GrassTex_ST;
                float4 _MudTex_ST;
                float4 _SwampTex_ST;
                float4 _RockTex_ST;
                float _Tiling;
                float _VariationScale;
                float _RockSlopeStart;
                float _RockSlopeEnd;
                float _Brightness;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 shadowCoord : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.shadowCoord = GetShadowCoord(p);
                return o;
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float2 uv = i.positionWS.xz * _Tiling;

                // Large, soft ecological patches that remain continuous across streamed chunks.
                float2 cell = floor(i.positionWS.xz * _VariationScale);
                float2 f = frac(i.positionWS.xz * _VariationScale);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(cell);
                float b = hash21(cell + float2(1,0));
                float c = hash21(cell + float2(0,1));
                float d = hash21(cell + float2(1,1));
                float variation = lerp(lerp(a,b,f.x), lerp(c,d,f.x), f.y);

                half3 grass = SAMPLE_TEXTURE2D(_GrassTex, sampler_GrassTex, uv).rgb;
                half3 mud = SAMPLE_TEXTURE2D(_MudTex, sampler_MudTex, uv * 0.82).rgb;
                half3 forest = SAMPLE_TEXTURE2D(_SwampTex, sampler_SwampTex, uv * 0.70).rgb;
                half3 rock = SAMPLE_TEXTURE2D(_RockTex, sampler_RockTex, uv * 0.55).rgb;

                float mudMask = smoothstep(0.46, 0.67, variation) * (1.0 - smoothstep(0.72, 0.90, variation));
                float forestMask = smoothstep(0.70, 0.92, variation);
                half3 baseColor = lerp(grass, mud, mudMask * 0.72);
                baseColor = lerp(baseColor, forest, forestMask * 0.78);

                float slope = 1.0 - saturate(n.y);
                float rockMask = smoothstep(_RockSlopeStart, _RockSlopeEnd, slope);
                baseColor = lerp(baseColor, rock, rockMask);

                // Subtle macro tint stops the world looking like one repeated texture.
                float macro = lerp(0.88, 1.08, variation);
                baseColor *= macro * _Brightness;

                Light mainLight = GetMainLight(i.shadowCoord);
                float ndl = saturate(dot(n, mainLight.direction));
                half3 ambient = SampleSH(n);
                half3 lighting = ambient + mainLight.color * ndl * mainLight.shadowAttenuation;
                lighting = max(lighting, 0.24);
                return half4(baseColor * lighting, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
