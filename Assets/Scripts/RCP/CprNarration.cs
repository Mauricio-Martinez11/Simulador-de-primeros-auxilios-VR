using System;
using UnityEngine;

namespace FirstAidVR.Rcp
{
    [Serializable] public sealed class CprDialogue { public string key, title, hint, text; }
    [Serializable] public sealed class CprDialogueScript { public CprDialogue[] entries; }
    public static class CprNarration
    {
        private static CprDialogueScript script;
        public static CprDialogue Get(string key)
        {
            if(script==null) script=JsonUtility.FromJson<CprDialogueScript>(Resources.Load<TextAsset>("RCP/Narration/Script").text);
            foreach(var entry in script.entries) if(entry.key==key) return entry;
            throw new ArgumentException("No existe el diálogo RCP: "+key);
        }
        public static AudioClip Clip(string key) => Resources.Load<AudioClip>("RCP/Narration/"+key);
        public static string StepKey(CprPracticeSession session) => session.Closed && !session.Completed ? "Interrupted" : "Step"+((int)session.Step).ToString("00");
    }

}
