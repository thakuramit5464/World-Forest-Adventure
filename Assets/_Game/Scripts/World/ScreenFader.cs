using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Greenhaven.World
{
    /// <summary>
    /// Fades the screen out, loads a scene, and fades back in. Created on demand and kept across the
    /// load; one instance at a time.
    /// </summary>
    public sealed class ScreenFader : MonoBehaviour
    {
        const float FadeSeconds = 0.4f;

        static ScreenFader instance;
        CanvasGroup group;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;

        public static bool Busy => instance != null;

        public static void LoadScene(string sceneName)
        {
            if (instance != null) return;
            var go = new GameObject("ScreenFader");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<ScreenFader>();
            instance.Build();
            instance.StartCoroutine(instance.Run(sceneName));
        }

        void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            gameObject.AddComponent<GraphicRaycaster>();
            group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = true; // no touch input while the scene changes
            var image = new GameObject("Black").AddComponent<Image>();
            image.transform.SetParent(transform, false);
            image.color = new Color(0.04f, 0.06f, 0.08f);
            RectTransform rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        IEnumerator Run(string sceneName)
        {
            yield return Fade(0f, 1f);
            AsyncOperation load = SceneManager.LoadSceneAsync(sceneName);
            while (load != null && !load.isDone) yield return null;
            yield return null; // let the new scene's Start run behind the black screen
            yield return Fade(1f, 0f);
            instance = null;
            Destroy(gameObject);
        }

        IEnumerator Fade(float from, float to)
        {
            for (float t = 0f; t < FadeSeconds; t += Time.unscaledDeltaTime)
            {
                group.alpha = Mathf.Lerp(from, to, t / FadeSeconds);
                yield return null;
            }
            group.alpha = to;
        }
    }
}
