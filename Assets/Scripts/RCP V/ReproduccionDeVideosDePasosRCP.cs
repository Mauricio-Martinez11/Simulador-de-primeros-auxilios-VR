using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace FirstAidVR.Rcp.Consulta
{
    public sealed class ReproduccionDeVideosDePasosRCP : MonoBehaviour
    {
        [SerializeField] private RawImage screen;
        private VideoPlayer player;
        private RenderTexture target;
        private ConfiguracionDeCriteriosRCP settings;
        private int current=-1;
        private bool allowed, failed;
        public VideoPlayer Player => player;
        public void Setup(RawImage image) { screen=image; }
        public void Bind(ConfiguracionDeCriteriosRCP config)
        {
            settings=config;
            if(player!=null) return;
            target=new RenderTexture(1280,720,0,RenderTextureFormat.ARGB32); target.Create();
            player=gameObject.AddComponent<VideoPlayer>(); player.playOnAwake=false; player.isLooping=true;
            player.renderMode=VideoRenderMode.RenderTexture; player.targetTexture=target; player.audioOutputMode=VideoAudioOutputMode.None;
            player.skipOnDrop=true; player.waitForFirstFrame=true;
            player.prepareCompleted+=OnPrepared; player.errorReceived+=OnError;
        }
        public void Show(PasoDePracticaRCP step,bool playing)
        {
            int index=Mathf.Clamp((int)step-1,0,5); allowed=playing;
            // La ilustración original de colocación muestra las manos en el orden contrario.
            // Reflejar tanto el video como la imagen de reserva para mostrar la derecha abajo y la izquierda arriba.
            screen.uvRect=index==4?new Rect(1,0,-1,1):new Rect(0,0,1,1);
            if(current!=index)
            {
                current=index; failed=false;
                if(player!=null) player.Stop();
                screen.texture=settings.practiceVideoPosters!=null && index<settings.practiceVideoPosters.Length?settings.practiceVideoPosters[index]:null;
                if(player!=null && settings.practiceVideos!=null && index<settings.practiceVideos.Length && settings.practiceVideos[index]!=null)
                { player.clip=settings.practiceVideos[index]; player.Prepare(); }
            }
            if(player==null || failed || !player.isPrepared) return;
            if(allowed && !player.isPlaying) player.Play();
            else if(!allowed && player.isPlaying) player.Pause();
            if(player.texture!=null && player.frame>=0) screen.texture=target;
        }
        private void OnPrepared(VideoPlayer source) { if(allowed && isActiveAndEnabled) source.Play(); }
        private void OnError(VideoPlayer source,string error)
        {
            failed=true; source.Stop();
            screen.texture=settings.practiceVideoPosters!=null && current<settings.practiceVideoPosters.Length?settings.practiceVideoPosters[current]:null;
            Debug.LogWarning("RCP: demostración en imagen de respaldo; vídeo no disponible. "+error,this);
        }
        private void OnDisable() { allowed=false; if(player!=null) player.Pause(); }
        private void OnDestroy()
        {
            if(player!=null) { player.prepareCompleted-=OnPrepared; player.errorReceived-=OnError; player.Stop(); }
            if(target!=null) { target.Release(); Destroy(target); }
        }
    }
}
