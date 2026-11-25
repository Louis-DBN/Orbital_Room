using System.Collections.Generic;
using UnityEngine;

namespace Features.Movement
{
    /// <summary>
    /// Continuous ISS-style zero-gravity locomotion using Meta Quest hand tracking (OVRHand + OVRSkeleton).
    /// The rig is always a weightless 0G Rigidbody:
    /// - Closing a hand near any wall or handrail arrests inertia and lets you pull/push freely.
    /// - Releasing launches the rig with sampled 0G momentum and speed.
    /// - Index-pinching in open space fires a subtle backup EVA micro-thruster so you never get stranded.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public class ZeroGGrabLocomotion : MonoBehaviour
    {
        private const int HandCount = 2;
        private const int FingerCount = 4;

        [Header("Rig & Hand References")]
        [SerializeField] private Transform trackingSpace;
        [SerializeField] private Transform headAnchor;
        [SerializeField] private OVRHand leftHand;
        [SerializeField] private OVRSkeleton leftSkeleton;
        [SerializeField] private OVRHand rightHand;
        [SerializeField] private OVRSkeleton rightSkeleton;

        [Header("Hand Grab Detection (Hand Tracking Only)")]
        [Min(0.02f)]
        [SerializeField] private float grabRadius = 0.22f;
        [Range(0.1f, 0.95f)]
        [SerializeField] private float closeCurlThreshold = 0.42f;
        [Range(0.05f, 0.85f)]
        [SerializeField] private float releaseCurlThreshold = 0.24f;
        [Range(1, 4)]
        [SerializeField] private int requiredCurledFingers = 3;
        [SerializeField] private bool requireHighConfidenceToStart = false;
        [SerializeField] private LayerMask grabbableLayers = ~0;

        [Header("Zero-G Physics")]
        [Min(0.1f)]
        [SerializeField] private float maxSpeed = 6.0f;
        [Range(0.5f, 3.0f)]
        [SerializeField] private float launchMultiplier = 1.25f;
        [Min(0.02f)]
        [SerializeField] private float velocityHistoryWindow = 0.12f;
        [Min(0f)]
        [Tooltip("Near-zero damping so you continuously glide in 0G; set to 0 for pure frictionless drift.")]
        [SerializeField] private float zeroGDamping = 0.015f;
        [Range(0f, 0.9f)]
        [SerializeField] private float wallBounciness = 0.28f;

        [Header("Open-Space EVA Thruster (Anti-Stranding)")]
        [SerializeField] private bool enableAirThruster = true;
        [Min(0f)]
        [SerializeField] private float thrusterAcceleration = 1.8f;

        [Header("Body Capsule Collision")]
        [Min(0.05f)]
        [SerializeField] private float bodyRadius = 0.22f;
        [Min(0.3f)]
        [SerializeField] private float bodyHeight = 0.72f;
        [SerializeField] private float bodyCenterYOffset = -0.20f;
        [SerializeField] private LayerMask wallCollisionLayers = ~0;

        private struct HandBones
        {
            public bool Resolved;
            public int SkeletonChangeCount;
            public Transform Wrist;
            public Transform Palm;
            public Transform MiddleProximal;
            public Transform[] Proximals;
            public Transform[] Tips;
        }

        private struct VelocitySample
        {
            public float Time;
            public Vector3 Velocity;
        }

        private readonly HandBones[] handBones = new HandBones[HandCount];
        private readonly Collider[] grabbedSurfaces = new Collider[HandCount];
        private readonly Vector3[] previousLocalPalmPositions = new Vector3[HandCount];
        private readonly bool[] handClosedStates = new bool[HandCount];
        private readonly bool[] handNearSurfaceStates = new bool[HandCount];
        private readonly bool[] handThrusterStates = new bool[HandCount];
        private readonly Vector3[] lastPalmWorldPositions = new Vector3[HandCount];
        private readonly Collider[] overlapBuffer = new Collider[24];
        private readonly List<VelocitySample> velocityHistory = new List<VelocitySample>(16);

        private Rigidbody rigBody;
        private CapsuleCollider bodyCollider;
        private PhysicsMaterial zeroFrictionMaterial;
        private Vector3 pendingThrusterAccel;
        private bool wasAnyHandGrabbing;

        public bool IsLeftGrabbing => grabbedSurfaces[0] != null;
        public bool IsRightGrabbing => grabbedSurfaces[1] != null;
        public bool IsGrabbing => IsLeftGrabbing || IsRightGrabbing;
        public bool IsLeftNearSurface => handNearSurfaceStates[0];
        public bool IsRightNearSurface => handNearSurfaceStates[1];
        public bool IsLeftThrusting => handThrusterStates[0];
        public bool IsRightThrusting => handThrusterStates[1];
        public Vector3 LeftPalmWorldPosition => lastPalmWorldPositions[0];
        public Vector3 RightPalmWorldPosition => lastPalmWorldPositions[1];
        public Vector3 CurrentVelocity => rigBody != null ? rigBody.linearVelocity : Vector3.zero;
        public float CurrentSpeed => CurrentVelocity.magnitude;

        private void Awake()
        {
            for (int i = 0; i < HandCount; i++)
            {
                handBones[i].Proximals = new Transform[FingerCount];
                handBones[i].Tips = new Transform[FingerCount];
            }

            ConfigureRigPhysics();
        }

        private void Start()
        {
            if (trackingSpace == null || leftHand == null || rightHand == null)
            {
                Debug.LogError("ZeroGGrabLocomotion requires trackingSpace, leftHand, and rightHand references.", this);
                enabled = false;
                return;
            }

            if (leftSkeleton == null) leftSkeleton = leftHand.GetComponent<OVRSkeleton>();
            if (rightSkeleton == null) rightSkeleton = rightHand.GetComponent<OVRSkeleton>();
        }

        private void ConfigureRigPhysics()
        {
            rigBody = GetComponent<Rigidbody>();
            rigBody.useGravity = false;
            rigBody.isKinematic = false;
            rigBody.linearDamping = zeroGDamping;
            rigBody.angularDamping = 0f;
            rigBody.constraints = RigidbodyConstraints.FreezeRotation;
            rigBody.interpolation = RigidbodyInterpolation.Interpolate;
            rigBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            zeroFrictionMaterial = new PhysicsMaterial("ZeroGPlayer")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                bounciness = wallBounciness,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Maximum
            };

            Transform existing = transform.Find("ZeroGBodyCollider");
            GameObject colliderGo = existing != null ? existing.gameObject : new GameObject("ZeroGBodyCollider");
            colliderGo.transform.SetParent(transform, false);
            colliderGo.layer = gameObject.layer;

            bodyCollider = colliderGo.GetComponent<CapsuleCollider>();
            if (bodyCollider == null)
                bodyCollider = colliderGo.AddComponent<CapsuleCollider>();

            bodyCollider.isTrigger = false;
            bodyCollider.direction = 1;
            bodyCollider.radius = bodyRadius;
            bodyCollider.height = bodyHeight;
            bodyCollider.center = Vector3.zero;
            bodyCollider.sharedMaterial = zeroFrictionMaterial;

            SyncBodyColliderTransform();
        }

