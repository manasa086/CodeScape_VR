using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace CodeScape
{
    // Builds and updates the world-space UI: component info panel, status board and button console.
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Header("Placement (set by CodeScape > Build Scene)")]
        [SerializeField] Transform infoPanelAnchor;
        [SerializeField] Transform statusBoardAnchor;
        [SerializeField] Transform consoleAnchor;

        [Header("Look")]
        [SerializeField] Material panelMaterial;
        [SerializeField] Material buttonMaterial;

        TMP_FontAsset font;
        TextMeshPro infoTitle;
        TextMeshPro infoBody;
        TextMeshPro statusText;
        PanelButton highlightButton;
        PanelButton followButton;
        bool followOn;
        readonly List<PanelButton> buttons = new List<PanelButton>();

        string IdleStatus => "Point at a component and pull the trigger to select it.   " +
                             $"<color=#9AA4B2>Follow Me is {(followOn ? "ON" : "OFF")} (B / Y button)</color>";

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            font = ArchitectureManager.Instance != null ? ArchitectureManager.Instance.Font : null;
            BuildInfoPanel();
            BuildStatusBoard();
            followOn = FollowController.Instance != null && FollowController.Instance.Following;
            BuildConsole();
            if (followButton != null) followButton.SetToggled(followOn);
            ShowNode(null);
            SetStatus(IdleStatus);
        }

        // ---------- construction ----------

        void BuildInfoPanel()
        {
            if (infoPanelAnchor == null) return;
            var size = new Vector2(0.62f, 0.56f);
            CreateBackground(infoPanelAnchor, size, new Color(0.06f, 0.07f, 0.1f, 0.92f));

            infoTitle = CreateText(infoPanelAnchor, "Title", new Vector3(0f, 0.22f, 0f), new Vector2(0.56f, 0.08f), 0.5f,
                                   TextAlignmentOptions.Left);
            infoBody = CreateText(infoPanelAnchor, "Body", new Vector3(0f, -0.04f, 0f), new Vector2(0.56f, 0.42f), 0.26f,
                                  TextAlignmentOptions.TopLeft);
            infoBody.textWrappingMode = TextWrappingModes.Normal;
        }

        void BuildStatusBoard()
        {
            if (statusBoardAnchor == null) return;
            var size = new Vector2(1.5f, 0.38f);
            CreateBackground(statusBoardAnchor, size, new Color(0.05f, 0.06f, 0.09f, 0.9f));

            string title = ArchitectureManager.Instance?.Data?.title ?? "Architecture";
            CreateText(statusBoardAnchor, "Title", new Vector3(0f, 0.13f, 0f), new Vector2(1.42f, 0.09f), 0.7f,
                       TextAlignmentOptions.Center).text = $"<b>CodeScape VR</b>  <color=#9AA4B2>|  {title}</color>";

            statusText = CreateText(statusBoardAnchor, "Status", new Vector3(0f, 0.025f, 0f), new Vector2(1.42f, 0.1f), 0.45f,
                                    TextAlignmentOptions.Center);
            statusText.textWrappingMode = TextWrappingModes.Normal;

            string C(Color c) => NodeStyle.RichHex(c);
            CreateText(statusBoardAnchor, "Legend", new Vector3(0f, -0.1f, 0f), new Vector2(1.42f, 0.05f), 0.32f,
                       TextAlignmentOptions.Center).text =
                $"<color={C(NodeStyle.Frontend)}>Frontend (panel)</color>    " +
                $"<color={C(NodeStyle.Api)}>API Gateway (diamond)</color>    " +
                $"<color={C(NodeStyle.Service)}>Service (cube)</color>    " +
                $"<color={C(NodeStyle.Database)}>Database (cylinder)</color>    " +
                $"<color={C(NodeStyle.External)}>External (sphere)</color>";

            CreateText(statusBoardAnchor, "Help", new Vector3(0f, -0.155f, 0f), new Vector2(1.42f, 0.04f), 0.24f,
                       TextAlignmentOptions.Center).text =
                "<color=#8A93A0>Quest: point + trigger to select   |   Desktop: left-click select, right-drag look, keys H / T / F / R</color>";
        }

        void BuildConsole()
        {
            if (consoleAnchor == null) return;
            var arch = ArchitectureManager.Instance;
            var path = arch != null ? arch.GetRequestPath("createOrder") : null;
            string traceName = path != null ? path.name : "Request";

            float spacing = 0.21f;
            var defs = new (ButtonAction action, string text, string toggled, Color accent)[]
            {
                (ButtonAction.HighlightDependencies, "Highlight\nDependencies", "Highlight: ON\n(press to clear)", NodeStyle.Database),
                (ButtonAction.TraceRequest, $"Trace\n{traceName}", null, NodeStyle.Pulse),
                (ButtonAction.SimulateFailure, "Simulate\nPayment Failure", null, NodeStyle.Failure),
                (ButtonAction.Reset, "Reset\nScene", null, new Color(0.75f, 0.78f, 0.85f)),
                (ButtonAction.ToggleFollow, "Follow Me\nOFF", "Follow Me\nON", NodeStyle.Success),
            };

            for (int i = 0; i < defs.Length; i++)
            {
                float x = (i - (defs.Length - 1) * 0.5f) * spacing;
                var b = PanelButton.Create(consoleAnchor, defs[i].action, defs[i].text, defs[i].toggled, defs[i].accent,
                                           new Vector3(x, 0f, 0f), buttonMaterial, font);
                buttons.Add(b);
                if (defs[i].action == ButtonAction.HighlightDependencies) highlightButton = b;
                if (defs[i].action == ButtonAction.ToggleFollow) followButton = b;
            }
        }

        void CreateBackground(Transform parent, Vector2 size, Color color)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Background";
            Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(parent, false);
            quad.transform.localPosition = new Vector3(0f, 0f, 0.003f);
            quad.transform.localScale = new Vector3(size.x, size.y, 1f);
            var mat = panelMaterial != null ? new Material(panelMaterial) : new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.SetColor("_BaseColor", color);
            mat.color = color;
            quad.GetComponent<Renderer>().sharedMaterial = mat;
        }

        TextMeshPro CreateText(Transform parent, string name, Vector3 localPos, Vector2 size, float maxFont,
                               TextAlignmentOptions align)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var tmp = go.AddComponent<TextMeshPro>();
            if (font != null) tmp.font = font;
            tmp.rectTransform.sizeDelta = size;
            tmp.alignment = align;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 0.05f;
            tmp.fontSizeMax = maxFont;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.color = Color.white;
            return tmp;
        }

        // ---------- updates ----------

        public void ShowNode(NodeView node)
        {
            if (infoTitle == null) return;

            if (node == null)
            {
                infoTitle.text = "<color=#9AA4B2>Component Info</color>";
                infoBody.text = "<color=#C8CDD5>Point the controller at a component on the table and pull the trigger.\n\n" +
                                "Then use the buttons in front of you to highlight its dependencies or trace a Create Order request.</color>";
                return;
            }

            var d = node.Data;
            var arch = ArchitectureManager.Instance;
            string color = NodeStyle.RichHex(node.BaseColor);
            infoTitle.text = $"<color={color}><b>{d.name}</b></color>";

            var sb = new StringBuilder();
            sb.Append($"<color={color}>{NodeStyle.DisplayType(d.type)}</color>   <color=#9AA4B2>{d.language}</color>\n\n");
            sb.Append($"<b>Purpose</b>\n{d.description}\n\n");
            if (d.endpoints != null && d.endpoints.Length > 0)
                sb.Append($"<b>Endpoints</b>\n<color=#C8CDD5>{string.Join("\n", d.endpoints)}</color>\n\n");
            if (arch != null)
            {
                sb.Append($"<b>Depends on:</b> {SelectionManager.Names(arch.DependenciesOf(node))}\n");
                sb.Append($"<b>Used by:</b> {SelectionManager.Names(arch.CallersOf(node))}");
            }
            infoBody.text = sb.ToString();
        }

        public void SetStatus(string message)
        {
            if (statusText != null) statusText.text = message;
        }

        public void SetFollowToggled(bool on)
        {
            followOn = on;
            if (followButton != null) followButton.SetToggled(on);
        }

        public void SetHighlightToggled(bool on)
        {
            if (highlightButton != null) highlightButton.SetToggled(on);
        }

        public void OnButton(ButtonAction action)
        {
            Debug.Log($"[CodeScape] Button pressed: {action}");
            switch (action)
            {
                case ButtonAction.HighlightDependencies:
                    SelectionManager.Instance?.ToggleHighlight();
                    break;
                case ButtonAction.TraceRequest:
                    RequestSimulator.Instance?.StartTrace(false);
                    break;
                case ButtonAction.SimulateFailure:
                    RequestSimulator.Instance?.StartTrace(true);
                    break;
                case ButtonAction.Reset:
                    ResetScene();
                    break;
                case ButtonAction.ToggleFollow:
                    FollowController.Instance?.Toggle();
                    break;
            }
        }

        public void ResetScene()
        {
            RequestSimulator.Instance?.StopTrace();
            SelectionManager.Instance?.ClearSelection();
            AudioFeedback.Click();
            SetStatus("Scene reset. " + IdleStatus);
        }
    }
}
