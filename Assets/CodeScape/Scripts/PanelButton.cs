using TMPro;
using UnityEngine;

namespace CodeScape
{
    public enum ButtonAction { HighlightDependencies, TraceRequest, SimulateFailure, Reset, ToggleFollow }

    // A physical 3D button on the table console. Selected with the controller ray or mouse.
    public class PanelButton : MonoBehaviour, IRayTarget
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        public ButtonAction Action { get; private set; }

        Material material;
        Transform cap;
        TextMeshPro label;
        Color baseColor;
        Color accent;
        bool hovered;
        bool toggled;
        float pressTimer;
        string text;
        string toggledText;

        public static PanelButton Create(Transform parent, ButtonAction action, string text, string toggledText,
                                         Color accent, Vector3 localPos, Material template, TMP_FontAsset font)
        {
            var root = new GameObject("Button_" + action);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPos;
            var button = root.AddComponent<PanelButton>();
            button.Action = action;
            button.accent = accent;
            button.text = text;
            button.toggledText = toggledText ?? text;
            button.Build(template, font);
            return button;
        }

        void Build(Material template, TMP_FontAsset font)
        {
            var size = new Vector3(0.19f, 0.022f, 0.07f);

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Cap";
            Destroy(go.GetComponent<Collider>());
            cap = go.transform;
            cap.SetParent(transform, false);
            cap.localScale = size;
            cap.localPosition = new Vector3(0f, size.y * 0.5f, 0f);
            material = template != null ? new Material(template) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.EnableKeyword("_EMISSION");
            go.GetComponent<Renderer>().sharedMaterial = material;
            baseColor = Color.Lerp(new Color(0.12f, 0.13f, 0.16f), accent, 0.35f);

            var box = gameObject.AddComponent<BoxCollider>();
            box.size = new Vector3(size.x, size.y * 2f, size.z);
            box.center = new Vector3(0f, size.y, 0f);

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(cap, false);
            // Lie flat on top of the cap, readable by someone standing in front of the table.
            labelGo.transform.localPosition = new Vector3(0f, 0.51f, 0f);
            labelGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            label = labelGo.AddComponent<TextMeshPro>();
            if (font != null) label.font = font;
            label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta = new Vector2(size.x * 0.92f, size.z * 0.85f);
            label.enableAutoSizing = true;
            label.fontSizeMin = 0.05f;
            label.fontSizeMax = 0.2f;
            label.color = Color.white;
            // The parent cap is non-uniformly scaled; undo that so the text keeps its shape.
            labelGo.transform.localScale = new Vector3(1f / size.x, 1f / size.z, 1f / size.y);
            Refresh();
        }

        public void SetToggled(bool value)
        {
            toggled = value;
            Refresh();
        }

        void Update()
        {
            if (pressTimer <= 0f) return;
            pressTimer -= Time.deltaTime;
            Refresh();
        }

        void Refresh()
        {
            if (material == null) return;
            Color c = toggled ? accent : baseColor;
            Color emission = toggled ? accent * 0.6f : Color.black;
            if (hovered)
            {
                c = Color.Lerp(c, Color.white, 0.2f);
                emission += accent * 0.35f;
            }
            material.SetColor(BaseColorId, c);
            material.color = c;
            material.SetColor(EmissionId, emission);

            float press = pressTimer > 0f ? 0.008f : 0f;
            cap.localPosition = new Vector3(0f, cap.localScale.y * 0.5f - press, 0f);
            label.text = toggled ? toggledText : text;
            label.color = toggled ? new Color(0.08f, 0.08f, 0.1f) : Color.white;   // stay readable on bright caps
        }

        public void OnHoverEnter()
        {
            hovered = true;
            Refresh();
        }

        public void OnHoverExit()
        {
            hovered = false;
            Refresh();
        }

        public void OnSelect()
        {
            pressTimer = 0.15f;
            Refresh();
            if (UIManager.Instance != null) UIManager.Instance.OnButton(Action);
        }
    }
}
