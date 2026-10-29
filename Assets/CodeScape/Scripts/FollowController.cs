using UnityEngine;

namespace CodeScape
{
    // "Follow Me": the application moves with the user's head, like a floating window.
    //
    // - Left / right: when the user turns their head further than a dead zone away from the table, the content
    //   (table, architecture, console and panels) glides around them until it is in front of them again.
    // - Up / down: the status board and info panel rise and sink when the user looks up or down, so they stay
    //   in view. The table itself stays at table height.
    // - Small head movements inside the dead zones change nothing, so selecting things stays steady.
    //
    // Press B / Y on a controller, or the "Follow Me" console button, to turn it off and back on.
    public class FollowController : MonoBehaviour
    {
        public static FollowController Instance { get; private set; }

        [Tooltip("Everything that follows the user: table, architecture, console and panels")]
        [SerializeField] Transform contentRoot;
        [Tooltip("Status board and info panel (a child of the content) that also follow up and down")]
        [SerializeField] Transform panelsRoot;
        [SerializeField] bool following = true;

        [Header("Left / right")]
        [Tooltip("How far (degrees) the user can turn away before the content follows")]
        [SerializeField] float turnDeadZone = 25f;
        [SerializeField] float turnSeconds = 0.8f;
        [SerializeField] float maxTurnSpeed = 120f;

        [Header("Up / down (panels)")]
        [SerializeField] float pitchDeadZone = 15f;
        [Tooltip("Distance from the user to the panels, used to turn a look angle into a height change")]
        [SerializeField] float panelDistance = 2f;
        [Tooltip("Lowest and highest the panels may move from their normal height (meters)")]
        [SerializeField] Vector2 panelLiftRange = new Vector2(-0.2f, 0.7f);
        [SerializeField] float panelSeconds = 0.6f;

        public bool Following => following;

        // Where the user's head stands inside the content, as set by ViewpointController (floor x, z).
        Vector3 ViewerSpot
        {
            get
            {
                Vector2 v = viewpoint != null ? viewpoint.ViewPosition : new Vector2(0f, -0.45f);
                return new Vector3(v.x, 0f, v.y);
            }
        }

        OVRCameraRig cameraRig;
        Transform head;
        ViewpointController viewpoint;
        float heading;            // yaw of the content, degrees
        float headingVelocity;
        bool turning;
        Vector3 contentVelocity;
        float panelLift;
        float panelLiftVelocity;
        Vector3 panelsBaseLocal;

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            if (panelsRoot != null) panelsBaseLocal = panelsRoot.localPosition;
            viewpoint = FindFirstObjectByType<ViewpointController>();
            UIManager.Instance?.SetFollowToggled(following);
        }

        void Update()
        {
            if (!HeadsetState.IsActive()) return;
            if (head == null && !FindHead()) return;
            // Wait for the start placement: until then the head can face any way, and following it would make the
            // content swing round for a moment at launch.
            if (viewpoint == null) viewpoint = FindFirstObjectByType<ViewpointController>();
            if (viewpoint != null && !viewpoint.HasPlaced) return;

            if (OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.RTouch) ||
                OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.LTouch))
                Toggle();

            if (following) Step(Time.deltaTime);
        }

        bool FindHead()
        {
            cameraRig = FindFirstObjectByType<OVRCameraRig>();
            if (cameraRig == null || cameraRig.centerEyeAnchor == null) return false;
            head = cameraRig.centerEyeAnchor;
            return true;
        }

        public void Toggle()
        {
            following = !following;
            turning = false;
            AudioFeedback.Click();
            UIManager.Instance?.SetFollowToggled(following);
            UIManager.Instance?.SetStatus(following
                ? "<b>Follow Me is ON:</b> the table and panels move with your head. Press B / Y to turn it off."
                : "<b>Follow Me is OFF:</b> the table stays where it is. Press B / Y to turn it on.");
            Debug.Log($"[CodeScape] Follow Me {(following ? "ON" : "OFF")}");
        }

        // The user was placed in front of the table (start or recenter): put the content back at its start spot.
        public void ResetPlacement()
        {
            heading = 0f;
            headingVelocity = 0f;
            turning = false;
            contentVelocity = Vector3.zero;
            panelLift = 0f;
            panelLiftVelocity = 0f;
            if (contentRoot != null) contentRoot.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            if (panelsRoot != null) panelsRoot.localPosition = panelsBaseLocal;
        }

        // One follow update. Kept separate from Update so it can be tested with made-up head poses.
        void Step(float dt)
        {
            if (contentRoot == null || head == null) return;

            // Left / right: turn the content around the user once they look far enough away from it.
            Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude > 0.1f)   // not looking straight up or down
            {
                float headHeading = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
                if (!turning && Mathf.Abs(Mathf.DeltaAngle(heading, headHeading)) > turnDeadZone)
                {
                    turning = true;
                    Debug.Log($"[CodeScape] Follow: turning content from {heading:0} to {headHeading:0}");
                }

                if (turning)
                {
                    heading = Mathf.SmoothDampAngle(heading, headHeading, ref headingVelocity, turnSeconds, maxTurnSpeed, dt);
                    Quaternion rotation = Quaternion.Euler(0f, heading, 0f);
                    // Keep the content at the same distance in front of the user, wherever they now stand.
                    Vector3 headOnFloor = new Vector3(head.position.x, 0f, head.position.z);
                    Vector3 target = headOnFloor - rotation * ViewerSpot;
                    Vector3 position = Vector3.SmoothDamp(contentRoot.position, target, ref contentVelocity, turnSeconds, Mathf.Infinity, dt);
                    contentRoot.SetPositionAndRotation(position, rotation);

                    if (Mathf.Abs(Mathf.DeltaAngle(heading, headHeading)) < 2f && (position - target).sqrMagnitude < 0.0004f)
                    {
                        turning = false;
                        headingVelocity = 0f;
                        contentVelocity = Vector3.zero;
                    }
                }
            }

            // Up / down: raise or lower the panels when the user looks well above or below the straight-ahead line.
            if (panelsRoot != null)
            {
                float pitch = Mathf.Asin(Mathf.Clamp(head.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
                float excess = Mathf.Max(0f, Mathf.Abs(pitch) - pitchDeadZone) * Mathf.Sign(pitch);
                float targetLift = Mathf.Clamp(panelDistance * Mathf.Tan(excess * Mathf.Deg2Rad), panelLiftRange.x, panelLiftRange.y);
                panelLift = Mathf.SmoothDamp(panelLift, targetLift, ref panelLiftVelocity, panelSeconds, Mathf.Infinity, dt);
                panelsRoot.localPosition = panelsBaseLocal + Vector3.up * panelLift;
            }
        }
    }
}
