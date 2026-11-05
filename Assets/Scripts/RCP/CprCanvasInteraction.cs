using Oculus.Interaction;
using Oculus.Interaction.Surfaces;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FirstAidVR.Rcp
{
    public static class CprCanvasInteraction
    {
        public static void Attach(Canvas canvas)
        {
            if (Object.FindObjectOfType<PointableCanvasModule>() == null)
            {
                var events = EventSystem.current;
                if (events == null) events = new GameObject("EventSystem RCP", typeof(EventSystem)).GetComponent<EventSystem>();
                events.gameObject.AddComponent<PointableCanvasModule>().ExclusiveMode = true;
            }
            if (canvas.GetComponent<GraphicRaycaster>() == null) canvas.gameObject.AddComponent<GraphicRaycaster>();
            if (canvas.GetComponentInChildren<RayInteractable>(true) != null) return;
            var go = new GameObject("ISDK_RayCanvasInteraction", typeof(RectTransform)); go.SetActive(false);
            var rect = (RectTransform)go.transform; rect.SetParent(canvas.transform, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            var pointable = go.AddComponent<PointableCanvas>(); pointable.InjectAllPointableCanvas(canvas);
            var plane = go.AddComponent<PlaneSurface>();
            var clipper = go.AddComponent<BoundsClipper>();
            var bounds = ((RectTransform)canvas.transform).rect;
            clipper.Position = bounds.center; clipper.Size = new Vector3(bounds.width, bounds.height, .01f);
            var surface = go.AddComponent<ClippedPlaneSurface>(); surface.InjectAllClippedPlaneSurface(plane, new IBoundsClipper[] { clipper });
            var ray = go.AddComponent<RayInteractable>(); ray.InjectAllRayInteractable(surface);
            ray.InjectOptionalSelectSurface(plane); ray.InjectOptionalPointableElement(pointable);
            go.SetActive(true);
        }
    }
}
