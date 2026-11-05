Shader "FirstAidVR/PatientHologram"
{
    Properties { _Color ("Color",Color) = (.2,.8,.95,.12) }
    SubShader
    {
        Tags { "Queue"="Geometry+1" "RenderType"="Transparent" }
        Cull Back
        Pass
        {
            ZWrite On ColorMask 0
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos:SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert(appdata v) { v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.pos=UnityObjectToClipPos(v.vertex); return o; }
            fixed4 frag(v2f i):SV_Target { UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i); return 0; }
            ENDCG
        }
        Pass
        {
            ZWrite Off ZTest Equal
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos:SV_POSITION; float3 normal:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            fixed4 _Color;
            v2f vert(appdata v) { v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.pos=UnityObjectToClipPos(v.vertex); o.normal=UnityObjectToWorldNormal(v.normal); return o; }
            fixed4 frag(v2f i):SV_Target { UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i); float light=.65+.35*saturate(dot(normalize(i.normal),normalize(float3(.3,1,.4)))); return fixed4(_Color.rgb*light,_Color.a); }
            ENDCG
        }
    }
}
