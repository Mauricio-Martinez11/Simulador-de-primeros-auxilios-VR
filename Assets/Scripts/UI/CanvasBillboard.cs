using System.Collections;
using UnityEngine;
using FirstAidVR.Rcp;

// Se conserva el nombre serializado del componente. El menú ahora es un panel fijo en el espacio del mundo.
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
