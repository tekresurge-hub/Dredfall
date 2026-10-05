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
            QualitySettings.lodBias = 2.15f;
            QualitySettings.maximumLODLevel = 0;
            QualitySettings.shadowDistance = 72f;
            QualitySettings.shadowResolution = UnityEngine.ShadowResolution.Medium;
            QualitySettings.shadows = UnityEngine.ShadowQuality.All;
            QualitySettings.softParticles = true;
            QualitySettings.realtimeReflectionProbes = false;

            if (camera != null)
            {
                camera.allowHDR = true;
                camera.allowMSAA = true;
                camera.farClipPlane = Mathf.Max(camera.farClipPlane, 420f);
                camera.backgroundColor = new Color(0.055f, 0.065f, 0.065f);

                // URP has its own post AA setting. SMAA is much better suited to the
                // thousands of thin alpha-tested leaf/grass edges in DREGFALL than
                // relying on the Camera MSAA flag alone.
                UniversalAdditionalCameraData urpCamera = camera.GetUniversalAdditionalCameraData();
                urpCamera.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                urpCamera.antialiasingQuality = AntialiasingQuality.High;
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.34f, 0.37f, 0.34f);
            RenderSettings.ambientEquatorColor = new Color(0.19f, 0.21f, 0.18f);
            RenderSettings.ambientGroundColor = new Color(0.085f, 0.075f, 0.06f);
            RenderSettings.ambientIntensity = 0.82f;

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
                sun.color = new Color(1.0f, 0.94f, 0.84f);
                sun.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            }
        }
    }
}
