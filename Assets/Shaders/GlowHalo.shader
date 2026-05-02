Shader "Custom/GlowHalo"
{
    Properties
    {
        _GlowColor ("Glow Color", Color) = (1.0, 0.2, 0.1, 1.0)
        _GlowIntensity ("Glow Intensity", Range(0.5, 3.0)) = 1.5
        _GlowPower ("Glow Power", Range(1.0, 8.0)) = 3.0
    }
    SubShader
    {
        Tags { "Queue"="Transparent+1" "RenderType"="Transparent" }
        LOD 100

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Front

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 viewDir : TEXCOORD0;
                float3 normal : TEXCOORD1;
            };

            float4 _GlowColor;
            float _GlowIntensity;
            float _GlowPower;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                // Invert normal for backface rendering (inner glow effect)
                o.normal = -UnityObjectToWorldNormal(v.normal);
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.viewDir = normalize(_WorldSpaceCameraPos - worldPos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Fresnel effect: stronger at edges
                float fresnel = 1.0 - saturate(dot(i.viewDir, i.normal));
                fresnel = pow(fresnel, _GlowPower);

                float4 col = _GlowColor;
                col.a = fresnel * _GlowIntensity;
                col.rgb *= _GlowIntensity;
                return col;
            }
            ENDCG
        }
    }
    FallBack "Transparent/Diffuse"
}
