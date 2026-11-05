using UnityEngine;
using UnityEngine.UI;

namespace FirstAidVR.Rcp
{
    /// <summary>Resolution-independent rounded glass cards; uses the standard mobile UI material.</summary>
    [AddComponentMenu("")]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CprRoundGraphic : MaskableGraphic
    {
        public float radius=18;
        public float bottomShade=.78f;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r=rectTransform.rect;
            float round=Mathf.Clamp(radius,0,Mathf.Min(r.width,r.height)*.5f);
            var center=r.center; const int n=8;
            vh.AddVert(center,color,new Vector2(.5f,.5f));
            for(int corner=0;corner<4;corner++)
            {
                var c=new Vector2(corner==0 || corner==3?r.xMax-round:r.xMin+round,corner<2?r.yMax-round:r.yMin+round);
                for(int i=0;i<=n;i++)
                {
                    float a=(corner*90+i*90f/n)*Mathf.Deg2Rad;
                    var p=c+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*round;
                    var tint=color* Mathf.Lerp(bottomShade,1,Mathf.InverseLerp(r.yMin,r.yMax,p.y)); tint.a=color.a;
                    vh.AddVert(p,tint,new Vector2(Mathf.InverseLerp(r.xMin,r.xMax,p.x),Mathf.InverseLerp(r.yMin,r.yMax,p.y)));
                }
            }
            int total=4*(n+1);
            for(int i=1;i<=total;i++) vh.AddTriangle(0,i,i==total?1:i+1);
        }
    }
}
