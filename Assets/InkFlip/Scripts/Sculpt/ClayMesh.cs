using System.Collections.Generic;
using UnityEngine;

namespace InkFlip
{
    public enum ClayTool
    {
        // sculpt the surface of one shape
        Add,    // build clay up
        Dent,   // press clay in
        Smooth, // relax lumps
        Grab,   // drag a patch of clay around

        // arrange the shapes themselves
        Move,   // slide a shape around
        Rotate, // rotation rings around the selected shape
        Resize, // drag up / down to grow or shrink a shape
        Spin,   // turn a shape freely, like a trackball
    }

    // Starting shapes. They are all the same cube-sphere grid pushed into a different form,
    // so they share the same UV layout, sculpting and painting.
    public enum ClayShape
    {
        Sphere,
        Cube,
        Cylinder,
        Cone,
        Pyramid,
        Vase,   // thrown on the pottery wheel: a cylinder with its own radius at every height
    }

    // A lump of "playdough": a grid of points that can be pushed, pressed, smoothed and dragged.
    //
    // The topology never changes while sculpting, which is what keeps the mesh paintable: every face
    // of the cube-sphere is its own UV2 island in a 3x3 atlas, so the ink system (which needs clean,
    // non-overlapping second UVs) works on a mesh that was created at runtime. Paint also stays stuck
    // to the clay when it is deformed afterwards.
    //
    // Points shared between faces are welded: edges of the cube-sphere exist as duplicate vertices
    // (they need different UVs), but sculpting moves one "unique" point and copies it to all of them,
    // so the surface never splits open along the seams.
    //
    // Keeping the mesh healthy: every edit is checked before it sticks. If a step would flip a triangle
    // inside out (the surface folding through itself), produce a non-finite number, or push clay through
    // the middle, it is undone or shortened - the clay simply "resists". EnsureHealthy is a last check
    // before a sculpture is placed.
    public class ClayMesh
    {
        public readonly Mesh Mesh;
        public readonly ClayShape Shape;
        public readonly float Radius;

        // the cube-sphere's six faces: n is the face normal, u and v span it
        static readonly Vector3[] FaceNormal = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        static readonly Vector3[] FaceU = { Vector3.up, Vector3.forward, Vector3.forward, Vector3.right, Vector3.right, Vector3.up };
        static readonly Vector3[] FaceV = { Vector3.forward, Vector3.up, Vector3.right, Vector3.forward, Vector3.up, Vector3.right };

        // gap left around each face island in the UV atlas (fraction of one cell) so paint can bleed past the edge
        const float CellMargin = 0.03f;

        // clay can't be pushed closer to the middle than this, or further out than this (in radii)
        const float MinCentreDistance = 0.12f;
        const float MaxCentreDistance = 3f;

        // a triangle whose normal swings more than this in one step has folded over (cos of ~78 degrees)
        const float FoldCosine = 0.2f;

        readonly int resolution;
        readonly int[] triangles;
        readonly int[] vertexToUnique;
        readonly int[][] pointTriangles;   // for each unique point, the triangles that use it
        readonly List<int>[] neighbours;
        readonly Vector3[] positions;      // one per unique point
        readonly Vector3[] normals;        // one per unique point
        readonly Vector3[] meshVertices;
        readonly Vector3[] meshNormals;
        readonly Vector3[] originalPositions;
        readonly Vector3[] unitPoints;     // each unique point's spot on the 2x2x2 cube, used to rebuild lathe shapes

        // scratch lists reused by every brush stroke
        readonly List<int> touched = new List<int>();
        readonly List<float> touchedWeight = new List<float>();
        readonly List<Vector3> smoothTargets = new List<Vector3>();

        // active grab (see BeginGrab)
        readonly List<int> grabIndices = new List<int>();
        readonly List<float> grabWeights = new List<float>();
        readonly List<Vector3> grabOrigins = new List<Vector3>();
        readonly List<bool> grabMirrored = new List<bool>();
        Vector3 grabLastOffset;
        Vector3 grabLastMirroredOffset;

        // the edit guard: what the touched points and their triangles looked like before an edit
        readonly List<int> guardIndices = new List<int>();
        readonly List<Vector3> guardPositions = new List<Vector3>();
        readonly List<int> guardTriangles = new List<int>();
        readonly List<Vector3> guardNormals = new List<Vector3>();
        readonly int[] triangleStamp;
        int stamp;

