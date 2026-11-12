using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlip
{
    // A thick line drawn on a canvas, used for the racer on the draw board.
    public class UIStroke : MaskableGraphic
    {
        public readonly List<Vector2> points = new List<Vector2>();
        public float thickness = 14f;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            float half = thickness * 0.5f;
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 p = points[i];
                // a round-ish dot at every point, so joins and ends look soft
                AddQuad(vh, p + new Vector2(-half, -half), p + new Vector2(-half, half), p + new Vector2(half, half), p + new Vector2(half, -half));
                AddQuad(vh, p + new Vector2(0f, -half * 1.25f), p + new Vector2(-half * 1.25f, 0f), p + new Vector2(0f, half * 1.25f), p + new Vector2(half * 1.25f, 0f));

                if (i == 0) continue;
                Vector2 q = points[i - 1];
                Vector2 along = p - q;
                if (along.sqrMagnitude < 0.0001f) continue;
                Vector2 side = new Vector2(-along.y, along.x).normalized * half;
                AddQuad(vh, q - side, q + side, p + side, p - side);
            }
        }

        void AddQuad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            var quad = new UIVertex[4];
            Vector2[] corners = { a, b, c, d };
            for (int i = 0; i < 4; i++)
            {
                quad[i] = UIVertex.simpleVert;
                quad[i].position = corners[i];
                quad[i].color = color;
            }
            vh.AddUIVertexQuad(quad);
        }

        public void Changed() => SetVerticesDirty();
    }
}
