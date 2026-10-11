using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Greenhaven.Editor.Cloudpeak
{
    /// <summary>
    /// Physics checks on a freshly built Cloudpeak scene (Edit mode): the spawn is clear, nothing solid
    /// stands on the trail, the summit marker can be seen from the lower viewpoints, and a
    /// CharacterController with the player's exact settings walks the whole route from the forest
    /// entrance to the summit without jumping.
    /// </summary>
    public static class CloudpeakValidation
    {
        public static void Run(CloudpeakWorld world, GameObject player, List<GameObject> walkable, Vector3 summitMarker, StringBuilder log)
        {
            Physics.SyncTransforms();
            var ignore = new HashSet<Collider>(walkable.Where(w => w != null).SelectMany(w => w.GetComponentsInChildren<Collider>()));
            foreach (Collider c in player.GetComponentsInChildren<Collider>()) ignore.Add(c);
            var cc = player.GetComponent<CharacterController>();

            // Spawn.
            Vector3 spawn = player.transform.position;
            float ground = world.Surface(spawn.x, spawn.z);
            Collider[] atSpawn = Physics.OverlapCapsule(spawn + Vector3.up * (cc.radius + 0.4f), spawn + Vector3.up * (cc.height - cc.radius), cc.radius,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore).Where(c => !ignore.Contains(c)).ToArray();
            log.AppendLine(Mathf.Abs(spawn.y - ground) < 0.2f && atSpawn.Length == 0
                ? "Spawn: on clear, walkable ground."
                : $"⚠ Spawn: {spawn.y - ground:0.00} m off the ground; {atSpawn.Length} collider(s) overlap it.");

            // Obstacle sweep: a player-sized capsule (lifted above step height) at every other trail sample.
            var blockers = new Dictionary<string, int>();
            for (int i = 0; i < world.Trail.Count; i += 2)
            {
                TrailPoint t = world.Trail[i];
                Vector3 p = t.Surface == TrailSurface.Bridge ? new Vector3(t.Position.x, world.BridgeDeckHeight + 0.4f, t.Position.y) : world.OnSurface(t.Position);
                foreach (Collider c in Physics.OverlapCapsule(p + Vector3.up * (cc.radius + 0.45f), p + Vector3.up * (cc.height - cc.radius), cc.radius - 0.05f,
                             Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    if (ignore.Contains(c)) continue;
                    string name = c.transform.root == c.transform ? c.name : $"{c.transform.root.name}/{c.name}";
                    blockers[name] = blockers.TryGetValue(name, out int n) ? n + 1 : 1;
                }
            }
            log.AppendLine(blockers.Count == 0
                ? "Obstacles: nothing solid stands on the trail."
                : "⚠ Obstacles on the trail: " + string.Join(", ", blockers.Take(8).Select(b => $"{b.Key} ({b.Value})")));

            // Summit visibility from the lower viewpoints (terrain only; trees are kept out of these view corridors).
            var terrainColliders = new HashSet<Collider>(walkable.Where(w => w != null && w.name.StartsWith("Ground")).SelectMany(w => w.GetComponents<Collider>()));
            foreach ((string name, Vector2 from) in new[] { ("Whispering Glade", world.Glade), ("Foothill Rest", world.FoothillRest) })
            {
                Vector3 eye = world.OnSurface(from, 1.6f);
                Vector3 toMarker = summitMarker - eye;
                bool hidden = Physics.RaycastAll(eye, toMarker.normalized, toMarker.magnitude - 0.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                    .Any(h => terrainColliders.Contains(h.collider));
                log.AppendLine(hidden ? $"⚠ The summit banner is hidden by terrain from {name}." : $"View: the summit banner is in sight from {name} ({toMarker.magnitude:0} m).");
            }

            Walk(world, player, cc, log);
        }

        /// <summary>Steers a copy of the player's CharacterController along the trail at run speed with gravity, never jumping.</summary>
        static void Walk(CloudpeakWorld world, GameObject player, CharacterController source, StringBuilder log)
        {
            List<TrailPoint> trail = world.Trail;
            bool wasActive = player.activeSelf;
            player.SetActive(false);
            var go = new GameObject("~TrailWalkTest");
            try
            {
                CharacterController cc = go.AddComponent<CharacterController>();
                cc.height = source.height;
                cc.radius = source.radius;
                cc.center = source.center;
                cc.stepOffset = source.stepOffset;
                cc.slopeLimit = source.slopeLimit;
                cc.skinWidth = source.skinWidth;
                go.transform.position = world.OnSurface(trail[0].Position, 0.1f);
                Physics.SyncTransforms();

                const float dt = 1f / 50f, speed = 5.5f, gravity = -22f;
                float length = trail[trail.Count - 1].Distance;
                int maxSteps = Mathf.CeilToInt(length / (speed * dt) * 3f) + 2000;
                int progress = 0, best = 0, sinceProgress = 0, steps, groundedSteps = 0;
                float vy = -2f, worstDrop = 0f;
                string failure = null;
                bool arrived = false;
                for (steps = 0; steps < maxSteps; steps++)
                {
                    Vector3 pos = go.transform.position;
                    while (progress < trail.Count - 1 && Flat(pos, trail[progress].Position) < 1.5f) progress++;
                    if (progress == trail.Count - 1 && Flat(pos, trail[progress].Position) < 0.8f)
                    {
                        arrived = true;
                        break;
                    }
                    Vector2 to = trail[progress].Position - new Vector2(pos.x, pos.z);
                    Vector3 dir = new Vector3(to.x, 0f, to.y).normalized;
                    vy = cc.isGrounded ? -2f : Mathf.Max(vy + gravity * dt, -30f);
                    if (cc.isGrounded) groundedSteps++;
                    cc.Move((dir * speed + Vector3.up * vy) * dt);

                    float expected = trail[Mathf.Max(0, progress - 1)].Surface == TrailSurface.Bridge ? world.BridgeDeckHeight : trail[Mathf.Max(0, progress - 1)].Height;
                    worstDrop = Mathf.Max(worstDrop, expected - go.transform.position.y);
                    if (expected - go.transform.position.y > 3f)
                    {
                        failure = $"fell off the trail near {trail[progress].Distance:0} m";
                        break;
                    }
                    if (progress > best)
                    {
                        best = progress;
                        sinceProgress = 0;
                    }
                    else if (++sinceProgress > 300)
                    {
                        failure = $"stuck near {trail[progress].Distance:0} m ({trail[progress].Section}) at {go.transform.position}";
                        break;
                    }
                }
                if (arrived)
                    log.AppendLine($"Walk test: a CharacterController with the player's settings (slope {cc.slopeLimit}°, step {cc.stepOffset} m) " +
                                   $"reached the summit from the forest entrance without jumping: {length:0} m in {steps * dt:0} s at run speed, " +
                                   $"grounded {100f * groundedSteps / Mathf.Max(1, steps):0}% of the time.");
                else
                    log.AppendLine($"⚠ Walk test failed: {failure ?? "ran out of time"} (reached {trail[best].Distance:0} of {length:0} m).");
            }
            finally
            {
                Object.DestroyImmediate(go);
                player.SetActive(wasActive);
                Physics.SyncTransforms();
            }
        }

        static float Flat(Vector3 a, Vector2 b) => Vector2.Distance(new Vector2(a.x, a.z), b);
    }
}