        public int UniqueCount => positions.Length;

        public ClayMesh(ClayShape shape, int resolution = 24, float radius = 1f)
        {
            Shape = shape;
            this.resolution = Mathf.Max(4, resolution);
            Radius = radius;

            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var unique = new List<Vector3>();
            var unitList = new List<Vector3>();
            var uniqueLookup = new Dictionary<Vector3Int, int>();
            var toUnique = new List<int>();
            var tris = new List<int>();

            int stride = this.resolution + 1;
            for (int face = 0; face < 6; face++)
            {
                int cellX = face % 3;
                int cellY = face / 3;
                int firstVertex = vertices.Count;

                for (int j = 0; j <= this.resolution; j++)
                {
                    for (int i = 0; i <= this.resolution; i++)
                    {
                        float tu = i / (float)this.resolution;
                        float tv = j / (float)this.resolution;
                        Vector3 cubePoint = FaceNormal[face] + FaceU[face] * (tu * 2f - 1f) + FaceV[face] * (tv * 2f - 1f);
                        Vector3 point = ShapePoint(shape, cubePoint) * radius;

                        vertices.Add(point);
                        uvs.Add(new Vector2(
                            (cellX + CellMargin + tu * (1f - 2f * CellMargin)) / 3f,
                            (cellY + CellMargin + tv * (1f - 2f * CellMargin)) / 3f));

                        // weld: the same point on two faces maps to one unique index
                        var key = Vector3Int.RoundToInt(cubePoint.normalized * 1000f);
                        if (!uniqueLookup.TryGetValue(key, out int id))
                        {
                            id = unique.Count;
                            unique.Add(point);
                            unitList.Add(cubePoint);
                            uniqueLookup[key] = id;
                        }
                        toUnique.Add(id);
                    }
                }

                for (int j = 0; j < this.resolution; j++)
                {
                    for (int i = 0; i < this.resolution; i++)
                    {
                        int v00 = firstVertex + j * stride + i;
                        int v10 = v00 + 1;
                        int v01 = v00 + stride;
                        int v11 = v01 + 1;
                        AddOutwardTriangle(tris, vertices, v00, v01, v10);
                        AddOutwardTriangle(tris, vertices, v10, v01, v11);
                    }
                }
            }

            triangles = tris.ToArray();
            vertexToUnique = toUnique.ToArray();
            positions = unique.ToArray();
            originalPositions = unique.ToArray();
            unitPoints = unitList.ToArray();
            normals = new Vector3[positions.Length];
            meshVertices = new Vector3[vertices.Count];
            meshNormals = new Vector3[vertices.Count];
            triangleStamp = new int[triangles.Length / 3];

            neighbours = new List<int>[positions.Length];
            var usedBy = new List<int>[positions.Length];
            for (int i = 0; i < neighbours.Length; i++)
            {
                neighbours[i] = new List<int>(8);
                usedBy[i] = new List<int>(8);
            }
            for (int t = 0; t < triangles.Length; t += 3)
            {
                int a = vertexToUnique[triangles[t]];
                int b = vertexToUnique[triangles[t + 1]];
                int c = vertexToUnique[triangles[t + 2]];
                Link(a, b); Link(b, c); Link(c, a);
                usedBy[a].Add(t / 3); usedBy[b].Add(t / 3); usedBy[c].Add(t / 3);
            }
            pointTriangles = new int[positions.Length][];
            for (int i = 0; i < pointTriangles.Length; i++) pointTriangles[i] = usedBy[i].ToArray();

            Mesh = new Mesh { name = "ClayMesh" };
            Mesh.MarkDynamic();
            Mesh.SetVertices(vertices);
            Mesh.SetUVs(0, uvs);
            Mesh.SetUVs(1, uvs); // UV2 drives the paint layer
            Mesh.SetTriangles(triangles, 0);
            Refresh();
        }

