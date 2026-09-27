// Simpelt græs-shader til URP med GPU instancing (bruges af GrassInstanceRenderer via
// Graphics.RenderMeshInstanced – men virker også på almindelige MeshRenderers).
//
//  - Farvegradient fra rod til spids + valgfri tekstur med alpha cutout
//  - Vind: sving i world space, stærkest i spidsen (højde = UV.y, 0 = rod, 1 = spids)
//  - Main light med skygger, ambient (SH), SSAO og fog
//  - Dobbeltsidet (Cull Off), bagsiden får vendt normalen
//  - Passes: UniversalForward, ShadowCaster, DepthOnly, DepthNormals
//
// Begrænsninger (bevidst holdt simpelt): ingen additional lights, ingen lightmaps,
// ingen DOTS instancing (GPU Resident Drawer bruges ikke til græsset – det tegnes af
// GrassInstanceRenderer).
Shader "Blender Instances/Instanced Grass (URP)"
{
    Properties
    {
        [MainTexture] _BaseMap ("Texture (A = cutout)", 2D) = "white" {}
        [MainColor] _BaseColor ("Tip Color", Color) = (0.55, 0.8, 0.3, 1)
        _RootColor ("Root Color", Color) = (0.15, 0.32, 0.08, 1)
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5

        [Header(Wind)]
        _WindStrength ("Strength", Range(0, 1)) = 0.15
        _WindSpeed ("Speed", Float) = 1.5
        _WindFrequency ("World Frequency", Float) = 0.2
        _WindDirection ("Direction (XZ)", Vector) = (1, 0, 0.3, 0)

        [Header(Lighting)]
        _Translucency ("Translucency (backlight)", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "IgnoreProjector" = "True"
        }
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        // Alle properties i UnityPerMaterial => SRP Batcher-kompatibel.
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _RootColor;
            half _Cutoff;
            half _WindStrength;
            float _WindSpeed;
            float _WindFrequency;
            float4 _WindDirection;
            half _Translucency;
        CBUFFER_END

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS   : NORMAL;
            float2 uv         : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        // World-position inkl. vind. Skal kaldes EFTER UNITY_SETUP_INSTANCE_ID,
        // så UNITY_MATRIX_M er den aktuelle instance's matrix.
        float3 GrassWorldPosition(float3 positionOS, float heightFactor)
        {
            float3 positionWS = TransformObjectToWorld(positionOS);
            float3 rootWS = UNITY_MATRIX_M._m03_m13_m23; // instance'ens origin => samme fase for hele strået
            float2 dir = normalize(_WindDirection.xz + 1e-5);
            float phase = _Time.y * _WindSpeed + dot(rootWS.xz, dir) * _WindFrequency;
            float sway = (sin(phase) * 0.7 + sin(phase * 2.3 + 1.7) * 0.3) * _WindStrength;
            positionWS.xz += dir * sway * heightFactor * heightFactor;
            return positionWS;
        }

        void ClipAlpha(float2 uv)
        {
            half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).a * _BaseColor.a;
            clip(alpha - _Cutoff);
        }
        ENDHLSL

        // ---------------------------------------------------------------------------------
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float2 uv         : TEXCOORD2;
                float  height     : TEXCOORD3;
                float  fogFactor  : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float height = saturate(input.uv.y);
                float3 positionWS = GrassWorldPosition(input.positionOS.xyz, height);

                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.height = height;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input, FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                clip(tex.a * _BaseColor.a - _Cutoff);

                half3 albedo = tex.rgb * lerp(_RootColor.rgb, _BaseColor.rgb, input.height);
                float3 normalWS = normalize(IS_FRONT_VFACE(facing, true, false) ? input.normalWS : -input.normalWS);

                // Main light + skygger
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    float4 shadowCoord = float4(GetNormalizedScreenSpaceUV(input.positionCS), 0, 1);
                #else
                    float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #endif
                Light mainLight = GetMainLight(shadowCoord);
                half atten = mainLight.distanceAttenuation * mainLight.shadowAttenuation;

                half NdotL = dot(normalWS, mainLight.direction);
                half diffuse = saturate(NdotL * 0.5 + 0.5);                     // half-lambert: blødere græs
                half backlight = saturate(-NdotL) * _Translucency;              // lys igennem strået
                half3 direct = mainLight.color * atten * (diffuse + backlight);

                half3 ambient = SampleSH(normalWS);

                #if defined(_SCREEN_SPACE_OCCLUSION)
                    AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(input.positionCS));
                    ambient *= ao.indirectAmbientOcclusion;
                    direct *= ao.directAmbientOcclusion;
                #endif

                half3 color = albedo * (direct + ambient);
                color = MixFog(color, input.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        // ---------------------------------------------------------------------------------
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            // Sættes af URP's shadow pass (samme navne som i URP/Lit's ShadowCasterPass.hlsl).
            float3 _LightDirection;
            float3 _LightPosition;

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);

                float3 positionWS = GrassWorldPosition(input.positionOS.xyz, saturate(input.uv.y));
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                output.positionCS = positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                ClipAlpha(input.uv);
                return 0;
            }
            ENDHLSL
        }

        // ---------------------------------------------------------------------------------
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 positionWS = GrassWorldPosition(input.positionOS.xyz, saturate(input.uv.y));
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half frag(Varyings input) : SV_Target
            {
                ClipAlpha(input.uv);
                return input.positionCS.z;
            }
            ENDHLSL
        }

        // ---------------------------------------------------------------------------------
        // Bruges af SSAO og andre effekter, der skal kende normalerne.
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float2 uv         : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 positionWS = GrassWorldPosition(input.positionOS.xyz, saturate(input.uv.y));
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 frag(Varyings input, FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC) : SV_Target
            {
                ClipAlpha(input.uv);
                float3 n = normalize(IS_FRONT_VFACE(facing, true, false) ? input.normalWS : -input.normalWS);
                return half4(n, 0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
