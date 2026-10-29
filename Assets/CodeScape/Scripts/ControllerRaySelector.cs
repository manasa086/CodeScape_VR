using UnityEngine;
using UnityEngine.XR;

namespace CodeScape
{
    // Quest controller interaction: point the laser at a node or console button and pull the index trigger
    // (or press A / X). Targets within reach can also be touched: touching + trigger selects, and pushing a
    // console button with the controller presses it.
    // Tracking comes from OVRCameraRig (Meta XR SDK); this script only does the raycast and selection.
    public class ControllerRaySelector : MonoBehaviour
    {
        [SerializeField] OVRInput.Controller controller = OVRInput.Controller.RTouch;
        [Tooltip("Leave empty to use the matching controller anchor of the OVRCameraRig")]
        [SerializeField] Transform rayOrigin;
        [Tooltip("Tilts the ray relative to the controller (degrees), if pointing feels off on the headset")]
        [SerializeField] Vector3 rayAngleOffset = Vector3.zero;
        [SerializeField] float maxDistance = 6f;
        [Tooltip("Radius of the fallback thick ray, so small or thin targets are easier to hit")]
        [SerializeField] float rayRadius = 0.015f;
        [Tooltip("Targets this close to the controller tip can be touched directly")]
        [SerializeField] float touchRadius = 0.05f;
        [Tooltip("How far the tip sits in front of the controller anchor")]
        [SerializeField] float tipOffset = 0.04f;
        [SerializeField] Material laserMaterial;

        LineRenderer laser;
        Transform dot;
        IRayTarget hover;
        bool selectHeld;
        PanelButton pokedButton;
        bool loggedState;

        static readonly Color IdleColor = new Color(1f, 1f, 1f, 0.35f);
        static readonly Color TargetColor = new Color(0.2f, 0.9f, 0.95f, 1f);
        static readonly Collider[] Overlaps = new Collider[16];

        void Start()
        {
            if (rayOrigin == null)
            {
                var rig = FindFirstObjectByType<OVRCameraRig>();
                if (rig != null)
                    rayOrigin = controller == OVRInput.Controller.LTouch ? rig.leftControllerAnchor : rig.rightControllerAnchor;
            }

            laser = gameObject.AddComponent<LineRenderer>();
            laser.sharedMaterial = laserMaterial != null ? laserMaterial : new Material(Shader.Find("Sprites/Default"));
            laser.positionCount = 2;
            laser.startWidth = 0.004f;
            laser.endWidth = 0.002f;
            laser.useWorldSpace = true;
            laser.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var d = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            d.name = "RayDot";
            Destroy(d.GetComponent<Collider>());
            d.transform.localScale = Vector3.one * 0.012f;
            d.GetComponent<Renderer>().sharedMaterial = laser.sharedMaterial;
            dot = d.transform;
            dot.SetParent(transform, false);

            SetVisible(false);
        }

