using UnityEngine;
using UnityEngine.XR;

namespace CodeScape
{
    // Quest controller laser: point at a node or console button and pull the index trigger to select it.
    // Tracking comes from OVRCameraRig (Meta XR SDK); this script only does the raycast and selection.
    public class ControllerRaySelector : MonoBehaviour
    {
        [SerializeField] OVRInput.Controller controller = OVRInput.Controller.RTouch;
        [Tooltip("Leave empty to use the matching controller anchor of the OVRCameraRig")]
        [SerializeField] Transform rayOrigin;
        [Tooltip("Tilts the ray relative to the controller (degrees), if pointing feels off on the headset")]
        [SerializeField] Vector3 rayAngleOffset = Vector3.zero;
        [SerializeField] float maxDistance = 6f;
        [SerializeField] Material laserMaterial;

        LineRenderer laser;
        Transform dot;
        IRayTarget hover;

        static readonly Color IdleColor = new Color(1f, 1f, 1f, 0.35f);
        static readonly Color TargetColor = new Color(0.2f, 0.9f, 0.95f, 1f);

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
            bool usable = XRSettings.isDeviceActive && rayOrigin != null && OVRInput.IsControllerConnected(controller);
            if (!usable)
            {
                SetVisible(false);
                RayTargetUtil.UpdateHover(ref hover, null);
                return;
            }

            var ray = new Ray(rayOrigin.position, rayOrigin.rotation * Quaternion.Euler(rayAngleOffset) * Vector3.forward);
            IRayTarget target = null;
            Vector3 end = ray.origin + ray.direction * maxDistance;
            bool hit = Physics.Raycast(ray, out var info, maxDistance, ~0, QueryTriggerInteraction.Ignore);
            if (hit)
            {
                end = info.point;
                target = RayTargetUtil.Find(info.collider);
            }

            RayTargetUtil.UpdateHover(ref hover, target);

            SetVisible(true);
            laser.SetPosition(0, ray.origin);
            laser.SetPosition(1, end);
            var color = target != null ? TargetColor : IdleColor;
            laser.startColor = color;
            laser.endColor = color;
            dot.gameObject.SetActive(hit);
            dot.position = end;

            if (target != null && OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, controller))
            {
                target.OnSelect();
                Buzz();
            }
        }

        void SetVisible(bool visible)
        {
            if (laser != null) laser.enabled = visible;
            if (dot != null) dot.gameObject.SetActive(visible);
        }

        void Buzz()
        {
            OVRInput.SetControllerVibration(0.5f, 0.35f, controller);
            CancelInvoke(nameof(StopBuzz));
            Invoke(nameof(StopBuzz), 0.08f);
        }

        void StopBuzz()
        {
            OVRInput.SetControllerVibration(0f, 0f, controller);
        }
    }
}
