Shader "Hydra/PostProcessing/Noise"
{
    Properties
    {
        _NoiseScale ("Noise Scale", Float) = 1
        _NoiseAmount ("Noise Amount", Float) = 1
        _NoiseSpeed ("Noise Speed", Float) = 1
        _Alpha ("Alpha", Float) = 1
        _VignetteAmount ("Vignette Amount", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 screenPos : TEXCOORD1;
            };

            float grayscale(float3 col, float range)
            {
                float gray = (col.x + col.y + col.z)/3;
                gray *= range;
                gray = min(max(gray, 0), 1);
                return gray;
            }

            float vignette(float2 uv, float intensity)
            {
                float2 adjusted = (uv-0.5)*2;
                float vignette = distance(adjusted, float2(0,0));
                vignette = pow(1-intensity, vignette);
                return vignette;
            }

            float _NoiseAmount;
            float _NoiseScale;
            float _NoiseSpeed;

            float _VignetteAmount;
            float _Alpha;

            float2 unity_gradientNoise_dir(float2 p)
            {
                p = p % 289;
                float x = (34 * p.x + 1) * p.x % 289 + p.y;
                x = (34 * x + 1) * x % 289;
                x = frac(x / 41) * 2 - 1;
                return normalize(float2(x - floor(x + 0.5), abs(x) - 0.5));
            }

            float unity_gradientNoise(float2 p)
            {
                float2 ip = floor(p);
                float2 fp = frac(p);
                float d00 = dot(unity_gradientNoise_dir(ip), fp);
                float d01 = dot(unity_gradientNoise_dir(ip + float2(0, 1)), fp - float2(0, 1));
                float d10 = dot(unity_gradientNoise_dir(ip + float2(1, 0)), fp - float2(1, 0));
                float d11 = dot(unity_gradientNoise_dir(ip + float2(1, 1)), fp - float2(1, 1));
                fp = fp * fp * fp * (fp * (fp * 6 - 15) + 10);
                return lerp(lerp(d00, d01, fp.y), lerp(d10, d11, fp.y), fp.x);
            }


            float Unity_GradientNoise_float(float2 UV, float Scale)
            {
                return unity_gradientNoise(UV * Scale) + 0.5;
            }



            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.screenPos = ComputeScreenPos(o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float4 col = float4(1,1,1,0);

                float2 uv = i.screenPos.xy /i.screenPos.w;

                if(_NoiseAmount != 0)
                {
                    float noise = Unity_GradientNoise_float(float2(uv.x, uv.y + (_NoiseSpeed * _Time.x)), _NoiseScale);
                    noise *= _NoiseAmount;
                    col.rgba = noise;
                }

                col.rgb *= vignette(uv, _VignetteAmount);
                col.a = _Alpha;
                return col;
            }
            ENDCG
        }
    }
}
