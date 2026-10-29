using UnityEngine;
using UnityEngine.XR;

namespace CodeScape
{
    // Writes one short log line every couple of seconds that compares the head pose reported by the Meta camera
    // rig with the view the camera is actually rendering. If the two move together when the user turns their
    // head, head tracking reaches the screen; if the rendered view stays put, the rendering path is the problem.
    // Read it on a device with:  adb logcat -s Unity | grep "\[CodeScape\] Tracking"
    public class TrackingDiagnostics : MonoBehaviour
    {
        [SerializeField] float intervalSeconds = 2f;
        [Tooltip("Stop logging this long after start")]
        [SerializeField] float durationSeconds = 600f;

        OVRCameraRig rig;
        float startTime;
        float nextLog;

        void Start()
        {
            startTime = Time.time;
        }

        void Update()
        {
            if (Time.time - startTime > durationSeconds || Time.time < nextLog) return;
            nextLog = Time.time + intervalSeconds;

            var cam = Camera.main;
            if (cam == null)
            {
                Debug.Log("[CodeScape] Tracking: no main camera");
                return;
            }
            if (rig == null) rig = FindFirstObjectByType<OVRCameraRig>();

            string head = rig != null && rig.centerEyeAnchor != null ? Describe(rig.centerEyeAnchor.forward) : "n/a";
            string rendered = "n/a (not stereo)";
            if (cam.stereoEnabled)
            {
                // The eye view matrix is what is really sent to the headset; a camera looks down -Z in view space.
                Matrix4x4 eyeToWorld = cam.GetStereoViewMatrix(Camera.StereoscopicEye.Left).inverse;
                rendered = Describe(eyeToWorld.MultiplyVector(new Vector3(0f, 0f, -1f)));
            }

            Debug.Log($"[CodeScape] Tracking: xr={XRSettings.isDeviceActive} device={XRSettings.loadedDeviceName} " +
                      $"stereo={cam.stereoEnabled} target={cam.stereoTargetEye} frame={Time.frameCount} | " +
                      $"head {head} | rendered view {rendered} | camera {Describe(cam.transform.forward)}");
        }

        static string Describe(Vector3 forward)
        {
            forward.Normalize();
            float yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            float pitch = Mathf.Asin(Mathf.Clamp(forward.y, -1f, 1f)) * Mathf.Rad2Deg;
            return $"yaw={yaw:0} pitch={pitch:0}";
        }
    }
}