        private void FixedUpdate()
        {
            if (rigBody == null) return;

            SyncBodyColliderTransform();

            if (!rigBody.isKinematic)
            {
                rigBody.linearDamping = zeroGDamping;

                if (pendingThrusterAccel.sqrMagnitude > 1e-6f)
                {
                    rigBody.AddForce(pendingThrusterAccel, ForceMode.Acceleration);
                }

                if (rigBody.linearVelocity.sqrMagnitude > maxSpeed * maxSpeed)
                {
                    rigBody.linearVelocity = rigBody.linearVelocity.normalized * maxSpeed;
                }
            }

            pendingThrusterAccel = Vector3.zero;
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0.0001f || trackingSpace == null || rigBody == null)
                return;

            Physics.SyncTransforms();
            SyncBodyColliderTransform();

            int grabbingCount = 0;
            Vector3 totalPull = Vector3.zero;

            if (ProcessHand(0, leftHand, leftSkeleton, out Vector3 leftPull))
            {
                totalPull += leftPull;
                grabbingCount++;
            }

            if (ProcessHand(1, rightHand, rightSkeleton, out Vector3 rightPull))
            {
                totalPull += rightPull;
                grabbingCount++;
            }

            bool isGrabbingNow = IsGrabbing;

            if (isGrabbingNow)
            {
                if (!wasAnyHandGrabbing)
                {
                    // Arrest all 0G inertia the instant a hand latches onto a bar or wall.
                    rigBody.linearVelocity = Vector3.zero;
                    rigBody.angularVelocity = Vector3.zero;
                    rigBody.isKinematic = true;
                    velocityHistory.Clear();
                }

                if (grabbingCount > 0)
                {
                    Vector3 desiredDelta = totalPull / grabbingCount;
                    Vector3 startPos = transform.position;
                    MoveKinematicWithWallClamp(desiredDelta);
                    Vector3 appliedDelta = transform.position - startPos;
                    RecordPullVelocitySample(appliedDelta / dt);
                }
                else
                {
                    RecordPullVelocitySample(Vector3.zero);
                }

                pendingThrusterAccel = Vector3.zero;
                wasAnyHandGrabbing = true;
                return;
            }

