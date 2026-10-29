using UnityEngine;

namespace CodeScape
{
    // Puts the player at a comfortable spot in front of the table when the headset session starts, and again
    // after every recenter (hold the Meta button), so the whole architecture is in view whichever part of the
    // room the user happens to start in or face.
    //
    // Only the OVRCameraRig root is moved (position and heading). Head tracking itself is never touched, so the
    // view keeps following the head: turning, nodding and leaning change the view as in any VR app.
    public class ViewpointController : MonoBehaviour
    {
        [Tooltip("Where the head is placed on the floor (x, z). The table is in front, along +Z. " +
                 "A more negative z stands further back: more of the scene fits into view.")]
        [SerializeField] Vector2 viewPosition = new Vector2(0f, -0.45f);
        [Tooltip("Seconds to wait after the headset session starts, so tracking has settled")]
        [SerializeField] float settleSeconds = 0.6f;
        [Tooltip("Log the head and rig pose every few seconds for this long after start (for checking tracking)")]
        [SerializeField] float logPoseSeconds = 90f;

        OVRCameraRig cameraRig;
        Transform rig;
        Transform head;
        float authoredHeight;
        float xrActiveSince = -1f;
        float alignAt = -1f;
        float giveUpAt;
        float nextPoseLog;
        float startTime;
        bool subscribed;
        bool wasPlacedOnce;

        // Where the head is placed on the floor (x, z); FollowController keeps the content at this distance.
        public Vector2 ViewPosition => viewPosition;

        // True once the user has been placed in front of the table (so FollowController can start following).
        public bool HasPlaced => wasPlacedOnce;

        void Start()
        {
            startTime = Time.time;
        }

        void OnDestroy()
        {
            if (subscribed && OVRManager.display != null) OVRManager.display.RecenteredPose -= OnRecentered;
        }

        void Update()
        {
            if (rig == null && !FindRig()) return;
            if (!HeadsetState.IsActive())
            {
                xrActiveSince = -1f;
                return;
            }

            if (!subscribed && OVRManager.display != null)
            {
                OVRManager.display.RecenteredPose += OnRecentered;
                subscribed = true;
            }

            if (xrActiveSince < 0f)
            {
                xrActiveSince = Time.time;
                alignAt = Time.time + settleSeconds;
                giveUpAt = alignAt + 4f;
            }

            if (alignAt >= 0f && Time.time >= alignAt) TryAlign();
            LogPose();
        }

        bool FindRig()
        {
            cameraRig = FindFirstObjectByType<OVRCameraRig>();
            if (cameraRig == null || cameraRig.centerEyeAnchor == null) return false;
            rig = cameraRig.transform;
            head = cameraRig.centerEyeAnchor;
            authoredHeight = rig.position.y;   // the rig sits on the floor in the scene
            return true;
        }

        void OnRecentered()
        {
            // The headset just moved its tracking origin to where the user stands and looks.
            alignAt = Time.time + 0.3f;
            giveUpAt = alignAt + 3f;
        }

        void TryAlign()
        {
            // Wait until the head pose is real (about eye height above the floor), then align once.
            bool tracking = head.localPosition.sqrMagnitude > 0.04f;
            if (!tracking && Time.time < giveUpAt) return;

            // The rig may only be turned and slid across the floor. Undo any tilt or lift (nothing in this app
            // should add any), so head movement is always measured against an upright, floor-level frame.
            rig.rotation = Quaternion.Euler(0f, rig.eulerAngles.y, 0f);
            rig.position = new Vector3(rig.position.x, authoredHeight, rig.position.z);

            Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            bool facingKnown = forward.sqrMagnitude > 0.1f;   // not looking steeply up or down
            if (!facingKnown && Time.time < giveUpAt) return;

            if (facingKnown)
            {
                // Turn the rig around the head so the head faces the table (+Z).
                float headYaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
                rig.RotateAround(head.position, Vector3.up, -headYaw);
            }

            // Slide the rig so the head ends up at the chosen spot on the floor (height stays as tracked).
            Vector3 headPosition = head.position;
            rig.position += new Vector3(viewPosition.x - headPosition.x, 0f, viewPosition.y - headPosition.z);

            alignAt = -1f;
            wasPlacedOnce = true;
            FollowController.Instance?.ResetPlacement();   // the user now faces the table: content back at its start spot
            Debug.Log($"[CodeScape] Viewpoint placed: head=({head.position.x:0.00}, {head.position.y:0.00}, {head.position.z:0.00}) " +
                      $"rig=({rig.position.x:0.00}, {rig.position.y:0.00}, {rig.position.z:0.00}) tracking={tracking}");
        }

        void LogPose()
        {
            if (!wasPlacedOnce || Time.time - startTime > logPoseSeconds || Time.time < nextPoseLog) return;
            nextPoseLog = Time.time + 3f;
            Vector3 angles = head.rotation.eulerAngles;
            float pitch = angles.x > 180f ? angles.x - 360f : angles.x;
            Debug.Log($"[CodeScape] Head pose: pos=({head.position.x:0.00}, {head.position.y:0.00}, {head.position.z:0.00}) " +
                      $"yaw={angles.y:0} pitch={pitch:0} | rig pos=({rig.position.x:0.00}, {rig.position.y:0.00}, {rig.position.z:0.00}) " +
                      $"tilt={Vector3.Angle(rig.up, Vector3.up):0.0}");
        }
    }
}
