using UnityEngine;

/// <summary>
/// Runtime parent used to apply world-axis scale while keeping the child's rotation.
/// </summary>
public class ViewScaleRoot : MonoBehaviour
{
    public static Transform GetRootOrSelf(Transform target)
    {
        if (target == null)
        {
            return null;
        }

        ViewScaleRoot root = target.GetComponentInParent<ViewScaleRoot>();
        return root != null ? root.transform : target;
    }
}
