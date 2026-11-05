using System.Collections;
using UnityEngine;

namespace FirstAidVR.Rcp
{
    public static class CprFamiliarization
    {
        public static bool Completed { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { Completed=false; }
        public static IEnumerator Run(CprIntroPanel panel,Camera camera,CprTrackedHands input,CprTrainingSettings settings)
        {
            int step=0;
            while(step<4 && !panel.SkipRequested)
            {
                bool controllers=input.Read(true,settings).controller || input.Read(false,settings).controller;
                string key=step==0?"Look":step==1?"Hands":step==2?(controllers?"SelectControllers":"SelectHands"):"ControlsDone";
                panel.Present(key,step==2?new[]{"Probar selección"}:new string[0]);
                panel.SetNavigationStep(step);
                var initial=Vector3.ProjectOnPlane(camera.transform.forward,Vector3.up);
                bool leftSeen=false,rightSeen=false,done=false; float held=0;
                while(!panel.SkipRequested && panel.Navigation==0 && (!panel.FinishedSpeaking || !done))
                {
                    if(panel.Focus)
                    {
                        if(step==0)
                        {
                            float angle=Vector3.SignedAngle(initial,Vector3.ProjectOnPlane(camera.transform.forward,Vector3.up),Vector3.up);
                            leftSeen|=angle< -12; rightSeen|=angle>12;
                            panel.Hint((leftSeen?"✓":"←")+" Mira a la izquierda     "+(rightSeen?"✓":"→")+" Mira a la derecha");
                            done=leftSeen && rightSeen;
                        }
                        else if(step==1)
                        {
                            bool visible=input.Read(true,settings).valid || input.Read(false,settings).valid;
                            held=visible?held+Mathf.Min(Time.unscaledDeltaTime,.1f):0; done=held>=.6f;
                        }
                        else done=step==3 || panel.Selection>=0;
                    }
                    yield return null;
                }
                step=Mathf.Clamp(step+(panel.Navigation<0?-1:1),0,4);
            }
            Completed=true;
        }
    }
}