        void Update()
        {
            bool connected = OVRInput.IsControllerConnected(controller);
            if (!loggedState && XRSettings.isDeviceActive)
            {
                loggedState = true;
                Debug.Log($"[CodeScape] {controller} selector: origin={(rayOrigin != null ? rayOrigin.name : "none")}, connected={connected}");
            }

            bool usable = XRSettings.isDeviceActive && rayOrigin != null && connected;
            if (!usable)
            {
                SetVisible(false);
                RayTargetUtil.UpdateHover(ref hover, null);
                pokedButton = null;
                return;
            }

            Vector3 origin = rayOrigin.position;
            Vector3 direction = rayOrigin.rotation * Quaternion.Euler(rayAngleOffset) * Vector3.forward;
            Vector3 tip = origin + direction * tipOffset;

            // 1) Touch: a target within reach of the controller tip wins over the ray.
            //    (A ray that starts inside a collider cannot hit it, so pressing a button by hand needs this.)
            string how = "ray";
            IRayTarget target = FindTouchTarget(tip, out Vector3 end, out Collider touched);
            bool hasEnd = target != null;
            if (target != null)
            {
                how = "touch";
            }
            else
            {
                // 2) Thin ray, then a slightly thicker ray for small targets.
                end = origin + direction * maxDistance;
                if (Physics.Raycast(origin, direction, out var info, maxDistance, ~0, QueryTriggerInteraction.Ignore))
                {
                    end = info.point;
                    hasEnd = true;
                    target = RayTargetUtil.Find(info.collider);
                }
                if (target == null &&
                    Physics.SphereCast(origin, rayRadius, direction, out var thick, maxDistance, ~0, QueryTriggerInteraction.Ignore))
                {
                    var thickTarget = RayTargetUtil.Find(thick.collider);
                    if (thickTarget != null && (!hasEnd || thick.distance <= Vector3.Distance(origin, end) + 0.05f))
                    {
                        target = thickTarget;
                        end = thick.point;
                        hasEnd = true;
                        how = "thick ray";
                    }
                }
            }

            if (!ReferenceEquals(target, hover) && target != null) Buzz(0.15f, 0.04f);   // tick when landing on a target
            RayTargetUtil.UpdateHover(ref hover, target);

            SetVisible(true);
            laser.SetPosition(0, origin);
            laser.SetPosition(1, end);
            var color = target != null ? TargetColor : IdleColor;
            laser.startColor = color;
            laser.endColor = color;
            dot.gameObject.SetActive(hasEnd);
            dot.position = end;

            // 3) Poke: pushing the controller into a console button presses it once.
            var poke = touched != null && IsInside(touched, tip) ? target as PanelButton : null;
            if (poke != null && poke != pokedButton)
            {
                Debug.Log($"[CodeScape] {controller} poked {poke.name}");
                poke.OnSelect();
                Buzz(0.6f, 0.08f);
            }
            pokedButton = poke;

            // 4) Select: index trigger (button event or analog value) or the A / X button.
            float trigger = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, controller);
            bool triggerButton = OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, controller);
            bool faceButton = OVRInput.Get(OVRInput.Button.One, controller);
            bool selectNow = trigger > 0.55f || triggerButton || faceButton;
            bool selectDown = selectNow && !selectHeld;
            selectHeld = selectNow;

            if (selectDown)
            {
                string input = triggerButton || trigger > 0.55f ? "index trigger" : "A/X button";
                Debug.Log($"[CodeScape] {controller} select: target={(target is Component c ? c.name : "none")} " +
                          $"via {how}, input={input}, trigger={trigger:0.00}");
                if (target != null && !ReferenceEquals(target, poke))
                {
                    target.OnSelect();
                    Buzz(0.5f, 0.08f);
                }
            }
        }

        IRayTarget FindTouchTarget(Vector3 tip, out Vector3 point, out Collider hitCollider)
        {
            point = tip;
            hitCollider = null;
            IRayTarget best = null;
            float bestDistance = float.MaxValue;
            int count = Physics.OverlapSphereNonAlloc(tip, touchRadius, Overlaps, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var t = RayTargetUtil.Find(Overlaps[i]);
                if (t == null) continue;
                var closest = Overlaps[i].ClosestPoint(tip);
                float distance = Vector3.Distance(closest, tip);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = t;
                    point = closest;
                    hitCollider = Overlaps[i];
                }
            }
            return best;
        }

        static bool IsInside(Collider c, Vector3 point)
        {
            return (c.ClosestPoint(point) - point).sqrMagnitude < 1e-6f;
        }

        void SetVisible(bool visible)
        {
            if (laser != null) laser.enabled = visible;
            if (dot != null) dot.gameObject.SetActive(visible);
        }

        void Buzz(float amplitude, float seconds)
        {
            OVRInput.SetControllerVibration(0.5f, amplitude, controller);
            CancelInvoke(nameof(StopBuzz));
            Invoke(nameof(StopBuzz), seconds);
        }

        void StopBuzz()
        {
            OVRInput.SetControllerVibration(0f, 0f, controller);
        }
    }
}
