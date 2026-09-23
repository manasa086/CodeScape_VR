using TMPro;
using UnityEngine;

namespace CodeScape
{
    public enum NodeState { Normal, Selected, Dependency, Faded }

    // One architecture component on the table: shape, label, collider and visual state.
    public class NodeView : MonoBehaviour, IRayTarget
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        public NodeData Data { get; private set; }
        public string Id => Data.id;
        public Color BaseColor { get; private set; }
        public NodeState State { get; private set; }
        public bool IsHovered { get; private set; }

        // World position just above the shape, used by the request pulse.
        public Vector3 TopPoint => transform.position + Vector3.up * (topHeight + 0.03f);
        // World position at the middle of the shape, used by the connection lines.
        public Vector3 CenterPoint => transform.position + Vector3.up * (topHeight * 0.5f);

        Transform shape;
        Vector3 shapeScale;
        Material material;
        TextMeshPro label;
        Renderer ring;
        Material ringMaterial;
        float topHeight;

        bool failed;
        float flash;
        Color flashColor;

        public void Init(NodeData data, Material nodeTemplate, Material glowTemplate, TMP_FontAsset font)
        {
            Data = data;
            BaseColor = NodeStyle.ColorFor(data.type);
            name = "Node_" + data.id;

            BuildShape(nodeTemplate);
            BuildRing(glowTemplate);
            BuildLabel(font);
            BuildCollider();
            Refresh();
        }

        void BuildShape(Material template)
        {
            PrimitiveType primitive;
            Vector3 scale;
            Quaternion rotation = Quaternion.identity;

            switch (Data.type)
            {
                case "frontend":   // a small "screen"
                    primitive = PrimitiveType.Cube; scale = new Vector3(0.13f, 0.085f, 0.02f); break;
                case "api":        // a diamond
                    primitive = PrimitiveType.Cube; scale = new Vector3(0.07f, 0.07f, 0.07f);
                    rotation = Quaternion.Euler(0f, 45f, 0f); break;
                case "database":
                    primitive = PrimitiveType.Cylinder; scale = new Vector3(0.085f, 0.05f, 0.085f); break;
                case "external":
                    primitive = PrimitiveType.Sphere; scale = new Vector3(0.095f, 0.095f, 0.095f); break;
                default:           // service
                    primitive = PrimitiveType.Cube; scale = new Vector3(0.08f, 0.08f, 0.08f); break;
            }

            var go = GameObject.CreatePrimitive(primitive);
            go.name = "Shape";
            Destroy(go.GetComponent<Collider>());
            shape = go.transform;
            shape.SetParent(transform, false);

            // Cylinder primitives are 2 units tall, the others 1 unit.
            float height = primitive == PrimitiveType.Cylinder ? scale.y * 2f : scale.y;
            shape.localPosition = new Vector3(0f, height * 0.5f, 0f);
            shape.localRotation = rotation;
            shape.localScale = scale;
            shapeScale = scale;
            topHeight = height;

            material = template != null ? new Material(template) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.EnableKeyword("_EMISSION");
            go.GetComponent<Renderer>().sharedMaterial = material;
        }

        void BuildRing(Material template)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "SelectionRing";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.002f, 0f);
            go.transform.localScale = new Vector3(0.15f, 0.0015f, 0.15f);
            ring = go.GetComponent<Renderer>();
            ringMaterial = template != null ? new Material(template) : new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            ring.sharedMaterial = ringMaterial;
            ring.enabled = false;
        }

        void BuildLabel(TMP_FontAsset font)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, topHeight + 0.035f, 0f);
            label = go.AddComponent<TextMeshPro>();
            if (font != null) label.font = font;
            label.text = $"<b>{Data.name}</b>";
            label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta = new Vector2(0.28f, 0.045f);
            label.enableAutoSizing = true;
            label.fontSizeMin = 0.05f;
            label.fontSizeMax = 0.26f;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.outlineWidth = 0.2f;
            label.outlineColor = new Color32(0, 0, 0, 200);
            go.AddComponent<Billboard>();
        }

        void BuildCollider()
        {
            // A slightly oversized box makes small nodes easy to hit with the ray.
            var box = gameObject.AddComponent<BoxCollider>();
            float w = Mathf.Max(shapeScale.x, shapeScale.z) * 1.35f;
            box.size = new Vector3(w, topHeight * 1.3f, w);
            box.center = new Vector3(0f, topHeight * 0.5f, 0f);
        }

        public void SetState(NodeState state)
        {
            State = state;
            Refresh();
        }

        public void SetFailed(bool value)
        {
            failed = value;
            Refresh();
        }

        // Short glow, used when the request pulse arrives at this node.
        public void Flash(Color color)
        {
            flash = 1f;
            flashColor = color;
            Refresh();
        }

        void Update()
        {
            if (flash <= 0f && !failed) return;
            flash = Mathf.Max(0f, flash - Time.deltaTime * 1.2f);
            Refresh();   // also animates the blinking failure ring
        }

        void Refresh()
        {
            if (material == null) return;

            Color color = BaseColor;
            Color emission = Color.black;
            float scale = 1f;
            float labelAlpha = 1f;
            Color ringColor = Color.white;
            bool showRing = false;

            switch (State)
            {
                case NodeState.Selected:
                    emission = BaseColor * 0.9f;
                    scale = 1.15f;
                    showRing = true;
                    ringColor = new Color(1f, 0.92f, 0.3f);
                    break;
                case NodeState.Dependency:
                    emission = BaseColor * 0.5f;
                    scale = 1.08f;
                    showRing = true;
                    ringColor = BaseColor;
                    break;
                case NodeState.Faded:
                    color = Color.Lerp(BaseColor, NodeStyle.Faded, 0.8f);
                    labelAlpha = 0.25f;
                    break;
            }

            if (IsHovered)
            {
                emission += new Color(0.25f, 0.25f, 0.25f);
                scale *= 1.06f;
                labelAlpha = 1f;
            }

            if (failed)
            {
                float blink = 0.5f + 0.5f * Mathf.Sin(Time.time * 8f);
                color = Color.Lerp(color, NodeStyle.Failure, 0.6f);
                emission += NodeStyle.Failure * (0.4f + 0.8f * blink);
                showRing = true;
                ringColor = Color.Lerp(new Color(0.35f, 0f, 0f), NodeStyle.Failure, blink);
            }

            if (flash > 0f) emission += flashColor * (flash * 1.6f);

            material.SetColor(BaseColorId, color);
            material.color = color;
            material.SetColor(EmissionId, emission);
            shape.localScale = shapeScale * scale;
            label.alpha = labelAlpha;

            ring.enabled = showRing;
            ringMaterial.SetColor(BaseColorId, ringColor);
            ringMaterial.color = ringColor;
        }

        public void OnHoverEnter()
        {
            IsHovered = true;
            Refresh();
        }

        public void OnHoverExit()
        {
            IsHovered = false;
            Refresh();
        }

        public void OnSelect()
        {
            if (SelectionManager.Instance != null) SelectionManager.Instance.Select(this);
        }
    }
}
