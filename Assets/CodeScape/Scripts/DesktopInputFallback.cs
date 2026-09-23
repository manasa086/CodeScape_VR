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
        [SerializeField] Vector3 startPosition = new Vector3(0.1f, 1.6f, -0.3f);
        [SerializeField] float startPitch = 30f;
        [SerializeField] float startYaw = 6f;
        [SerializeField] float fieldOfView = 60f;
        [SerializeField] float lookSpeed = 0.15f;
        [SerializeField] float moveSpeed = 1f;

        Camera cam;
        Transform rig;
        float yaw;
        float pitch;
        bool placed;
        IRayTarget hover;

        void Update()
        {
            if (XRSettings.isDeviceActive)
            {
                RayTargetUtil.UpdateHover(ref hover, null);
                return;
            }

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

        void PlaceCamera()
        {
            placed = true;
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
