using Greenhaven.Quests;
using Greenhaven.World;
using UnityEditor;
using UnityEngine;

namespace Greenhaven.Editor.WorldTools
{
    /// <summary>Scene-builder helpers for named places and quest-target tags.</summary>
    public static class LocationSetup
    {
        public static LocationMarker AddLocation(Transform parent, string id, string name, string description, LocationKind kind, Vector3 position, float radius, bool alwaysOnMap = false)
        {
            var go = new GameObject($"Location: {name}");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            SphereCollider trigger = go.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = radius;
            trigger.center = new Vector3(0f, 1f, 0f);
            LocationMarker marker = go.AddComponent<LocationMarker>();
            var so = new SerializedObject(marker);
            so.FindProperty("locationId").stringValue = id;
            so.FindProperty("displayName").stringValue = name;
            so.FindProperty("description").stringValue = description;
            so.FindProperty("kind").enumValueIndex = (int)kind;
            so.FindProperty("alwaysOnMap").boolValue = alwaysOnMap;
            so.ApplyModifiedPropertiesWithoutUndo();
            return marker;
        }

        public static QuestTargetMarker AddQuestTarget(GameObject target, ObjectiveType type, string targetId)
        {
            if (target == null) return null;
            QuestTargetMarker marker = target.GetComponent<QuestTargetMarker>() ?? target.AddComponent<QuestTargetMarker>();
            var so = new SerializedObject(marker);
            so.FindProperty("type").enumValueIndex = (int)type;
            so.FindProperty("targetId").stringValue = targetId;
            so.ApplyModifiedPropertiesWithoutUndo();
            return marker;
        }
    }
}