        // 'c' is a point on the surface of a 2x2x2 cube; this pushes it onto the chosen shape.
        // Every shape is centred on the origin so the "keep away from the middle" rule works for all of them.
        static Vector3 ShapePoint(ClayShape shape, Vector3 c)
        {
            switch (shape)
            {
                case ClayShape.Cube:
                    return c * 0.8f;

                case ClayShape.Vase:
                case ClayShape.Cylinder:
                {
                    Vector2 disc = SquareToDisc(c.x, c.z);
                    return new Vector3(disc.x * 0.85f, c.y * 0.9f, disc.y * 0.85f);
                }

                case ClayShape.Cone:
                {
                    // narrows toward +Y; the tip stays a little blunt so no triangles collapse to nothing
                    float narrow = Mathf.Lerp(1f, 0.07f, (c.y + 1f) * 0.5f);
                    Vector2 disc = SquareToDisc(c.x, c.z);
                    return new Vector3(disc.x * 1f * narrow, c.y * 0.95f, disc.y * 1f * narrow);
                }

                case ClayShape.Pyramid:
                {
                    float narrow = Mathf.Lerp(1f, 0.07f, (c.y + 1f) * 0.5f);
                    return new Vector3(c.x * 1.05f * narrow, c.y * 0.95f, c.z * 1.05f * narrow);
                }

                default:
                    return c.normalized;
            }
        }

        // maps the boundary of a square onto a circle, keeping the grid even
        static Vector2 SquareToDisc(float x, float z)
        {
            return new Vector2(x * Mathf.Sqrt(1f - z * z * 0.5f), z * Mathf.Sqrt(1f - x * x * 0.5f));
        }

        // flips a triangle's winding if needed so it faces away from the centre of the lump
        static void AddOutwardTriangle(List<int> tris, List<Vector3> vertices, int a, int b, int c)
        {
            Vector3 normal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
            Vector3 centre = (vertices[a] + vertices[b] + vertices[c]) / 3f;
            if (Vector3.Dot(normal, centre) < 0f) { int swap = b; b = c; c = swap; }
            tris.Add(a); tris.Add(b); tris.Add(c);
        }

        void Link(int a, int b)
        {
            if (a == b) return;
            if (!neighbours[a].Contains(b)) neighbours[a].Add(b);
            if (!neighbours[b].Contains(a)) neighbours[b].Add(a);
        }

        // ---------- editing ----------

        // Add / Dent / Smooth at one spot. 'amount' already includes frame time: for Add and Dent it is how
        // far the middle of the brush may move, for Smooth it is a 0-1 blend toward the neighbours' average.
        // If the step would fold the surface it is retried with half the strength, then a quarter, then skipped.
        public void Brush(ClayTool tool, Vector3 centre, float radius, float amount)
        {
            if (tool != ClayTool.Add && tool != ClayTool.Dent && tool != ClayTool.Smooth) return;

            Collect(centre, radius);
            if (touched.Count == 0) return;

            BeginGuard(touched);
            float factor = 1f;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                ApplyBrushStep(tool, amount * factor);
                if (EndGuard()) return;
                factor *= 0.5f;
            }
        }

        void ApplyBrushStep(ClayTool tool, float amount)
        {
            if (tool == ClayTool.Smooth)
            {
                // work out every target first so the result doesn't depend on visiting order
                smoothTargets.Clear();
                for (int k = 0; k < touched.Count; k++)
                {
                    List<int> around = neighbours[touched[k]];
                    Vector3 average = Vector3.zero;
                    for (int n = 0; n < around.Count; n++) average += positions[around[n]];
                    smoothTargets.Add(average / Mathf.Max(1, around.Count));
                }
                for (int k = 0; k < touched.Count; k++)
                {
                    float blend = Mathf.Clamp01(amount * touchedWeight[k]);
                    positions[touched[k]] = Limit(Vector3.Lerp(positions[touched[k]], smoothTargets[k], blend));
                }
                return;
            }

            // one shared direction for the whole stroke, so a dab bulges instead of ruffling
            Vector3 direction = Vector3.zero;
            for (int k = 0; k < touched.Count; k++) direction += normals[touched[k]] * touchedWeight[k];
            if (direction.sqrMagnitude < 1e-8f) return;
            direction.Normalize();
            float sign = tool == ClayTool.Add ? 1f : -1f;
            for (int k = 0; k < touched.Count; k++)
            {
                positions[touched[k]] = Limit(positions[touched[k]] + direction * (sign * amount * touchedWeight[k]));
            }
        }

