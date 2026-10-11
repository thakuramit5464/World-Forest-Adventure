using System.Collections.Generic;
using UnityEngine;

namespace Greenhaven.Editor.AssetGenerator
{
    /// <summary>Structural checks for generated meshes: indices, normals, UVs, finite values, bounds.</summary>
    public static class MeshValidator
    {
        public static bool Validate(Mesh mesh, List<string> problems)
        {
            int before = problems.Count;
            if (mesh == null)
            {
                problems.Add("Mesh is null.");
                return false;
            }

            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            Vector2[] uvs = mesh.uv;
            int[] triangles = mesh.triangles;
            string n = mesh.name;

            if (vertices.Length == 0) problems.Add($"{n}: no vertices.");
            if (triangles.Length == 0 || triangles.Length % 3 != 0) problems.Add($"{n}: triangle index count {triangles.Length} is not a positive multiple of 3.");
            foreach (int index in triangles)
            {
                if (index < 0 || index >= vertices.Length)
                {
                    problems.Add($"{n}: triangle index {index} out of range (vertices: {vertices.Length}).");
                    break;
                }
            }

            if (normals.Length != vertices.Length) problems.Add($"{n}: {normals.Length} normals for {vertices.Length} vertices.");
            else
            {
                for (int i = 0; i < normals.Length; i++)
                {
                    if (!Finite(normals[i]) || Mathf.Abs(normals[i].magnitude - 1f) > 1e-3f)
                    {
                        problems.Add($"{n}: normal {i} is not unit length.");
                        break;
                    }
                }
            }

            if (uvs.Length != vertices.Length) problems.Add($"{n}: {uvs.Length} UVs for {vertices.Length} vertices.");

            if (vertices.Length > 0)
            {
                var computed = new Bounds(vertices[0], Vector3.zero);
                foreach (Vector3 v in vertices)
                {
                    if (!Finite(v))
                    {
                        problems.Add($"{n}: non-finite vertex position.");
                        break;
                    }
                    computed.Encapsulate(v);
                }
                if ((computed.min - mesh.bounds.min).sqrMagnitude > 1e-6f || (computed.max - mesh.bounds.max).sqrMagnitude > 1e-6f)
                    problems.Add($"{n}: stored bounds do not match the vertices.");
            }

            return problems.Count == before;
        }

        static bool Finite(Vector3 v) =>
            !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) ||
              float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));
    }
}
