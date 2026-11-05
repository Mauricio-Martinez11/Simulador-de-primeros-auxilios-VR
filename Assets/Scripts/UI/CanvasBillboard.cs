using System.Collections;
using UnityEngine;
using FirstAidVR.Rcp;

// Kept under the serialized component name. The menu is now a fixed world-space panel.
public class CanvasBillboard : MonoBehaviour
{
    void OnEnable() { CprWorldRecenter.Recentered+=PlaceInFront; PlaceInFront(); }
    void OnDisable() { CprWorldRecenter.Recentered-=PlaceInFront; }
    IEnumerator Start()
    {
        while(CprWorldRecenter.ViewCamera()==null) yield return null;
        yield return null;
        PlaceInFront();
    }
    public void PlaceInFront()
    {
        var camera=CprWorldRecenter.ViewCamera(); var canvas=GetComponentInChildren<Canvas>(true);
        if(camera!=null && canvas!=null)
            CprWorldRecenter.Align(transform,canvas.transform,camera.transform,3,false);
    }
}
