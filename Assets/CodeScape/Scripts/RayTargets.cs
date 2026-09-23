using UnityEngine;

namespace CodeScape
{
    // Anything the controller ray (or the desktop mouse) can point at and select.
    public interface IRayTarget
    {
        void OnHoverEnter();
        void OnHoverExit();
        void OnSelect();
    }

    public static class RayTargetUtil
    {
        public static IRayTarget Find(Collider collider)
        {
            return collider != null ? collider.GetComponentInParent<IRayTarget>() : null;
        }

        // Sends hover enter/exit when the pointed-at target changes.
        public static void UpdateHover(ref IRayTarget current, IRayTarget next)
        {
            if (ReferenceEquals(current, next)) return;
            if (current is Object oldObj && oldObj != null) current.OnHoverExit();
            current = next;
            current?.OnHoverEnter();
        }
    }
}
