Shader "FirstAidVR/PhoneHologram"
{
    Properties { _Color ("Color", Color) = (0.12,0.82,1,0.5) }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
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
            v2f vert(appdata v) {
                v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos=UnityObjectToClipPos(v.vertex); o.normal=UnityObjectToWorldNormal(v.normal);
                o.world=mul(unity_ObjectToWorld,v.vertex).xyz; return o;
            }
            fixed4 frag(v2f i):SV_Target {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float rim=pow(1-saturate(abs(dot(normalize(i.normal),normalize(_WorldSpaceCameraPos-i.world)))),2);
                float scan=.94+.06*sin(i.world.y*420-_Time.y*2);
                return fixed4(_Color.rgb*(.8+rim*.45)*scan,saturate(_Color.a+rim*.2));
            }
            ENDCG
        }
    }
}
