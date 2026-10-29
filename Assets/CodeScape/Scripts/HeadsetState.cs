using UnityEngine.XR;

namespace CodeScape
{
    // One place that answers "is a VR headset running?", shared by the scripts that behave differently with
    // and without one. A headset counts as active as soon as it is plugged in, even before its XR session starts.
    public static class HeadsetState
    {
        public static bool IsActive()
        {
            if (XRSettings.isDeviceActive) return true;
            try { return OVRManager.isHmdPresent; }
            catch { return false; }
        }
    }
}