        // Grab: pick up the clay under the brush, then drag it with DragGrab. Call ClearGrab first,
        // then BeginGrab once per spot (the mirrored spot is a second call with mirrored = true).
        public void ClearGrab()
        {
            grabIndices.Clear();
            grabWeights.Clear();
            grabOrigins.Clear();
            grabMirrored.Clear();
            grabLastOffset = Vector3.zero;
            grabLastMirroredOffset = Vector3.zero;
        }

        public void BeginGrab(Vector3 centre, float radius, bool mirrored)
        {
            Collect(centre, radius);
            for (int k = 0; k < touched.Count; k++)
            {
                grabIndices.Add(touched[k]);
                grabWeights.Add(touchedWeight[k]);
                grabOrigins.Add(positions[touched[k]]);
                grabMirrored.Add(mirrored);
            }
        }

        // 'offset' is measured from where the grab started, in the clay's local space. If the full drag
        // would fold the surface, the clay follows as far as it can instead of tearing or inverting.
        public void DragGrab(Vector3 offset, Vector3 mirroredOffset)
        {
            if (grabIndices.Count == 0) return;

            BeginGuard(grabIndices);
            Vector3 fromOffset = grabLastOffset;
            Vector3 fromMirrored = grabLastMirroredOffset;
            float t = 1f;
            for (int attempt = 0; attempt < 4; attempt++)
            {
                Vector3 tryOffset = Vector3.Lerp(fromOffset, offset, t);
                Vector3 tryMirrored = Vector3.Lerp(fromMirrored, mirroredOffset, t);
                ApplyGrab(tryOffset, tryMirrored);
                if (EndGuard())
                {
                    grabLastOffset = tryOffset;
                    grabLastMirroredOffset = tryMirrored;
                    return;
                }
                t *= 0.5f;
            }
        }

        void ApplyGrab(Vector3 offset, Vector3 mirroredOffset)
        {
            for (int k = 0; k < grabIndices.Count; k++)
            {
                Vector3 o = grabMirrored[k] ? mirroredOffset : offset;
                positions[grabIndices[k]] = Limit(grabOrigins[k] + o * grabWeights[k]);
            }
        }

        // every unique point within 'radius' of 'centre', with a smooth 1-to-0 falloff
        void Collect(Vector3 centre, float radius)
        {
            touched.Clear();
            touchedWeight.Clear();
            float radiusSq = radius * radius;
            for (int i = 0; i < positions.Length; i++)
            {
                float dSq = (positions[i] - centre).sqrMagnitude;
                if (dSq >= radiusSq) continue;
                float t = 1f - dSq / radiusSq;
                touched.Add(i);
                touchedWeight.Add(t * t);
            }
        }

        // keeps clay out of the very middle (so a dent can't push through to the other side)
        // and stops wild drags from flinging it across the room
        Vector3 Limit(Vector3 p)
        {
            float min = Radius * MinCentreDistance;
            float max = Radius * MaxCentreDistance;
            float length = p.magnitude;
            if (length > max) return p / length * max;
            if (length < min) return length > 1e-6f ? p / length * min : Vector3.up * min;
            return p;
        }

        // ---------- keeping the mesh healthy ----------

        // remembers the touched points and the triangles around them, before an edit
        void BeginGuard(List<int> indices)
        {
            guardIndices.Clear();
            guardPositions.Clear();
            guardTriangles.Clear();
            guardNormals.Clear();
            stamp++;

            for (int k = 0; k < indices.Count; k++)
            {
                int point = indices[k];
                guardIndices.Add(point);
                guardPositions.Add(positions[point]);
                foreach (int triangle in pointTriangles[point])
                {
                    if (triangleStamp[triangle] == stamp) continue;
                    triangleStamp[triangle] = stamp;
                    guardTriangles.Add(triangle);
                    guardNormals.Add(TriangleNormal(triangle));
                }
            }
        }

