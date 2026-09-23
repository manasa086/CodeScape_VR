using System.Collections.Generic;
using UnityEngine;

namespace CodeScape
{
    // Tracks the selected node and applies dependency highlighting.
    public class SelectionManager : MonoBehaviour
    {
        public static SelectionManager Instance { get; private set; }

        public NodeView Selected { get; private set; }
        public bool HighlightOn { get; private set; }

        void Awake()
        {
            Instance = this;
        }

        public void Select(NodeView node)
        {
            if (node == Selected) return;
            Selected = node;
            AudioFeedback.Click();
            Apply();
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowNode(node);
                if (node != null)
                    UIManager.Instance.SetStatus($"Selected <b>{node.Data.name}</b>. Press Highlight Dependencies to isolate what it uses.");
            }
        }

        public void ToggleHighlight()
        {
            if (Selected == null)
            {
                AudioFeedback.Error();
                UIManager.Instance?.SetStatus("Select a component first: point at it and pull the trigger.");
                return;
            }

            HighlightOn = !HighlightOn;
            AudioFeedback.Click();
            Apply();

            if (UIManager.Instance != null)
            {
                var deps = ArchitectureManager.Instance.DependenciesOf(Selected);
                UIManager.Instance.SetStatus(HighlightOn
                    ? $"<b>{Selected.Data.name}</b> depends on {deps.Count} component(s): {Names(deps)}"
                    : "Dependency highlight off.");
            }
        }

        // Turns highlighting on/off without sounds or status messages (used when a trace starts).
        public void SetHighlight(bool on)
        {
            on = on && Selected != null;
            if (HighlightOn == on) return;
            HighlightOn = on;
            Apply();
        }

        public void ClearSelection()
        {
            Selected = null;
            HighlightOn = false;
            Apply();
            UIManager.Instance?.ShowNode(null);
        }

        void Apply()
        {
            var arch = ArchitectureManager.Instance;
            if (arch == null) return;

            var deps = new HashSet<NodeView>();
            if (Selected != null) deps.UnionWith(arch.DependenciesOf(Selected));

            foreach (var node in arch.Nodes)
            {
                NodeState state;
                if (node == Selected) state = NodeState.Selected;
                else if (!HighlightOn) state = NodeState.Normal;
                else state = deps.Contains(node) ? NodeState.Dependency : NodeState.Faded;
                node.SetState(state);
            }

            foreach (var edge in arch.Edges)
            {
                EdgeState state;
                bool outgoing = Selected != null && edge.From == Selected;
                if (HighlightOn) state = outgoing ? EdgeState.Highlighted : EdgeState.Faded;
                else if (Selected != null && (outgoing || edge.To == Selected)) state = EdgeState.Related;
                else state = EdgeState.Normal;
                edge.SetState(state);
            }

            UIManager.Instance?.SetHighlightToggled(HighlightOn);
        }

        public static string Names(List<NodeView> list)
        {
            if (list.Count == 0) return "none";
            var names = new List<string>();
            foreach (var n in list) names.Add(n.Data.name);
            return string.Join(", ", names);
        }
    }
}
