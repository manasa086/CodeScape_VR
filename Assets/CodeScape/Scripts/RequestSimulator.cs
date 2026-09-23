using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CodeScape
{
    // Moves a glowing pulse through the nodes of a predefined request path.
    public class RequestSimulator : MonoBehaviour
    {
        public static RequestSimulator Instance { get; private set; }

        [SerializeField] string requestPathId = "createOrder";
        [Tooltip("Pulse travel speed in meters per second")]
        [SerializeField] float speed = 0.35f;
        [Tooltip("Pause at each node, in seconds")]
        [SerializeField] float dwell = 0.9f;

        public bool IsRunning => routine != null;

        Transform pulse;
        Material pulseMaterial;
        Light pulseLight;
        TrailRenderer trail;
        Coroutine routine;
        EdgeRenderer activeEdge;
        readonly List<NodeView> failedNodes = new List<NodeView>();

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            BuildPulse();
        }

        void BuildPulse()
        {
            var arch = ArchitectureManager.Instance;
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "RequestPulse";
            Destroy(go.GetComponent<Collider>());
            pulse = go.transform;
            pulse.SetParent(transform, false);
            pulse.localScale = Vector3.one * 0.035f;

            var glow = arch != null ? arch.GlowMaterial : null;
            pulseMaterial = glow != null ? new Material(glow) : new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            go.GetComponent<Renderer>().sharedMaterial = pulseMaterial;

            pulseLight = go.AddComponent<Light>();
            pulseLight.type = LightType.Point;
            pulseLight.range = 0.3f;
            pulseLight.intensity = 1.5f;

            trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.35f;
            trail.startWidth = 0.022f;
            trail.endWidth = 0f;
            trail.minVertexDistance = 0.005f;
            var lineMat = arch != null ? arch.LineMaterial : null;
            trail.sharedMaterial = lineMat != null ? lineMat : new Material(Shader.Find("Sprites/Default"));
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            SetPulseColor(NodeStyle.Pulse);
            go.SetActive(false);
        }

        void SetPulseColor(Color c)
        {
            pulseMaterial.SetColor("_BaseColor", c);
            pulseMaterial.color = c;
            pulseLight.color = c;
            trail.startColor = c;
            trail.endColor = new Color(c.r, c.g, c.b, 0f);
        }

        public void StartTrace(bool simulateFailure)
        {
            var arch = ArchitectureManager.Instance;
            var path = arch != null ? arch.GetRequestPath(requestPathId) : null;
            if (path == null || path.steps == null || path.steps.Length == 0)
            {
                UIManager.Instance?.SetStatus("No request path defined in architecture.json.");
                return;
            }

            StopTrace();
            // Show the whole architecture while the request travels through it.
            SelectionManager.Instance?.SetHighlight(false);
            routine = StartCoroutine(Run(path, simulateFailure));
        }

        public void StopTrace()
        {
            if (routine != null) StopCoroutine(routine);
            routine = null;
            SetActiveEdge(null, Color.clear);
            foreach (var n in failedNodes) n.SetFailed(false);
            failedNodes.Clear();
            if (pulse != null) pulse.gameObject.SetActive(false);
        }

        IEnumerator Run(RequestPath path, bool simulateFailure)
        {
            var arch = ArchitectureManager.Instance;
            var steps = path.steps;
            var callStack = new List<NodeView>();   // used to send an error back the way it came
            NodeView previous = null;
            string label = simulateFailure ? $"{path.name} (payment failure)" : path.name;

            SetPulseColor(NodeStyle.Pulse);

            for (int i = 0; i < steps.Length; i++)
            {
                var node = arch.GetNode(steps[i].node);
                if (node == null) continue;

                if (previous == null)
                {
                    pulse.position = node.TopPoint;
                    trail.Clear();
                    pulse.gameObject.SetActive(true);
                }
                else
                {
                    yield return Travel(previous, node, NodeStyle.Pulse);
                }

                if (callStack.Count >= 2 && callStack[callStack.Count - 2] == node)
                    callStack.RemoveAt(callStack.Count - 1);   // a response returning to its caller
                else
                    callStack.Add(node);

                node.Flash(NodeStyle.Pulse);
                AudioFeedback.Blip();
                UIManager.Instance?.SetStatus($"<color=#34E4EA><b>{label}</b></color>  step {i + 1}/{steps.Length}: {steps[i].message}");
                previous = node;

                if (simulateFailure && steps[i].fail)
                {
                    yield return new WaitForSeconds(dwell * 0.5f);
                    yield return ReturnError(node, callStack);
                    routine = null;
                    yield break;
                }

                yield return new WaitForSeconds(dwell);
            }

            AudioFeedback.Success();
            previous?.Flash(NodeStyle.Success);
            SetPulseColor(NodeStyle.Success);
            UIManager.Instance?.SetStatus($"<color=#7CFC8A><b>{path.name} complete:</b></color> order created and confirmed (201 Created). Press Trace to run it again.");
            yield return new WaitForSeconds(1.5f);
            pulse.gameObject.SetActive(false);
            routine = null;
        }

        IEnumerator ReturnError(NodeView failedAt, List<NodeView> callStack)
        {
            failedAt.SetFailed(true);
            failedNodes.Add(failedAt);
            AudioFeedback.Error();
            SetPulseColor(NodeStyle.Failure);
            UIManager.Instance?.SetStatus($"<color=#FF3B30><b>{failedAt.Data.name} declined the payment!</b></color> An error travels back to the user.");
            yield return new WaitForSeconds(dwell);

            var current = failedAt;
            for (int i = callStack.Count - 2; i >= 0; i--)
            {
                var next = callStack[i];
                yield return Travel(current, next, NodeStyle.Failure);
                next.Flash(NodeStyle.Failure);
                AudioFeedback.Blip();
                UIManager.Instance?.SetStatus($"<color=#FF3B30>Error 402 Payment Required</color> returned to <b>{next.Data.name}</b>");
                current = next;
                yield return new WaitForSeconds(dwell * 0.6f);
            }

            AudioFeedback.Error();
            UIManager.Instance?.SetStatus("<color=#FF3B30><b>Order failed:</b></color> payment declined, no order was saved. Press Reset or run the trace again.");
            yield return new WaitForSeconds(1.5f);
            pulse.gameObject.SetActive(false);
        }

        IEnumerator Travel(NodeView from, NodeView to, Color color)
        {
            SetActiveEdge(ArchitectureManager.Instance.FindEdge(from, to), color);

            Vector3 a = from.TopPoint;
            Vector3 b = to.TopPoint;
            float duration = Mathf.Max(0.2f, Vector3.Distance(a, b) / speed);
            for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
            {
                float s = Mathf.SmoothStep(0f, 1f, t);
                pulse.position = Vector3.Lerp(a, b, s) + Vector3.up * (Mathf.Sin(s * Mathf.PI) * 0.04f);
                yield return null;
            }
            pulse.position = b;

            SetActiveEdge(null, Color.clear);
        }

        void SetActiveEdge(EdgeRenderer edge, Color color)
        {
            if (activeEdge != null) activeEdge.SetActive(false, Color.clear);
            activeEdge = edge;
            if (activeEdge != null) activeEdge.SetActive(true, color);
        }
    }
}
