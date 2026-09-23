using System;

namespace CodeScape
{
    // Plain data classes that mirror Resources/architecture.json (read with JsonUtility).

    [Serializable]
    public class ArchitectureData
    {
        public string title;
        public NodeData[] nodes;
        public EdgeData[] edges;
        public RequestPath[] requestPaths;
    }

    [Serializable]
    public class NodeData
    {
        public string id;
        public string name;
        public string type;        // frontend | api | service | database | external
        public string language;
        public string description;
        public string[] endpoints;
        public float x;            // table-local grid position (see ArchitectureManager.gridSpacing)
        public float z;
    }

    [Serializable]
    public class EdgeData
    {
        public string from;        // caller
        public string to;          // dependency
    }

    [Serializable]
    public class RequestPath
    {
        public string id;
        public string name;
        public RequestStep[] steps;
    }

    [Serializable]
    public class RequestStep
    {
        public string node;
        public string message;
        public bool fail;          // the request fails here when "Simulate Failure" is used
    }
}
