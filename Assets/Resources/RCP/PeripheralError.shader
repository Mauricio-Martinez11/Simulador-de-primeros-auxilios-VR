Shader "FirstAidVR/PeripheralError"
{
 Properties { _Color ("Tint", Color) = (1,0.04,0.02,0) }
 SubShader
 {
  Tags { "Queue"="Overlay+100" "RenderType"="Transparent" "IgnoreProjector"="True" }
  Cull Off ZWrite Off ZTest Always Blend SrcAlpha OneMinusSrcAlpha
  Pass
  {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_instancing
   #include "UnityCG.cginc"
   struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
   struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
   fixed4 _Color;
   v2f vert(appdata v) { v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.vertex=float4(v.vertex.xy*2,UNITY_NEAR_CLIP_VALUE,1); o.uv=v.uv; return o; }
   fixed4 frag(v2f i):SV_Target { float edge=max(abs(i.uv.x-.5),abs(i.uv.y-.5))*2; fixed4 c=_Color; c.a*=smoothstep(.68,.98,edge); return c; }
   ENDCG
  }
 }
}
