// "Go here" arrow of the interaction cue. Unlit and opaque; it bobs 0.1 m up and down at 0.6 Hz and its colour
// blinks, both computed here from _Time (the phase wrapped in float, so the motion stays smooth on mobile GPUs), so the
// arrow costs no script time. The arrow mesh's bounds include the bob, so it is not culled at the top of the motion.
// SRP Batcher compatible and single-pass instanced stereo.
Shader "Virtuademy/InteractionCue/Arrow"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (0, 2.65, 4, 1)
        _Intensity ("Intensity", Float) = 1
        _BobHeight ("Bob height (m)", Float) = 0.1
        _BobSpeed ("Bob and blink speed (Hz)", Float) = 0.6
        _PulseMin ("Blink minimum", Range(0, 1)) = 0.6
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "InteractionCueArrow"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                float _BobHeight;
                float _BobSpeed;
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
                half shade : TEXCOORD0;
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
                positionWS.y += _BobHeight * sin(TWO_PI * frac(_BobSpeed * _Time.y));
                output.positionCS = TransformWorldToHClip(positionWS);
                // A fixed light from above keeps the arrow's shape readable without lighting.
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.shade = 0.65h + 0.35h * saturate(dot(normalWS, float3(0.3, 0.9, 0.3)));
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float phase = frac(_BobSpeed * _Time.y);
                half wave = 0.5h + 0.5h * sin(TWO_PI * phase);
                half blink = lerp(_PulseMin, 1.0h, wave);
                return half4(_Color.rgb * (_Intensity * blink * input.shade), 1);
            }
            ENDHLSL
        }
        // Opaque objects need a depth pass: with depth priming URP draws the forward pass with ZTest Equal.
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex vertDepth
            #pragma fragment fragDepth
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                float _BobHeight;
                float _BobSpeed;
                half _PulseMin;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vertDepth(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                positionWS.y += _BobHeight * sin(TWO_PI * frac(_BobSpeed * _Time.y));
                output.positionCS = TransformWorldToHClip(positionWS);
                return output;
            }

            half fragDepth(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