        // checks the edit. If any point went non-finite or any triangle folded over, the edit is rolled
        // back to what BeginGuard remembered and false is returned.
        bool EndGuard()
        {
            bool healthy = true;
            for (int k = 0; k < guardIndices.Count && healthy; k++)
            {
                if (!IsFinite(positions[guardIndices[k]])) healthy = false;
            }

            for (int k = 0; k < guardTriangles.Count && healthy; k++)
            {
                Vector3 before = guardNormals[k];
                if (before.sqrMagnitude < 1e-14f) continue; // was already flat, nothing to compare with
                Vector3 after = TriangleNormal(guardTriangles[k]);
                if (!IsFinite(after)) { healthy = false; break; }
                float cosine = Vector3.Dot(before, after) / Mathf.Sqrt(before.sqrMagnitude * Mathf.Max(after.sqrMagnitude, 1e-20f));
                if (cosine < FoldCosine) healthy = false;
            }

            if (!healthy)
            {
                for (int k = 0; k < guardIndices.Count; k++) positions[guardIndices[k]] = guardPositions[k];
            }
            return healthy;
        }

        Vector3 TriangleNormal(int triangle)
        {
            int t = triangle * 3;
            Vector3 a = positions[vertexToUnique[triangles[t]]];
            Vector3 b = positions[vertexToUnique[triangles[t + 1]]];
            Vector3 c = positions[vertexToUnique[triangles[t + 2]]];
            return Vector3.Cross(b - a, c - a);
        }

