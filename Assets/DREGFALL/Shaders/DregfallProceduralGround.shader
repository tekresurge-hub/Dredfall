Shader "DREGFALL/ProceduralGround"
{
    Properties
    {
        _GrassTex ("Sparse Grass Accent", 2D) = "white" {}
        _MudTex ("Earth / Soil", 2D) = "white" {}
        _SwampTex ("Forest Floor", 2D) = "white" {}
        _RockTex ("Rock", 2D) = "white" {}
        _Tiling ("Detail Tiling", Float) = 0.12
        _VariationScale ("Biome Variation Scale", Float) = 0.006
        _RockSlopeStart ("Rock Slope Start", Range(0,1)) = 0.30
        _RockSlopeEnd ("Rock Slope End", Range(0,1)) = 0.64
        _Brightness ("Brightness", Range(0.5,1.5)) = 0.90
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
                float4 _GrassTex_ST, _MudTex_ST, _SwampTex_ST, _RockTex_ST;
                float _Tiling, _VariationScale, _RockSlopeStart, _RockSlopeEnd, _Brightness;
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

            float valueNoise(float2 p)
            {
                float2 c = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(c);
                float b = hash21(c + float2(1,0));
                float d = hash21(c + float2(0,1));
                float e = hash21(c + float2(1,1));
                return lerp(lerp(a,b,f.x), lerp(d,e,f.x), f.y);
            }

            half3 sampleBroken(TEXTURE2D_PARAM(tex, samp), float2 worldXZ, float scale, float offset)
            {
                half3 a = SAMPLE_TEXTURE2D(tex, samp, worldXZ * scale + offset).rgb;
                half3 b = SAMPLE_TEXTURE2D(tex, samp, worldXZ * (scale * 0.47) + float2(offset * 1.73, offset * 0.61)).rgb;
                half3 c = SAMPLE_TEXTURE2D(tex, samp, worldXZ * (scale * 0.19) + float2(offset * 0.37, offset * 2.11)).rgb;
                return a * 0.54 + b * 0.29 + c * 0.17;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float2 w = i.positionWS.xz;

                // Large continuous biome masks: no chunk seams and no checkerboard cells.
                float macroA = valueNoise(w * _VariationScale);
                float macroB = valueNoise(w * (_VariationScale * 0.37) + 41.7);
                float micro = valueNoise(w * 0.025 + 9.3);
                float ecology = saturate(macroA * 0.58 + macroB * 0.32 + micro * 0.10);

                // Earth is deliberately the dominant surface.
                half3 earth = sampleBroken(TEXTURE2D_ARGS(_MudTex, sampler_MudTex), w, _Tiling * 0.70, 5.7);
                half3 forest = sampleBroken(TEXTURE2D_ARGS(_SwampTex, sampler_SwampTex), w, _Tiling * 0.58, 17.2);
                half3 grass = sampleBroken(TEXTURE2D_ARGS(_GrassTex, sampler_GrassTex), w, _Tiling * 0.52, 31.4);
                half3 rock = sampleBroken(TEXTURE2D_ARGS(_RockTex, sampler_RockTex), w, _Tiling * 0.42, 11.8);

                // Pull imported textures into a muted, realistic DREGFALL palette.
                float earthLum = dot(earth, half3(0.299,0.587,0.114));
                earth = lerp(earth, earthLum.xxx, 0.18) * half3(0.82,0.76,0.65);
                float forestLum = dot(forest, half3(0.299,0.587,0.114));
                forest = lerp(forest, forestLum.xxx, 0.25) * half3(0.73,0.72,0.58);
                float grassLum = dot(grass, half3(0.299,0.587,0.114));
                grass = lerp(grass, grassLum.xxx, 0.55) * half3(0.62,0.70,0.53);

                // Forest litter appears in broad damp/wooded patches.
                float forestMask = smoothstep(0.47, 0.76, ecology);
                half3 baseColor = lerp(earth, forest, forestMask * 0.72);

                // Painted grass is only a sparse accent; 3D vegetation supplies most greenery.
                float grassRegion = valueNoise(w * 0.0031 + 77.0);
                float grassFine = valueNoise(w * 0.017 + 121.0);
                float grassMask = smoothstep(0.70, 0.88, grassRegion) * smoothstep(0.48, 0.72, grassFine);
                baseColor = lerp(baseColor, grass, grassMask * 0.18);

                // Broad damp/dry tonal changes make kilometres of ground feel non-uniform.
                float broad = valueNoise(w * 0.0018 + 203.0);
                baseColor *= lerp(0.76, 1.02, broad);
                baseColor *= lerp(0.91, 1.03, micro);

                // Expose stone naturally as terrain gets steeper.
                float slope = 1.0 - saturate(n.y);
                float rockNoise = (valueNoise(w * 0.018 + 55.0) - 0.5) * 0.12;
                float rockMask = smoothstep(_RockSlopeStart + rockNoise, _RockSlopeEnd + rockNoise, slope);
                baseColor = lerp(baseColor, rock * 0.72, rockMask * 0.90);

                float lum = dot(baseColor, half3(0.299,0.587,0.114));
                baseColor = lerp(baseColor, lum.xxx, 0.08) * _Brightness;

                Light mainLight = GetMainLight(i.shadowCoord);
                float ndl = saturate(dot(n, mainLight.direction));
                half3 ambient = SampleSH(n);
                half3 lighting = ambient + mainLight.color * ndl * mainLight.shadowAttenuation;
                lighting = max(lighting, 0.22);
                return half4(baseColor * lighting, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
