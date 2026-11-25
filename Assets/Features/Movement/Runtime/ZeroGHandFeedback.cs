using UnityEngine;

namespace Features.Movement
{
    /// <summary>
    /// Renders subtle spatial palm indicators showing when each hand can grab a nearby surface,
    /// is latched onto the station structure, or is firing the backup EVA micro-thruster.
    /// </summary>
    [RequireComponent(typeof(ZeroGGrabLocomotion))]
    [DisallowMultipleComponent]
    public class ZeroGHandFeedback : MonoBehaviour
    {
        [SerializeField] private ZeroGGrabLocomotion locomotion;
        [SerializeField] private float indicatorSize = 0.045f;
        [SerializeField] private Color readyColor = new Color(0.2f, 0.85f, 1f, 0.45f);
        [SerializeField] private Color grabbingColor = new Color(0.25f, 1f, 0.45f, 0.85f);
        [SerializeField] private Color thrusterColor = new Color(0.45f, 0.65f, 1f, 0.7f);

        private Renderer leftIndicator;
        private Renderer rightIndicator;
        private Material leftMaterial;
        private Material rightMaterial;

        private void Awake()
        {
            if (locomotion == null)
                locomotion = GetComponent<ZeroGGrabLocomotion>();

            leftIndicator = CreateIndicator("LeftGrabIndicator", out leftMaterial);
            rightIndicator = CreateIndicator("RightGrabIndicator", out rightMaterial);
        }

        private void LateUpdate()
        {
            if (locomotion == null) return;

            UpdateIndicator(
                leftIndicator,
                leftMaterial,
                locomotion.IsLeftGrabbing,
                locomotion.IsLeftNearSurface,
                locomotion.IsLeftThrusting,
                locomotion.LeftPalmWorldPosition);

            UpdateIndicator(
                rightIndicator,
                rightMaterial,
                locomotion.IsRightGrabbing,
                locomotion.IsRightNearSurface,
                locomotion.IsRightThrusting,
                locomotion.RightPalmWorldPosition);
        }

        private void UpdateIndicator(
            Renderer indicator, Material mat, bool isGrabbing, bool isNear, bool isThrusting, Vector3 palmPos)
        {
            if (indicator == null || mat == null) return;

            bool visible = isGrabbing || isNear || isThrusting;
            indicator.enabled = visible;
            if (!visible) return;

            indicator.transform.position = palmPos;
            indicator.transform.localScale = Vector3.one * (isGrabbing ? indicatorSize * 1.25f : indicatorSize);
            mat.color = isGrabbing ? grabbingColor : (isThrusting ? thrusterColor : readyColor);
        }

        private Renderer CreateIndicator(string objectName, out Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = objectName;
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * indicatorSize;

            Collider col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            mat = new Material(shader);
            mat.color = readyColor;

            Renderer rend = go.GetComponent<Renderer>();
            rend.sharedMaterial = mat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
            rend.enabled = false;
            return rend;
        }

        private void OnDestroy()
        {
            if (leftMaterial != null) Destroy(leftMaterial);
            if (rightMaterial != null) Destroy(rightMaterial);
        }
    }
}
