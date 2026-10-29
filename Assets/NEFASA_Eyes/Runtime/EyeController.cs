using UnityEngine;

namespace NEFASA.Eyes
{
    /// <summary>Shared, per-instance settings for one eye or an eye pair.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class EyeController : MonoBehaviour
    {
        [ColorUsage(false), InspectorName("Augenfarbe")]
        public Color irisColor = new Color(0.328224771f, 0.313304157f, 0.273957575f);
        [ColorUsage(false), InspectorName("Innenfarbe")]
        public Color innerColor = new Color(0.405842683f, 0.338902798f, 0.234795079f);
        [Range(0.9f, 3.2f), InspectorName("Pupillenradius (mm)")]
        public float pupilRadius = 2.15f;
        [Range(0f, 1f), InspectorName("Fasertiefe")]
        public float fiberDepth = 0.24f;
        [Range(0f, 1f), InspectorName("Äderchen")]
        public float vessels = 0.60f;
        [Range(0f, 1f), InspectorName("Pigmentierung")]
        public float pigmentation = 0.660000000f;
        [Range(0f, 1f), InspectorName("Irisrand")]
        public float limbalRing = 0.58f;
        [Range(0f, 1f), InspectorName("Glanz")]
        public float gloss = 0.62f;
        [Range(0f, 1f), InspectorName("Feuchtigkeit")]
        public float moisture = 0.65f;

        [InspectorName("Sehnerv anzeigen / Show optic nerve")]
        public bool showOpticNerve = true;
        [SerializeField, HideInInspector] private OpticNervePart[] opticNerves;
        [SerializeField, HideInInspector] private SkinnedMeshRenderer[] surfaces;
        [SerializeField, HideInInspector] private MeshRenderer[] corneas;
        private MaterialPropertyBlock block;
        private int lastHash;
        private bool applied;
        private static readonly int Iris = Shader.PropertyToID("_IrisColor");
        private static readonly int Inner = Shader.PropertyToID("_InnerColor");
        private static readonly int Fiber = Shader.PropertyToID("_FiberDepth");
        private static readonly int Vessel = Shader.PropertyToID("_Vessels");
        private static readonly int Pigment = Shader.PropertyToID("_Pigmentation");
        private static readonly int Ring = Shader.PropertyToID("_LimbalRing");
        private static readonly int Gloss = Shader.PropertyToID("_Gloss");
        private static readonly int Moisture = Shader.PropertyToID("_Moisture");

        private void OnEnable() { CacheRenderers(); ApplySettings(); }
        private void OnValidate() { applied = false; }
        private void LateUpdate()
        {
            if (!applied || GetSettingsHash() != lastHash) ApplySettings();
        }

        /// <summary>Call after changing the hierarchy. Settings never edit shared materials.</summary>
        public void CacheRenderers()
        {
            surfaces = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            corneas = System.Array.FindAll(GetComponentsInChildren<MeshRenderer>(true), renderer => !renderer.GetComponentInParent<OpticNervePart>(true));
            opticNerves = GetComponentsInChildren<OpticNervePart>(true);
        }

        /// <summary>Immediately applies Inspector or script changes to all three LODs.</summary>
        public void ApplySettings()
        {
            pupilRadius = Mathf.Clamp(pupilRadius, 0.9f, 3.2f);
            fiberDepth = Mathf.Clamp01(fiberDepth);
            vessels = Mathf.Clamp01(vessels);
            pigmentation = Mathf.Clamp01(pigmentation);
            limbalRing = Mathf.Clamp01(limbalRing);
            gloss = Mathf.Clamp01(gloss);
            moisture = Mathf.Clamp01(moisture);
            if (surfaces == null || corneas == null || opticNerves == null) CacheRenderers();
            if (block == null) block = new MaterialPropertyBlock();
            foreach (var surface in surfaces)
            {
                if (!surface || !surface.sharedMesh) continue;
                ApplyMaterialSettings(surface);
                var mesh = surface.sharedMesh;
                int small = mesh.GetBlendShapeIndex("PupilSmall");
                int large = mesh.GetBlendShapeIndex("PupilLarge");
                if (small >= 0) surface.SetBlendShapeWeight(small, Mathf.Max(0f, 1.9f - pupilRadius) * 100f);
                if (large >= 0) surface.SetBlendShapeWeight(large, Mathf.Max(0f, pupilRadius - 1.9f) / 1.3f * 100f);
            }
            foreach (var cornea in corneas) if (cornea) ApplyMaterialSettings(cornea);
            foreach (var nerve in opticNerves) if (nerve) nerve.gameObject.SetActive(showOpticNerve);
            lastHash = GetSettingsHash();
            applied = true;
        }

        private void ApplyMaterialSettings(Renderer renderer)
        {
            renderer.GetPropertyBlock(block);
            block.SetVector(Iris, (Vector4)(QualitySettings.activeColorSpace == ColorSpace.Linear ? irisColor.linear : irisColor));
            block.SetVector(Inner, (Vector4)(QualitySettings.activeColorSpace == ColorSpace.Linear ? innerColor.linear : innerColor));
            block.SetFloat(Fiber, fiberDepth);
            block.SetFloat(Vessel, vessels);
            block.SetFloat(Pigment, pigmentation);
            block.SetFloat(Ring, limbalRing);
            block.SetFloat(Gloss, gloss);
            block.SetFloat(Moisture, moisture);
            renderer.SetPropertyBlock(block);
            block.Clear();
        }

        private int GetSettingsHash()
        {
            unchecked
            {
                int hash = irisColor.GetHashCode();
                hash = hash * 31 + innerColor.GetHashCode();
                hash = hash * 31 + pupilRadius.GetHashCode();
                hash = hash * 31 + fiberDepth.GetHashCode();
                hash = hash * 31 + vessels.GetHashCode();
                hash = hash * 31 + pigmentation.GetHashCode();
                hash = hash * 31 + limbalRing.GetHashCode();
                hash = hash * 31 + gloss.GetHashCode();
                hash = hash * 31 + moisture.GetHashCode();
                return hash * 31 + showOpticNerve.GetHashCode();
            }
        }
    }
}
