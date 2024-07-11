Shader "Hidden/Dilation"
{
    Properties
    {
        _Spread("Spread", Integer) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline"
        }
        LOD 100
        ZWrite Off Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        SAMPLER(sampler_BlitTexture);

        float _Spread;
        ENDHLSL

        Pass
        {
            Name "HorizontalDilation"
            ZTest Always

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag_vertical

            float4 frag_vertical(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Dilate
                float totalWeightedValues = 0;
                int shortestActivePixelDistance = _Spread + 1;
                float3 nearestActivePixelColor = float3(0, 0, 0);
                for (int x = -_Spread; x <= _Spread; x++)
                {
                    float2 uv = i.texcoord + float2(_BlitTexture_TexelSize.x * x, 0.0f);
                    float4 buffer = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, uv);

                    // Check if this is the nearest occupied pixel
                    // (Occupied means non-black here)
                    int distance = abs(x);
                    float falloff = 1.0f - distance / _Spread;
                    totalWeightedValues += buffer.a * falloff;

                    if (distance < shortestActivePixelDistance &&
                        buffer.a >= 1.0)
                    {
                        shortestActivePixelDistance = distance;
                        nearestActivePixelColor = buffer.xyz;
                    }
                }

                return float4(nearestActivePixelColor, 1 - saturate(shortestActivePixelDistance / _Spread));
            }
            ENDHLSL
        }

        Pass
        {
            Name "VerticalDilation"
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha

            Stencil
            {
                Ref 15
                Comp NotEqual
                Pass Zero
                Fail Zero
                ZFail Zero
            }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag_horizontal

            float4 frag_horizontal(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Dilate
                float totalWeightedValues = 0;
                float3 brightestActivePixelColor = float3(0, 0, 0);
                float brightestWeightedAlpha = 0;
                for (int y = -_Spread; y <= _Spread; y++)
                {
                    float2 uv = i.texcoord + float2(0.0f, _BlitTexture_TexelSize.y * y);
                    float4 buffer = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, uv);

                    // Check if this is the nearest occupied pixel
                    // (Occupied means non-black here)
                    int distance = abs(y);
                    float falloff = 1.0f - distance / _Spread;
                    float weightedValue = buffer.a * falloff;
                    totalWeightedValues += weightedValue;

                    // favor the brightest, nearest alpha
                    if (weightedValue > brightestWeightedAlpha)
                    {
                        brightestWeightedAlpha = weightedValue;
                        brightestActivePixelColor = buffer.xyz;
                    }
                }

                return float4(brightestActivePixelColor, Smoothstep01(brightestWeightedAlpha));
            }
            ENDHLSL
        }
    }
}