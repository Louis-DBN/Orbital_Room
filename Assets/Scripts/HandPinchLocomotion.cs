using UnityEngine;
using Oculus.Interaction.Input;
using Oculus.Platform;

//Script de base pour se déplacer avec l'hand tracking
//Regarder le script HandGuidedPlacement pour mieux comprendre comment utiliser l'hand tracking

public class HandPinchLocomotionBB : MonoBehaviour
{
    [Header("OVR Hand Reference (SampleScene default)")]
    public OVRHand ovrHand;

    [Header("ISDK Hand Reference (Optional)")]
    public HandRef rightHand;        // Assign RightHand (from Hand Interactions)

    [Header("Locomotion Settings")]
    public Transform handDirection;  // Assign a child that rotates with your hand (or defaults to hand)
    public float moveSpeed = 1.5f;
    public bool invert = false;      // Toggle if forward seems backwards

    void Start()
    {
        if (ovrHand == null && rightHand == null)
        {
            OVRHand[] hands = FindObjectsByType<OVRHand>(FindObjectsSortMode.None);
            foreach (var h in hands)
            {
                var skeletonType = ((OVRSkeleton.IOVRSkeletonDataProvider)h).GetSkeletonType();
                if (skeletonType == OVRSkeleton.SkeletonType.HandRight || skeletonType == OVRSkeleton.SkeletonType.XRHandRight)
                {
                    ovrHand = h;
                    break;
                }
                if (ovrHand == null) ovrHand = h;
            }
        }

        if (handDirection == null)
        {
            if (ovrHand != null) handDirection = ovrHand.transform;
            else if (rightHand != null) handDirection = rightHand.transform;
            else handDirection = transform;
        }
    }

    void Update()
    {
        bool isPinching = false;
        if (ovrHand != null)
        {
            isPinching = ovrHand.GetFingerIsPinching(OVRHand.HandFinger.Index);
        }
        else if (rightHand != null)
        {
            isPinching = rightHand.GetFingerIsPinching(HandFinger.Index);
        }

        if (isPinching)
        {
            Transform dirTransform = handDirection != null ? handDirection : transform;
            float yaw = dirTransform.eulerAngles.y;
            Vector3 forward = Quaternion.Euler(0, yaw, 0) * Vector3.forward;

            if (invert) forward = -forward;

            transform.position += forward * moveSpeed * Time.deltaTime;

            Debug.DrawRay(transform.position, forward, Color.cyan, 0.1f);
        }
    }
}
