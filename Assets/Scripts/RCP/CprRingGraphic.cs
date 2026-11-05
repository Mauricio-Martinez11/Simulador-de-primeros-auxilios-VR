using UnityEngine;
using UnityEngine.UI;
namespace FirstAidVR.Rcp
{
    [AddComponentMenu("")]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CprRingGraphic : MaskableGraphic
    {
        public float amount=1, thickness=8;
        public void SetAmount(float value) { if(Mathf.Abs(amount-value)<.001f) return; amount=Mathf.Clamp01(value); SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r=rectTransform.rect; float outer=Mathf.Min(r.width,r.height)*.5f, inner=Mathf.Max(0,outer-thickness);
            for(int i=0;i<=80;i++)
            {
                float a=(90-360*amount*i/80)*Mathf.Deg2Rad; var v=new Vector2(Mathf.Cos(a),Mathf.Sin(a));
                vh.AddVert(r.center+v*outer,color,Vector2.zero); vh.AddVert(r.center+v*inner,color,Vector2.zero);
                if(i>0) { int p=i*2; vh.AddTriangle(p-2,p,p-1); vh.AddTriangle(p-1,p,p+1); }
            }
        }
    }
}
