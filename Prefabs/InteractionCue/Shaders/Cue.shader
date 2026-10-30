// Rim of the interaction cue (InteractionCuePlaceholder), drawn on shell renderers that copy the target's mesh.
// The look of Showcase's Highlight.mat: a cyan Fresnel rim, transparent and additive. The pulse is computed here
// from _Time, so a pulsing cue costs no script time. URP unlit, no texture, no keyword; SRP Batcher compatible
// (one UnityPerMaterial buffer) and single-pass instanced stereo (instance id and stereo output in the structs).
Shader "Virtuademy/InteractionCue/Rim"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (0, 2.65, 4, 1)
        _Intensity ("Intensity", Float) = 1
        _FresnelPower ("Fresnel power", Float) = 2.35
        _Alpha ("Alpha", Range(0, 1)) = 0.5
        _PulseSpeed ("Pulse speed (Hz, 0 = fixed)", Float) = 0
        _PulseMin ("Pulse minimum", Range(0, 1)) = 0.35
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "InteractionCueRim"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                half _FresnelPower;
                half _Alpha;
                half _PulseSpeed;
                half _PulseMin;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = GetWorldSpaceViewDir(positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half facing = saturate(dot(normalize(input.normalWS), normalize(input.viewDirWS)));
                half rim = pow(1.0h - facing, _FresnelPower);
                half wave = 0.5h + 0.5h * sin(TWO_PI * _PulseSpeed * _Time.y);
                half pulse = _PulseSpeed > 0 ? lerp(_PulseMin, 1.0h, wave) : 1.0h;
                return half4(_Color.rgb * (_Intensity * rim * pulse * _Alpha), 0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
