using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Dregfall
{
    // Central runtime visual baseline for DREGFALL. Keeps the procedural world crisp
    // without requiring every test scene to be hand-configured.
    public sealed class DregfallVisualQuality : MonoBehaviour
    {
        public static void Apply(Camera camera)
        {
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
            QualitySettings.globalTextureMipmapLimit = 0;
            QualitySettings.lodBias = 1.35f;
            QualitySettings.maximumLODLevel = 0;
            QualitySettings.shadowDistance = 46f;
            QualitySettings.shadowResolution = UnityEngine.ShadowResolution.High;
            QualitySettings.shadows = UnityEngine.ShadowQuality.All;
            QualitySettings.softParticles = true;
            QualitySettings.realtimeReflectionProbes = false;

            if (camera != null)
            {
                camera.allowHDR = true;
                camera.allowMSAA = true;
                camera.farClipPlane = 260f;
                camera.backgroundColor = new Color(0.055f, 0.065f, 0.065f);

                // URP has its own post AA setting. SMAA is much better suited to the
                // thousands of thin alpha-tested leaf/grass edges in DREGFALL than
                // relying on the Camera MSAA flag alone.
                UniversalAdditionalCameraData urpCamera = camera.GetUniversalAdditionalCameraData();
                urpCamera.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                urpCamera.antialiasingQuality = AntialiasingQuality.High;
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.205f, 0.225f, 0.205f);
            RenderSettings.ambientEquatorColor = new Color(0.115f, 0.13f, 0.11f);
            RenderSettings.ambientGroundColor = new Color(0.052f, 0.047f, 0.039f);
            RenderSettings.ambientIntensity = 0.58f;

            Light sun = RenderSettings.sun;
            if (sun == null)
            {
                Light[] lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
                foreach (Light light in lights)
                    if (light.type == LightType.Directional) { sun = light; break; }
            }

            if (sun != null)
            {
                RenderSettings.sun = sun;
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = 0.82f;
                sun.intensity = 1.05f;
                sun.color = new Color(1.0f, 0.965f, 0.90f);
                sun.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            }
        }
    }
}
