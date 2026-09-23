using UnityEngine;

namespace CodeScape
{
    // Loads the architecture description from a local JSON file.
    public static class ArchitectureLoader
    {
        public const string DefaultResource = "architecture";

        public static ArchitectureData Load(TextAsset json)
        {
            if (json == null) json = Resources.Load<TextAsset>(DefaultResource);
            if (json == null)
            {
                Debug.LogError("[CodeScape] architecture.json not found in a Resources folder.");
                return null;
            }

            var data = JsonUtility.FromJson<ArchitectureData>(json.text);
            if (data == null || data.nodes == null || data.nodes.Length == 0)
            {
                Debug.LogError("[CodeScape] architecture.json has no nodes.");
                return null;
            }

            data.edges ??= new EdgeData[0];
            data.requestPaths ??= new RequestPath[0];
            Validate(data);
            return data;
        }

        static void Validate(ArchitectureData data)
        {
            var ids = new System.Collections.Generic.HashSet<string>();
            foreach (var n in data.nodes)
                if (!ids.Add(n.id)) Debug.LogWarning($"[CodeScape] Duplicate node id '{n.id}'.");

            foreach (var e in data.edges)
                if (!ids.Contains(e.from) || !ids.Contains(e.to))
                    Debug.LogWarning($"[CodeScape] Edge {e.from} -> {e.to} references an unknown node.");

            foreach (var p in data.requestPaths)
                foreach (var s in p.steps)
                    if (!ids.Contains(s.node))
                        Debug.LogWarning($"[CodeScape] Request path '{p.id}' uses unknown node '{s.node}'.");
        }
    }
}
