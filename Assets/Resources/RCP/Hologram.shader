Shader "FirstAidVR/Hologram"
{
    Properties { _Color ("Color", Color) = (0.12,0.82,1,0.38) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos:SV_POSITION; float3 normal:TEXCOORD0; float3 world:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            fixed4 _Color;
            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos=UnityObjectToClipPos(v.vertex);
                o.world=mul(unity_ObjectToWorld,v.vertex).xyz;
                o.normal=UnityObjectToWorldNormal(v.normal);
                return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float rim=pow(1-saturate(abs(dot(normalize(i.normal),normalize(_WorldSpaceCameraPos-i.world)))),2);
                float scan=.9+.1*sin(i.world.y*420-_Time.y*2);
                return fixed4(_Color.rgb*(.78+rim*.5)*scan,saturate(_Color.a+rim*.22));
            }
            ENDCG
        }
    }
}
