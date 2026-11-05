using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FirstAidVR.Rcp
{
    // Keep the panel still during a drag; apply the height on release so it cannot chase the ray.
    public sealed class CprHeightControl : MonoBehaviour, IBeginDragHandler, IEndDragHandler, ICancelHandler
    {
        public bool Dragging { get; private set; }
        public event Action Released;
        public void OnBeginDrag(PointerEventData data) { Dragging=true; }
        public void OnEndDrag(PointerEventData data) { Dragging=false; Released?.Invoke(); }
        public void OnCancel(BaseEventData data) { Cancel(); }
        public void Cancel() { Dragging=false; }
        private void OnDisable() { Cancel(); }
    }
}
