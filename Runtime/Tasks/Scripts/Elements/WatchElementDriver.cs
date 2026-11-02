using UnityEngine;
using UnityEngine.Video;

using Virtuademy.Environments.ScriptingApi.Placeholders;
using Virtuademy.Environments.ScriptingApi.Tasks;
using Virtuademy.SDK.Environments.UIKit;

namespace Virtuademy.SDK.Environments.Tasks
{
    /// <summary>Watch element: a video reaching its end, a POI closed or a panel hidden completes the task, Right.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public class WatchElementDriver : ElementDriver
    {
        private VideoPlayer video;
        private POIPlaceholder poi;
        private VirtuademyPanel panel;

        protected override void OnBind()
        {
            GameObject set = ((WatchElementPlaceholder)Element).Watched;
            GameObject watched = set != null ? set : gameObject;
            if (watched.TryGetComponent(out video))
            {
                video.loopPointReached += OnVideoEnd;
            }
            else if (watched.TryGetComponent(out poi))
            {
                poi.OnPOIClosed?.AddListener(Done);
            }
            else if (watched.TryGetComponent(out panel))
            {
                panel.OnHidden.AddListener(Done);
            }
            else
            {
                Debug.LogWarning("[WatchElement] " + name + ": " + watched.name + " has no Video Player, POI placeholder or " +
                    "Virtuademy Panel to watch.", this);
            }
        }

        private void OnDestroy()
        {
            if (video != null)
            {
                video.loopPointReached -= OnVideoEnd;
            }
            if (poi != null)
            {
                poi.OnPOIClosed?.RemoveListener(Done);
            }
            if (panel != null)
            {
                panel.OnHidden.RemoveListener(Done);
            }
        }

        private void OnVideoEnd(VideoPlayer source) => Done();

        private void Done() => Report(TaskResult.Right(gameObject), true);
    }
}
