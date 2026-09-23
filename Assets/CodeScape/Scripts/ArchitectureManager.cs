using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace CodeScape
{
    // Reads the architecture data and creates the nodes and dependency lines on the table.
    [DefaultExecutionOrder(-100)]
    public class ArchitectureManager : MonoBehaviour
    {
        public static ArchitectureManager Instance { get; private set; }

        [Header("Data")]
        [Tooltip("Leave empty to load Resources/architecture.json")]
        [SerializeField] TextAsset architectureJson;

        [Header("Layout")]
        [Tooltip("Parent on the table surface. Node x/z grid values are placed relative to it.")]
        [SerializeField] Transform architectureRoot;
        [SerializeField] float gridSpacing = 0.26f;

        [Header("Look")]
        [SerializeField] Material nodeMaterial;
        [SerializeField] Material glowMaterial;
        [SerializeField] Material lineMaterial;
        [SerializeField] TMP_FontAsset font;

        public ArchitectureData Data { get; private set; }
        public IReadOnlyList<NodeView> Nodes => nodeList;
        public IReadOnlyList<EdgeRenderer> Edges => edges;
        public Material GlowMaterial => glowMaterial;
        public Material LineMaterial => lineMaterial;
        public TMP_FontAsset Font => font;

        readonly Dictionary<string, NodeView> nodes = new Dictionary<string, NodeView>();
        readonly List<NodeView> nodeList = new List<NodeView>();
        readonly List<EdgeRenderer> edges = new List<EdgeRenderer>();

        void Awake()
        {
            Instance = this;
            if (architectureRoot == null) architectureRoot = transform;
            Build();
        }

        void Build()
        {
            Data = ArchitectureLoader.Load(architectureJson);
            if (Data == null) return;

            var nodeParent = new GameObject("Nodes").transform;
            nodeParent.SetParent(architectureRoot, false);
            var edgeParent = new GameObject("Edges").transform;
            edgeParent.SetParent(architectureRoot, false);

            foreach (var nodeData in Data.nodes)
            {
                var go = new GameObject();
                go.transform.SetParent(nodeParent, false);
                go.transform.localPosition = new Vector3(nodeData.x * gridSpacing, 0f, nodeData.z * gridSpacing);
                var view = go.AddComponent<NodeView>();
                view.Init(nodeData, nodeMaterial, glowMaterial, font);
                nodes[nodeData.id] = view;
                nodeList.Add(view);
            }

            foreach (var edgeData in Data.edges)
            {
                var from = GetNode(edgeData.from);
                var to = GetNode(edgeData.to);
                if (from == null || to == null) continue;
                var go = new GameObject();
                go.transform.SetParent(edgeParent, false);
                var edge = go.AddComponent<EdgeRenderer>();
                edge.Init(from, to, lineMaterial);
                edges.Add(edge);
            }

            Debug.Log($"[CodeScape] Built {nodes.Count} nodes and {edges.Count} connections.");
        }

        public NodeView GetNode(string id)
        {
            return id != null && nodes.TryGetValue(id, out var view) ? view : null;
        }

        // Nodes that the given node calls (outgoing edges).
        public List<NodeView> DependenciesOf(NodeView node)
        {
            var result = new List<NodeView>();
            foreach (var e in edges)
                if (e.From == node) result.Add(e.To);
            return result;
        }

        // Nodes that call the given node (incoming edges).
        public List<NodeView> CallersOf(NodeView node)
        {
            var result = new List<NodeView>();
            foreach (var e in edges)
                if (e.To == node) result.Add(e.From);
            return result;
        }

        public EdgeRenderer FindEdge(NodeView a, NodeView b)
        {
            foreach (var e in edges)
                if (e.Connects(a, b)) return e;
            return null;
        }

        public RequestPath GetRequestPath(string id)
        {
            if (Data == null) return null;
            foreach (var p in Data.requestPaths)
                if (p.id == id) return p;
            return Data.requestPaths.Length > 0 ? Data.requestPaths[0] : null;
        }
    }
}