        static bool IsFinite(Vector3 v)
        {
            return !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) ||
                     float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));
        }

        // Last check before a sculpture is placed. Repairs an inside-out mesh by flipping every triangle;
        // returns false (with a reason) if the lump is broken beyond that - non-finite points, or squashed
        // down to almost no volume.
        public bool EnsureHealthy(out string problem)
        {
            problem = null;
            for (int i = 0; i < positions.Length; i++)
            {
                if (!IsFinite(positions[i]))
                {
                    problem = "broke";
                    return false;
                }
            }

            float volume = SignedVolume();
            if (volume < 0f)
            {
                FlipWinding();
                volume = -volume;
            }

            if (volume < 0.02f * Radius * Radius * Radius)
            {
                problem = "got squashed flat";
                return false;
            }
            return true;
        }

        // positive when the triangles face outward, negative when the whole mesh is inside out
        float SignedVolume()
        {
            float volume = 0f;
            for (int t = 0; t < triangles.Length; t += 3)
            {
                Vector3 a = positions[vertexToUnique[triangles[t]]];
                Vector3 b = positions[vertexToUnique[triangles[t + 1]]];
                Vector3 c = positions[vertexToUnique[triangles[t + 2]]];
                volume += Vector3.Dot(a, Vector3.Cross(b, c));
            }
            return volume / 6f;
        }

        void FlipWinding()
        {
            for (int t = 0; t < triangles.Length; t += 3)
            {
                int swap = triangles[t + 1];
                triangles[t + 1] = triangles[t + 2];
                triangles[t + 2] = swap;
            }
            Mesh.SetTriangles(triangles, 0);
            Refresh();
        }

        // ---------- pottery wheel ----------

        // Rebuilds a Vase from a profile: radii[i] is the radius at height i / (count - 1), from the bottom
        // to the top, and the lump is 2 * halfHeight tall. This replaces every point, so it is meant for the
        // wheel step, before any hand sculpting; the result becomes the shape Reset returns to.
        public void SetProfile(float[] radii, float halfHeight)
        {
            int last = radii.Length - 1;
            for (int u = 0; u < positions.Length; u++)
            {
                Vector3 c = unitPoints[u];
                float t = (c.y + 1f) * 0.5f * last;
                int i = Mathf.Clamp(Mathf.FloorToInt(t), 0, last - 1);
                float radius = Mathf.Lerp(radii[i], radii[i + 1], t - i) * Radius;

                Vector2 disc = SquareToDisc(c.x, c.z);
                Vector3 point = new Vector3(disc.x * radius, c.y * halfHeight * Radius, disc.y * radius);
                positions[u] = point;
                originalPositions[u] = point;
            }
            Refresh();
        }

        // distance from the middle to the furthest point - sizes the rotation rings
        public float MaxRadius()
        {
            float best = 0f;
            for (int i = 0; i < positions.Length; i++)
            {
                float sq = positions[i].sqrMagnitude;
                if (sq > best) best = sq;
            }
            return Mathf.Sqrt(best);
        }

        // how far the lump reaches along a direction (in its own space) - used to rest one shape against another
        public float Support(Vector3 direction)
        {
            float best = float.MinValue;
            for (int i = 0; i < positions.Length; i++)
            {
                float d = Vector3.Dot(positions[i], direction);
                if (d > best) best = d;
            }
            return best;
        }

        // ---------- snapshots ----------

        public Vector3[] Snapshot()
        {
            return (Vector3[])positions.Clone();
        }

        public void Restore(Vector3[] snapshot)
        {
            if (snapshot == null || snapshot.Length != positions.Length) return;
            System.Array.Copy(snapshot, positions, positions.Length);
            Refresh();
        }

        public void ResetToShape()
        {
            System.Array.Copy(originalPositions, positions, positions.Length);
            Refresh();
        }

        // ---------- mesh ----------

        // pushes the edited points into the real mesh (recomputes smooth normals across the seams)
        public void Refresh()
        {
            RebuildNormals();
            for (int v = 0; v < meshVertices.Length; v++)
            {
                int u = vertexToUnique[v];
                meshVertices[v] = positions[u];
                meshNormals[v] = normals[u];
            }
            Mesh.SetVertices(meshVertices);
            Mesh.SetNormals(meshNormals);
            Mesh.RecalculateBounds();
        }

        void RebuildNormals()
        {
            System.Array.Clear(normals, 0, normals.Length);
            for (int t = 0; t < triangles.Length; t += 3)
            {
                int a = vertexToUnique[triangles[t]];
                int b = vertexToUnique[triangles[t + 1]];
                int c = vertexToUnique[triangles[t + 2]];
                // area-weighted, accumulated per unique point so duplicated seam vertices agree
                Vector3 n = Vector3.Cross(positions[b] - positions[a], positions[c] - positions[a]);
                normals[a] += n; normals[b] += n; normals[c] += n;
            }
            for (int i = 0; i < normals.Length; i++)
            {
                normals[i] = normals[i].sqrMagnitude > 1e-12f ? normals[i].normalized : positions[i].normalized;
            }
        }

        // ray (in the clay's local space) against the lump; works off the CPU copy so no collider rebuilds are needed.
        // The direction doesn't need to be normalised, so a scaled part can be hit with a ray brought into its space.
        public bool Raycast(Ray localRay, out Vector3 point, out Vector3 normal)
        {
            point = Vector3.zero;
            normal = Vector3.up;
            float best = float.MaxValue;
            int bestTriangle = -1;
            float bestU = 0f, bestV = 0f;

            Vector3 origin = localRay.origin;
            Vector3 direction = localRay.direction;
            for (int t = 0; t < triangles.Length; t += 3)
            {
                Vector3 a = positions[vertexToUnique[triangles[t]]];
                Vector3 e1 = positions[vertexToUnique[triangles[t + 1]]] - a;
                Vector3 e2 = positions[vertexToUnique[triangles[t + 2]]] - a;

                Vector3 p = Vector3.Cross(direction, e2);
                float det = Vector3.Dot(e1, p);
                if (Mathf.Abs(det) < 1e-9f) continue;
                float inv = 1f / det;

                Vector3 s = origin - a;
                float u = Vector3.Dot(s, p) * inv;
                if (u < 0f || u > 1f) continue;

                Vector3 q = Vector3.Cross(s, e1);
                float v = Vector3.Dot(direction, q) * inv;
                if (v < 0f || u + v > 1f) continue;

                float distance = Vector3.Dot(e2, q) * inv;
                if (distance > 0f && distance < best)
                {
                    best = distance;
                    bestTriangle = t;
                    bestU = u;
                    bestV = v;
                }
            }

            if (bestTriangle < 0) return false;

            point = origin + direction * best;
            Vector3 na = normals[vertexToUnique[triangles[bestTriangle]]];
            Vector3 nb = normals[vertexToUnique[triangles[bestTriangle + 1]]];
            Vector3 nc = normals[vertexToUnique[triangles[bestTriangle + 2]]];
            normal = (na * (1f - bestU - bestV) + nb * bestU + nc * bestV).normalized;
            return true;
        }

        // a standalone copy of the lump for the finished sculpture
        public Mesh BakeCopy()
        {
            Mesh copy = Object.Instantiate(Mesh);
            copy.name = "Sculpture_" + Shape;
            return copy;
        }
    }
}
