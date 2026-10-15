Shader "Driftline/ThemeGradient"
{
    Properties
    {
        _Low ("Lower atmosphere", Color) = (.035,.045,.06,1)
        _High ("Upper atmosphere", Color) = (.1,.15,.2,1)
        _Haze ("Distant haze", Color) = (.15,.18,.2,1)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; };
            struct v2f { float4 pos:SV_POSITION; float3 ray:TEXCOORD0; };
            float4 _Low,_High,_Haze;
            v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.ray=v.vertex.xyz;return o;}
            half4 frag(v2f i):SV_Target
            {
                float3 ray=normalize(i.ray);
                float t=smoothstep(-1,.65,ray.y);
                float haze=pow(saturate(1-abs(ray.y)),4)*.35;
                return half4(lerp(lerp(_Low.rgb,_High.rgb,t),_Haze.rgb,haze),1);
            }
            ENDHLSL
        }
    }
}
