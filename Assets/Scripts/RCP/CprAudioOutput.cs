using UnityEngine;
namespace FirstAidVR.Rcp
{
    public static class CprAudioOutput
    {
        // The current view owns the listener, including direct entry into the menu/tutorial.
        public static void Bind(Camera camera)
        {
            if(camera==null) return;
            var selected=camera.GetComponent<AudioListener>();
            if(selected==null) selected=camera.gameObject.AddComponent<AudioListener>();
            foreach(var listener in Object.FindObjectsOfType<AudioListener>())
                if(listener!=selected) listener.enabled=false;
            selected.enabled=true;
        }
        public static bool Ready()
        {
            foreach(var listener in Object.FindObjectsOfType<AudioListener>())
                if(listener.isActiveAndEnabled) return AudioSettings.dspTime>0;
            return false;
        }
    }
}
