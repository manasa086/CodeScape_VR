using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace CodeScape
{
    // Lets the scene be tested in the Editor without a headset (for example on macOS, where Quest Link
    // is unavailable). Only active while no XR device is running.
    //   Left click  - select node / press console button
    //   Right drag  - look around          WASD / QE - move
    //   H - highlight   T - trace   F - trace with payment failure   R / Esc - reset
    public class DesktopInputFallback : MonoBehaviour
    {
        // Same spot as the headset start position (ViewpointController), so both views show the same picture.
        [SerializeField] Vector3 startPosition = new Vector3(0.05f, 1.6f, -0.55f);
        [SerializeField] float startPitch = 20f;
        [SerializeField] float startYaw = 4f;
        [SerializeField] float fieldOfView = 70f;
        [SerializeField] float lookSpeed = 0.15f;
        [SerializeField] float moveSpeed = 1f;
        [Tooltip("Seconds to wait for an XR device to start before switching to desktop controls")]
        [SerializeField] float xrStartupGrace = 1.5f;

        Camera cam;
        Transform rig;
        Vector3 rigStartPosition;
        Quaternion rigStartRotation;
        float camStartFieldOfView;
        float yaw;
        float pitch;
        bool placed;
        IRayTarget hover;

        void Awake()
        {
            // Desktop controls are for testing in the Editor only. On the headset the XR session can start a
            // few frames after the scene, and moving the camera rig in that gap would shift the user's view.
            if (Application.platform == RuntimePlatform.Android) enabled = false;
        }

        // A headset counts as active as soon as it is plugged in, even before its XR session has started, so the
        // desktop camera never takes over (and tilts) the rig that the headset is about to track.
        static bool HeadsetActive()
        {
            if (XRSettings.isDeviceActive) return true;
            try { return OVRManager.isHmdPresent; }
            catch { return false; }
        }

        void Update()
        {
            if (HeadsetActive())
            {
                RayTargetUtil.UpdateHover(ref hover, null);
                if (placed) RestoreRig();
                return;
            }

            if (Time.timeSinceLevelLoad < xrStartupGrace) return;

            if (cam == null)
            {
                cam = Camera.main;
                if (cam == null) return;
                // Move the whole rig so head-tracking code in the rig cannot undo it.
                rig = cam.transform.root;
            }

            if (!placed) PlaceCamera();

            var mouse = Mouse.current;
            var keyboard = Keyboard.current;

            if (mouse != null)
            {
                if (mouse.rightButton.isPressed)
                {
                    Vector2 delta = mouse.delta.ReadValue();
                    yaw += delta.x * lookSpeed;
                    pitch = Mathf.Clamp(pitch - delta.y * lookSpeed, -80f, 80f);
                }

                var ray = cam.ScreenPointToRay(mouse.position.ReadValue());
                IRayTarget target = null;
                if (Physics.Raycast(ray, out var hit, 20f, ~0, QueryTriggerInteraction.Ignore))
                    target = RayTargetUtil.Find(hit.collider);
                RayTargetUtil.UpdateHover(ref hover, target);

                if (mouse.leftButton.wasPressedThisFrame && target != null)
                    target.OnSelect();
            }

            if (keyboard != null)
            {
                Vector3 move = Vector3.zero;
                if (keyboard.wKey.isPressed) move += Vector3.forward;
                if (keyboard.sKey.isPressed) move += Vector3.back;
                if (keyboard.aKey.isPressed) move += Vector3.left;
                if (keyboard.dKey.isPressed) move += Vector3.right;
                if (keyboard.eKey.isPressed) move += Vector3.up;
                if (keyboard.qKey.isPressed) move += Vector3.down;
                rig.position += Quaternion.Euler(0f, yaw, 0f) * move * (moveSpeed * Time.deltaTime);

                var ui = UIManager.Instance;
                if (ui != null)
                {
                    if (keyboard.hKey.wasPressedThisFrame) ui.OnButton(ButtonAction.HighlightDependencies);
                    if (keyboard.tKey.wasPressedThisFrame) ui.OnButton(ButtonAction.TraceRequest);
                    if (keyboard.fKey.wasPressedThisFrame) ui.OnButton(ButtonAction.SimulateFailure);
                    if (keyboard.rKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame)
                        ui.OnButton(ButtonAction.Reset);
                }
            }

            rig.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        // An XR device started after desktop controls took over: give the headset back its own pose.
        void RestoreRig()
        {
            placed = false;
            if (rig != null)
            {
                rig.position = rigStartPosition;
                rig.rotation = rigStartRotation;
            }
            if (cam != null) cam.fieldOfView = camStartFieldOfView;
            Debug.Log("[CodeScape] XR device active: desktop controls turned off.");
        }

        void PlaceCamera()
        {
            placed = true;
            rigStartPosition = rig.position;
            rigStartRotation = rig.rotation;
            camStartFieldOfView = cam.fieldOfView;
            yaw = startYaw;
            pitch = startPitch;
            cam.fieldOfView = fieldOfView;
            // Offset so the camera itself (not the rig root) ends up at startPosition.
            rig.position = startPosition - (cam.transform.position - rig.position);
            rig.rotation = Quaternion.Euler(pitch, yaw, 0f);
            Debug.Log("[CodeScape] No XR device active: desktop mouse/keyboard controls enabled.");
        }
    }
}
