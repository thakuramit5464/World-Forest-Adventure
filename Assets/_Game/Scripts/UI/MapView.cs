using System.Collections.Generic;
using Greenhaven.Player;
using Greenhaven.Quests;
using Greenhaven.World;
using UnityEngine;
using UnityEngine.UI;

namespace Greenhaven.UI
{
    /// <summary>
    /// The map page: the scene's captured picture with the player's position and heading, discovered
    /// places (name and icon colour by kind), scene gates, and the tracked quest's current objective (a
    /// pulsing ring on every matching target in this scene, or on the gate if the objective lies in the other
    /// scene). Pins are cloned from inactive templates built by the HUD builder and reused.
    /// </summary>
    public sealed class MapView : MonoBehaviour
    {
        [SerializeField] Image mapImage;
        [SerializeField] RectTransform pinLayer;
        [SerializeField] Text titleText, hintText;
        [SerializeField] RectTransform playerPin, locationTemplate, objectiveTemplate, gateTemplate;
        [SerializeField] QuestManager quests;

        readonly List<RectTransform> locationPins = new List<RectTransform>(), objectivePins = new List<RectTransform>(), gatePins = new List<RectTransform>();
        Transform player;

        static readonly Dictionary<LocationKind, Color> KindColours = new Dictionary<LocationKind, Color>
        {
            [LocationKind.Village] = new Color(0.55f, 0.23f, 0.16f), [LocationKind.House] = new Color(0.42f, 0.27f, 0.15f),
            [LocationKind.Bridge] = new Color(0.12f, 0.35f, 0.53f), [LocationKind.Gate] = new Color(0.42f, 0.27f, 0.15f),
            [LocationKind.Clearing] = new Color(0.18f, 0.49f, 0.41f), [LocationKind.Rest] = new Color(0.62f, 0.45f, 0.14f),
            [LocationKind.Viewpoint] = new Color(0.28f, 0.42f, 0.62f), [LocationKind.Summit] = new Color(0.55f, 0.23f, 0.16f),
            [LocationKind.Landmark] = new Color(0.55f, 0.23f, 0.16f),
        };

        public void Open()
        {
            if (player == null)
            {
                PlayerMotor motor = FindFirstObjectByType<PlayerMotor>();
                player = motor != null ? motor.transform : null;
            }
            if (quests == null) quests = FindFirstObjectByType<QuestManager>();
            SceneMap map = SceneMap.Current;
            if (mapImage != null) mapImage.sprite = map != null ? map.Map : null;
            if (titleText != null) titleText.text = map != null ? map.Title : "No map";
            Refresh();
        }

        void Update()
        {
            if (gameObject.activeInHierarchy) Refresh();
        }

        Vector2 Place(RectTransform pin, Vector3 world)
        {
            SceneMap map = SceneMap.Current;
            Vector2 uv = map != null ? map.ToMap(world) : new Vector2(0.5f, 0.5f);
            pin.anchorMin = pin.anchorMax = uv;
            pin.anchoredPosition = Vector2.zero;
            return uv;
        }

        RectTransform Pin(List<RectTransform> pool, int i, RectTransform template)
        {
            while (pool.Count <= i)
            {
                RectTransform clone = Instantiate(template, pinLayer);
                clone.gameObject.SetActive(false);
                pool.Add(clone);
            }
            pool[i].gameObject.SetActive(true);
            return pool[i];
        }

        static void Hide(List<RectTransform> pool, int from)
        {
            for (int i = from; i < pool.Count; i++) pool[i].gameObject.SetActive(false);
        }

        void Refresh()
        {
            if (pinLayer == null) return;
            // Player: position and heading.
            if (playerPin != null)
            {
                playerPin.gameObject.SetActive(player != null);
                if (player != null)
                {
                    Place(playerPin, player.position);
                    playerPin.localRotation = Quaternion.Euler(0f, 0f, -player.eulerAngles.y);
                }
            }

            // Places: discovered (or always shown).
            int n = 0;
            foreach (LocationMarker loc in LocationMarker.Active)
            {
                if (!loc.AlwaysOnMap && !loc.IsDiscovered) continue;
                RectTransform pin = Pin(locationPins, n++, locationTemplate);
                Place(pin, loc.transform.position);
                Image icon = pin.GetComponent<Image>();
                if (icon != null) icon.color = KindColours.TryGetValue(loc.Kind, out Color c) ? c : Color.black;
                Text label = pin.GetComponentInChildren<Text>();
                if (label != null) label.text = loc.DisplayName;
            }
            Hide(locationPins, n);

            // Gates to other scenes.
            int g = 0;
            foreach (SceneGate gate in FindObjectsByType<SceneGate>(FindObjectsSortMode.None))
            {
                RectTransform pin = Pin(gatePins, g++, gateTemplate);
                Place(pin, gate.transform.position);
                Text label = pin.GetComponentInChildren<Text>();
                if (label != null) label.text = gate.Label;
            }
            Hide(gatePins, g);

            // Current objective.
            int o = 0;
            string objective = "";
            QuestDefinition tracked = quests != null ? quests.TrackedQuest : null;
            if (tracked != null)
            {
                QuestProgress p = quests.Log.Get(tracked);
                QuestStep step = p != null && p.StepIndex < tracked.StepCount ? tracked.GetStep(p.StepIndex) : null;
                if (step != null)
                {
                    objective = quests.CurrentObjectiveText(tracked);
                    bool any = false;
                    foreach (QuestTargetMarker target in QuestTargetMarker.Active)
                    {
                        if (!target.Matches(step)) continue;
                        any = true;
                        Place(Pin(objectivePins, o++, objectiveTemplate), target.transform.position);
                    }
                    if (!any)
                        foreach (SceneGate gate in FindObjectsByType<SceneGate>(FindObjectsSortMode.None))
                            Place(Pin(objectivePins, o++, objectiveTemplate), gate.transform.position); // the objective is beyond this gate
                }
            }
            Hide(objectivePins, o);
            float pulse = 1f + 0.12f * Mathf.Sin(Time.unscaledTime * 4f);
            for (int i = 0; i < o; i++) objectivePins[i].localScale = Vector3.one * pulse;
            if (hintText != null) hintText.text = string.IsNullOrEmpty(objective) ? "Explore to discover new places." : "Objective: " + objective;
        }
    }
}