            if (wasAnyHandGrabbing)
            {
                // Restore free 0G Rigidbody motion and impart launch momentum from recent pull.
                rigBody.isKinematic = false;
                rigBody.linearDamping = zeroGDamping;
                rigBody.linearVelocity = ComputeLaunchVelocity();
                velocityHistory.Clear();
                wasAnyHandGrabbing = false;
            }

            if (enableAirThruster)
            {
                pendingThrusterAccel = ComputeOpenSpaceThrusterAcceleration();
            }
        }

        private bool ProcessHand(int index, OVRHand hand, OVRSkeleton skeleton, out Vector3 worldPull)
        {
            worldPull = Vector3.zero;
            handThrusterStates[index] = false;

            if (hand == null || !hand.isActiveAndEnabled || !hand.IsTracked || !hand.IsDataValid)
            {
                ReleaseHand(index);
                handClosedStates[index] = false;
                handNearSurfaceStates[index] = false;
                return false;
            }

            Vector3 palmPos = GetPalmWorldPosition(index, hand, skeleton);
            lastPalmWorldPositions[index] = palmPos;

            Collider nearbySurface = FindClosestGrabbableSurface(palmPos);
            handNearSurfaceStates[index] = nearbySurface != null;

            bool wasClosed = handClosedStates[index];
            bool isClosed = IsHandClosed(index, hand, skeleton, wasClosed);
            handClosedStates[index] = isClosed;

            if (!isClosed)
            {
                ReleaseHand(index);
                return false;
            }

            Collider grabbed = grabbedSurfaces[index];
            if (grabbed != null && (!grabbed.enabled || !grabbed.gameObject.activeInHierarchy))
            {
                ReleaseHand(index);
                grabbed = null;
            }

            if (grabbed == null)
            {
                if (requireHighConfidenceToStart && !hand.IsDataHighConfidence)
                    return false;

                if (nearbySurface == null)
                    return false;

                grabbedSurfaces[index] = nearbySurface;
                previousLocalPalmPositions[index] = trackingSpace.InverseTransformPoint(palmPos);
                return false;
            }

            Vector3 currentLocalPalm = trackingSpace.InverseTransformPoint(palmPos);
            // Tracking-space coordinates stay independent of rig translation, avoiding feedback loops.
            worldPull = trackingSpace.TransformVector(previousLocalPalmPositions[index] - currentLocalPalm);
            previousLocalPalmPositions[index] = currentLocalPalm;
            return true;
        }

        private Vector3 ComputeOpenSpaceThrusterAcceleration()
        {
            Vector3 accel = Vector3.zero;

            if (TryGetHandThrusterDirection(0, leftHand, out Vector3 leftDir))
                accel += leftDir * thrusterAcceleration;

            if (TryGetHandThrusterDirection(1, rightHand, out Vector3 rightDir))
                accel += rightDir * thrusterAcceleration;

            return accel;
        }

        private bool TryGetHandThrusterDirection(int index, OVRHand hand, out Vector3 direction)
        {
            direction = Vector3.zero;
            if (hand == null || !hand.isActiveAndEnabled || !hand.IsTracked || !hand.IsDataValid)
                return false;

            if (handClosedStates[index] || handNearSurfaceStates[index])
                return false;

            if (!hand.GetFingerIsPinching(OVRHand.HandFinger.Index))
                return false;

            Transform pointer = hand.IsPointerPoseValid ? hand.PointerPose : hand.transform;
            direction = pointer.forward.normalized;
            handThrusterStates[index] = true;
            return true;
        }

        private void RecordPullVelocitySample(Vector3 sampleVelocity)
        {
            float now = Time.unscaledTime;
            velocityHistory.Add(new VelocitySample
            {
                Time = now,
                Velocity = Vector3.ClampMagnitude(sampleVelocity, maxSpeed)
            });

            float cutoff = now - velocityHistoryWindow;
            while (velocityHistory.Count > 0 && velocityHistory[0].Time < cutoff)
            {
                velocityHistory.RemoveAt(0);
            }
        }

        private Vector3 ComputeLaunchVelocity()
        {
            float now = Time.unscaledTime;
            float cutoff = now - velocityHistoryWindow;

            // Use the strongest recent pull direction/speed inside the release window so
            // slight deceleration while opening the fingers does not cancel the push-off.
            Vector3 peakVelocity = Vector3.zero;
            float peakSqMag = 0f;
            Vector3 weightedSum = Vector3.zero;
            float weightTotal = 0f;

            for (int i = 0; i < velocityHistory.Count; i++)
            {
                VelocitySample sample = velocityHistory[i];
                if (sample.Time < cutoff)
                    continue;

                float sqMag = sample.Velocity.sqrMagnitude;
                if (sqMag > peakSqMag)
                {
                    peakSqMag = sqMag;
                    peakVelocity = sample.Velocity;
                }

                if (sqMag > 0.0025f)
                {
                    float weight = Mathf.Sqrt(sqMag);
                    weightedSum += sample.Velocity * weight;
                    weightTotal += weight;
                }
            }

            if (peakSqMag < 0.0025f)
                return Vector3.zero;

            Vector3 blended = weightTotal > 0f
                ? Vector3.Lerp(weightedSum / weightTotal, peakVelocity, 0.65f)
                : peakVelocity;

            return Vector3.ClampMagnitude(blended * launchMultiplier, maxSpeed);
        }

        private Vector3 GetPalmWorldPosition(int index, OVRHand hand, OVRSkeleton skeleton)
        {
            EnsureBonesResolved(index, skeleton);
            HandBones bones = handBones[index];

            if (bones.Resolved)
            {
                if (bones.Palm != null)
                    return bones.Palm.position;

                if (bones.Wrist != null && bones.MiddleProximal != null)
                    return (bones.Wrist.position + bones.MiddleProximal.position) * 0.5f;
            }

            return hand.transform.position;
        }

        private bool IsHandClosed(int index, OVRHand hand, OVRSkeleton skeleton, bool wasClosed)
        {
            EnsureBonesResolved(index, skeleton);
            HandBones bones = handBones[index];
            float threshold = wasClosed ? releaseCurlThreshold : closeCurlThreshold;

            int curledCount = 0;

            if (bones.Resolved && bones.Wrist != null && bones.MiddleProximal != null)
            {
                Vector3 palmDir = bones.MiddleProximal.position - bones.Wrist.position;
                if (palmDir.sqrMagnitude > 1e-6f)
                {
                    Vector3 palmForward = palmDir.normalized;
                    for (int f = 0; f < FingerCount; f++)
                    {
                        float curl = GetPinchFallback(hand, f);
                        Transform proximal = bones.Proximals[f];
                        Transform tip = bones.Tips[f];
                        if (proximal != null && tip != null)
                        {
                            Vector3 fingerVec = tip.position - proximal.position;
                            if (fingerVec.sqrMagnitude > 1e-6f)
                            {
                                float alignment = Vector3.Dot(fingerVec.normalized, palmForward);
                                float boneCurl = Mathf.Clamp01((0.65f - alignment) / 0.9f);
                                curl = Mathf.Max(curl, boneCurl);
                            }
                        }

                        if (curl >= threshold)
                            curledCount++;
                    }

                    return curledCount >= requiredCurledFingers;
                }
            }

            for (int f = 0; f < FingerCount; f++)
            {
                if (GetPinchFallback(hand, f) >= threshold)
                    curledCount++;
            }

            return curledCount >= requiredCurledFingers;
        }

        private static float GetPinchFallback(OVRHand hand, int fingerIndex)
        {
            return fingerIndex switch
            {
                0 => hand.GetFingerPinchStrength(OVRHand.HandFinger.Index),
                1 => hand.GetFingerPinchStrength(OVRHand.HandFinger.Middle),
                2 => hand.GetFingerPinchStrength(OVRHand.HandFinger.Ring),
                3 => hand.GetFingerPinchStrength(OVRHand.HandFinger.Pinky),
                _ => 0f
            };
        }

        private void EnsureBonesResolved(int index, OVRSkeleton skeleton)
        {
            if (skeleton == null || !skeleton.IsInitialized || skeleton.Bones == null || skeleton.Bones.Count == 0)
                return;

            ref HandBones bones = ref handBones[index];
            if (bones.Resolved && bones.SkeletonChangeCount == skeleton.SkeletonChangedCount)
                return;

            bones.Wrist = null;
            bones.Palm = null;
            bones.MiddleProximal = null;
            for (int i = 0; i < FingerCount; i++)
            {
                bones.Proximals[i] = null;
                bones.Tips[i] = null;
            }

            OVRSkeleton.SkeletonType skeletonType = skeleton.GetSkeletonType();
            bool isOpenXrHand = skeletonType == OVRSkeleton.SkeletonType.XRHandLeft ||
                                skeletonType == OVRSkeleton.SkeletonType.XRHandRight;

            IList<OVRBone> boneList = skeleton.Bones;
            for (int i = 0; i < boneList.Count; i++)
            {
                OVRBone bone = boneList[i];
                if (bone == null || bone.Transform == null) continue;

                if (isOpenXrHand)
                    AssignOpenXrBone(ref bones, bone);
                else
                    AssignOvrBone(ref bones, bone);
            }

            bones.SkeletonChangeCount = skeleton.SkeletonChangedCount;
            bones.Resolved = bones.Wrist != null && bones.MiddleProximal != null;
        }

        private static void AssignOpenXrBone(ref HandBones bones, OVRBone bone)
        {
            switch (bone.Id)
            {
                case OVRSkeleton.BoneId.XRHand_Palm:
                    bones.Palm = bone.Transform;
                    break;
                case OVRSkeleton.BoneId.XRHand_Wrist:
                    bones.Wrist = bone.Transform;
                    break;
                case OVRSkeleton.BoneId.XRHand_IndexProximal:
                    bones.Proximals[0] = bone.Transform;
                    break;
                case OVRSkeleton.BoneId.XRHand_IndexTip:
                    bones.Tips[0] = bone.Transform;
                    break;
                case OVRSkeleton.BoneId.XRHand_MiddleProximal:
                    bones.Proximals[1] = bone.Transform;
                    bones.MiddleProximal = bone.Transform;
                    break;
                case OVRSkeleton.BoneId.XRHand_MiddleTip:
                    bones.Tips[1] = bone.Transform;
                    break;
                case OVRSkeleton.BoneId.XRHand_RingProximal:
                    bones.Proximals[2] = bone.Transform;
                    break;
                case OVRSkeleton.BoneId.XRHand_RingTip:
                    bones.Tips[2] = bone.Transform;
                    break;
                case OVRSkeleton.BoneId.XRHand_LittleProximal:
                    bones.Proximals[3] = bone.Transform;
                    break;
                case OVRSkeleton.BoneId.XRHand_LittleTip:
                    bones.Tips[3] = bone.Transform;
                    break;
            }
        }

        private static void AssignOvrBone(ref HandBones bones, OVRBone bone)
        {
            switch (bone.Id)
            {
                case OVRSkeleton.BoneId.Hand_WristRoot:
                    bones.Wrist = bone.Transform;
                    break;
                case OVRSkeleton.BoneId.Hand_Index1:
                    bones.Proximals[0] = bone.Transform;
                    break;
                case OVRSkeleton.BoneId.Hand_IndexTip:
                    bones.Tips[0] = bone.Transform;
                    break;
                case OVRSkeleton.BoneId.Hand_Middle1:
                    bones.Proximals[1] = bone.Transform;
                    bones.MiddleProximal = bone.Transform;
                    break;
                case OVRSkeleton.BoneId.Hand_MiddleTip:
                    bones.Tips[1] = bone.Transform;
                    break;
                case OVRSkeleton.BoneId.Hand_Ring1:
                    bones.Proximals[2] = bone.Transform;
                    break;
                case OVRSkeleton.BoneId.Hand_RingTip:
                    bones.Tips[2] = bone.Transform;
                    break;
                case OVRSkeleton.BoneId.Hand_Pinky1:
                    bones.Proximals[3] = bone.Transform;
                    break;
                case OVRSkeleton.BoneId.Hand_PinkyTip:
                    bones.Tips[3] = bone.Transform;
                    break;
            }
        }

        private Collider FindClosestGrabbableSurface(Vector3 palmPos)
        {
            // Include visible trigger surfaces (such as non-blocking ISS handrails) as well as solid walls.
            int count = Physics.OverlapSphereNonAlloc(
                palmPos, grabRadius, overlapBuffer, grabbableLayers, QueryTriggerInteraction.Collide);

            Collider best = null;
            float bestDistSq = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                Collider candidate = overlapBuffer[i];
                if (candidate == null || candidate.transform.IsChildOf(transform))
                    continue;

                if (candidate.isTrigger && candidate.GetComponent<Renderer>() == null)
                    continue;

                Vector3 closest = candidate.ClosestPoint(palmPos);
                float distSq = (closest - palmPos).sqrMagnitude;
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    best = candidate;
                }
            }

            return best;
        }

        private void SyncBodyColliderTransform()
        {
            if (bodyCollider == null) return;

            Vector3 headWorld = headAnchor != null ? headAnchor.position : transform.position + Vector3.up * 1.5f;
            bodyCollider.transform.position = headWorld + Vector3.up * bodyCenterYOffset;
            bodyCollider.transform.rotation = Quaternion.identity;
        }

        private void MoveKinematicWithWallClamp(Vector3 delta)
        {
            if (bodyCollider == null)
            {
                transform.position += delta;
                return;
            }

            Vector3 remaining = delta;
            const float skin = 0.005f;

            for (int pass = 0; pass < 3; pass++)
            {
                float dist = remaining.magnitude;
                if (dist < 1e-5f)
                    break;

                GetBodyCapsuleEndpoints(out Vector3 p0, out Vector3 p1);
                Vector3 dir = remaining / dist;

                if (!SweepCapsuleIgnoringRig(p0, p1, bodyRadius, dir, dist + skin, out RaycastHit hit))
                {
                    transform.position += remaining;
                    SyncBodyColliderTransform();
                    break;
                }

                float moveDist = Mathf.Max(0f, hit.distance - skin);
                Vector3 step = dir * moveDist;
                transform.position += step;
                SyncBodyColliderTransform();

                remaining = Vector3.ProjectOnPlane(remaining - step, hit.normal);
            }

            DepenetrateFromWalls();
        }

        private bool SweepCapsuleIgnoringRig(
            Vector3 p0, Vector3 p1, float radius, Vector3 dir, float maxDist, out RaycastHit validHit)
        {
            RaycastHit[] hits = Physics.CapsuleCastAll(
                p0, p1, radius, dir, maxDist, wallCollisionLayers, QueryTriggerInteraction.Ignore);

            bool found = false;
            float closest = float.MaxValue;
            validHit = default;

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider == null || hit.collider.transform.IsChildOf(transform))
                    continue;

                if (hit.distance < closest)
                {
                    closest = hit.distance;
                    validHit = hit;
                    found = true;
                }
            }

            return found;
        }

        private void DepenetrateFromWalls()
        {
            if (bodyCollider == null) return;

            for (int iter = 0; iter < 2; iter++)
            {
                GetBodyCapsuleEndpoints(out Vector3 p0, out Vector3 p1);
                int count = Physics.OverlapCapsuleNonAlloc(
                    p0, p1, bodyRadius, overlapBuffer, wallCollisionLayers, QueryTriggerInteraction.Ignore);

                bool moved = false;
                for (int i = 0; i < count; i++)
                {
                    Collider col = overlapBuffer[i];
                    if (col == null || col.transform.IsChildOf(transform))
                        continue;

                    if (Physics.ComputePenetration(
                        bodyCollider, bodyCollider.transform.position, bodyCollider.transform.rotation,
                        col, col.transform.position, col.transform.rotation,
                        out Vector3 dir, out float dist))
                    {
                        transform.position += dir * dist;
                        SyncBodyColliderTransform();
                        moved = true;
                    }
                }

                if (!moved) break;
            }
        }

        private void GetBodyCapsuleEndpoints(out Vector3 bottom, out Vector3 top)
        {
            Vector3 center = bodyCollider.transform.position;
            float halfSegment = Mathf.Max(0f, bodyHeight * 0.5f - bodyRadius);
            bottom = center - Vector3.up * halfSegment;
            top = center + Vector3.up * halfSegment;
        }

        private void ReleaseHand(int index)
        {
            grabbedSurfaces[index] = null;
        }

        private void OnDisable()
        {
            ReleaseHand(0);
            ReleaseHand(1);
            handClosedStates[0] = false;
            handClosedStates[1] = false;
            handNearSurfaceStates[0] = false;
            handNearSurfaceStates[1] = false;
            handThrusterStates[0] = false;
            handThrusterStates[1] = false;
            velocityHistory.Clear();
            pendingThrusterAccel = Vector3.zero;
            wasAnyHandGrabbing = false;

            if (rigBody != null)
            {
                rigBody.linearVelocity = Vector3.zero;
                rigBody.isKinematic = false;
            }
        }

        private void OnDestroy()
        {
            if (zeroFrictionMaterial != null)
                Destroy(zeroFrictionMaterial);
        }

        private void OnDrawGizmosSelected()
        {
            if (headAnchor != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(headAnchor.position, bodyRadius);
            }

            Gizmos.color = Color.yellow;
            if (leftHand != null)
                Gizmos.DrawWireSphere(Application.isPlaying ? lastPalmWorldPositions[0] : leftHand.transform.position, grabRadius);
            if (rightHand != null)
                Gizmos.DrawWireSphere(Application.isPlaying ? lastPalmWorldPositions[1] : rightHand.transform.position, grabRadius);
        }
    }
}
