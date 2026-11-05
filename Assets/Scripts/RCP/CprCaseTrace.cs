using UnityEngine;
using UnityEngine.UI;

namespace FirstAidVR.Rcp
{
    // A two-beat illustration chosen for the case narrative, not a diagnostic ECG.
    public static class CprCaseMonitor
    {
        public const float FirstBeat = .35f, SecondBeat = 1.25f, FlatAt = 2.25f;
        public static int BeatCount(float seconds) => seconds < FirstBeat ? 0 : seconds < SecondBeat ? 1 : 2;
        public static float SignalAt(float seconds)
        {
            return BeatShape(seconds - FirstBeat) + BeatShape(seconds - SecondBeat);
        }
        private static float BeatShape(float t)
        {
            if (t < -.10f || t > .34f) return 0;
            // Deliberately schematic, without voltage, frequency or diagnostic axes.
            return .15f * Triangle(t + .065f, .035f) - .2f * Triangle(t + .016f, .018f)
                + Triangle(t - .018f, .024f) - .38f * Triangle(t - .055f, .028f)
                + .22f * Triangle(t - .22f, .10f);
        }
        private static float Triangle(float x, float width) => Mathf.Max(0, 1 - Mathf.Abs(x) / width);
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CprCaseTrace : MaskableGraphic
    {
        private float seconds;
        private bool started;
        public void SetTime(float value, bool observing)
        {
            if (Mathf.Approximately(value, seconds) && started == observing) return;
            seconds = value; started = observing; SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = rectTransform.rect;
            var grid = new Color(.18f, .35f, .39f, .35f);
            for (int i = 1; i < 12; i++)
                Line(vh, new Vector2(Mathf.Lerp(r.xMin,r.xMax,i/12f),r.yMin), new Vector2(Mathf.Lerp(r.xMin,r.xMax,i/12f),r.yMax), .6f, grid);
            for (int i = 1; i < 4; i++)
                Line(vh, new Vector2(r.xMin,Mathf.Lerp(r.yMin,r.yMax,i/4f)), new Vector2(r.xMax,Mathf.Lerp(r.yMin,r.yMax,i/4f)), .6f, grid);
            Vector2 previous = default;
            for (int i = 0; i <= 180; i++)
            {
                float sample = Mathf.Lerp(-.15f,2.25f,i/180f);
                if (started && sample > seconds) break;
                float y = started && seconds < CprCaseMonitor.FlatAt ? CprCaseMonitor.SignalAt(sample) : 0;
                var point = new Vector2(Mathf.Lerp(r.xMin,r.xMax,i/180f), r.center.y + y*r.height*.38f);
                if (i > 0) Line(vh,previous,point,2.8f,started?color:new Color(.45f,.60f,.65f));
                previous = point;
            }
        }
        private static void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color color)
        {
            var n = new Vector2(-(b-a).y,(b-a).x).normalized*width*.5f;
            int start=vh.currentVertCount;
            vh.AddVert(a-n,color,Vector2.zero);vh.AddVert(a+n,color,Vector2.zero);
            vh.AddVert(b+n,color,Vector2.zero);vh.AddVert(b-n,color,Vector2.zero);
            vh.AddTriangle(start,start+1,start+2);vh.AddTriangle(start,start+2,start+3);
        }
    }
}
