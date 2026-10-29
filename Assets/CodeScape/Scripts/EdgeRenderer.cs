using UnityEngine;

namespace CodeScape
{
    public enum EdgeState { Normal, Related, Highlighted, Faded }

    // A dependency line (with an arrowhead) from a caller node to the node it depends on.
    public class EdgeRenderer : MonoBehaviour
    {
        const float NodeClearance = 0.065f;   // keep the line out of the shapes
        const float ArrowLength = 0.03f;

        public NodeView From { get; private set; }
        public NodeView To { get; private set; }
        public EdgeState State { get; private set; }

        LineRenderer line;
        LineRenderer arrow;
        bool active;
        Color activeColor;

        public void Init(NodeView from, NodeView to, Material material)
        {
            From = from;
            To = to;
            name = $"Edge_{from.Id}_{to.Id}";

            if (material == null) material = new Material(Shader.Find("Sprites/Default"));
            line = CreateLine(gameObject, material);
            arrow = CreateLine(new GameObject("Arrow"), material);
            arrow.transform.SetParent(transform, false);

            UpdatePositions();
            Refresh();
        }

        static LineRenderer CreateLine(GameObject go, Material material)
        {
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = material;
            lr.useWorldSpace = false;   // the arrows move with the table (FollowController moves it)
            lr.numCapVertices = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            return lr;
        }

        public void UpdatePositions()
        {
            Vector3 a = From.CenterPoint;
            Vector3 b = To.CenterPoint;
            float y = Mathf.Min(a.y, b.y);
            a.y = y;
            b.y = y;

            Vector3 dir = (b - a).normalized;
            Vector3 start = a + dir * NodeClearance;
            Vector3 end = b - dir * NodeClearance;

            // Points are worked out in world space, then stored in each line's own space so they move with the table.
            line.positionCount = 2;
            line.SetPosition(0, line.transform.InverseTransformPoint(start));
            line.SetPosition(1, line.transform.InverseTransformPoint(end));

            Vector3 side = Vector3.Cross(Vector3.up, dir) * (ArrowLength * 0.6f);
            Vector3 back = end - dir * ArrowLength;
            arrow.positionCount = 3;
            arrow.SetPosition(0, arrow.transform.InverseTransformPoint(back + side));
            arrow.SetPosition(1, arrow.transform.InverseTransformPoint(end));
            arrow.SetPosition(2, arrow.transform.InverseTransformPoint(back - side));
        }

        public void SetState(EdgeState state)
        {
            State = state;
            Refresh();
        }

        // Temporarily overrides the look while the request pulse travels along this edge.
        public void SetActive(bool value, Color color)
        {
            active = value;
            activeColor = color;
            Refresh();
        }

        void Refresh()
        {
            Color color;
            float width;

            if (active)
            {
                color = activeColor;
                width = 0.012f;
            }
            else
            {
                switch (State)
                {
                    case EdgeState.Highlighted:
                        color = Color.Lerp(To.BaseColor, Color.white, 0.25f); width = 0.01f; break;
                    case EdgeState.Related:
                        color = new Color(1f, 1f, 1f, 0.95f); width = 0.007f; break;
                    case EdgeState.Faded:
                        color = new Color(0.6f, 0.6f, 0.65f, 0.12f); width = 0.004f; break;
                    default:
                        color = new Color(0.85f, 0.88f, 0.95f, 0.6f); width = 0.005f; break;
                }
            }

            var faint = new Color(color.r, color.g, color.b, color.a * 0.45f);
            line.startColor = faint;   // brighter toward the dependency = direction
            line.endColor = color;
            line.startWidth = line.endWidth = width;
            arrow.startColor = arrow.endColor = color;
            arrow.startWidth = arrow.endWidth = width;
        }

        public bool Connects(NodeView a, NodeView b)
        {
            return (From == a && To == b) || (From == b && To == a);
        }
    }
}
