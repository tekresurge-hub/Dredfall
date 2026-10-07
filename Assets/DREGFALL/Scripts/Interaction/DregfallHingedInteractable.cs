using System.Collections;
using UnityEngine;

namespace Dregfall
{
    public enum DregfallHingeType { Door, Window }

    public sealed class DregfallHingedInteractable : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] DregfallHingeType hingeType = DregfallHingeType.Door;
        [SerializeField] string displayName = "Door";

        [Header("Physical Hinge")]
        [SerializeField] Transform movingPart;
        [SerializeField] Vector3 localOpenEuler = new Vector3(0f, 95f, 0f);
        [SerializeField, Range(0.2f, 3f)] float travelTime = 0.72f;
        [SerializeField] AnimationCurve motion = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("3D Audio")]
        [SerializeField] AudioClip openSound;
        [SerializeField] AudioClip closeSound;
        [SerializeField, Range(0f, 1f)] float volume = 0.82f;
        [SerializeField, Range(1f, 30f)] float maxAudioDistance = 15f;

        Quaternion closedRotation;
        Quaternion openRotation;
        AudioSource source;
        Coroutine movement;
        bool isOpen;

        public string DisplayName => displayName;
        public bool IsOpen => isOpen;
        public DregfallHingeType HingeType => hingeType;

        void Awake()
        {
            if (movingPart == null) movingPart = transform;
            closedRotation = movingPart.localRotation;
            openRotation = closedRotation * Quaternion.Euler(localOpenEuler);
            source = movingPart.GetComponent<AudioSource>();
            if (source == null) source = movingPart.gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 1.2f;
            source.maxDistance = maxAudioDistance;
            source.dopplerLevel = 0f;
        }

        public void Toggle()
        {
            bool targetOpen = !isOpen;
            if (movement != null) StopCoroutine(movement);
            movement = StartCoroutine(Move(targetOpen));
        }

        IEnumerator Move(bool targetOpen)
        {
            Quaternion from = movingPart.localRotation;
            Quaternion to = targetOpen ? openRotation : closedRotation;
            AudioClip clip = targetOpen ? openSound : closeSound;
            if (clip != null) source.PlayOneShot(clip, volume);

            float elapsed = 0f;
            float duration = Mathf.Max(0.05f, travelTime);
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = motion.Evaluate(Mathf.Clamp01(elapsed / duration));
                movingPart.localRotation = Quaternion.Slerp(from, to, t);
                yield return null;
            }

            movingPart.localRotation = to;
            isOpen = targetOpen;
            movement = null;
        }

        public void Configure(DregfallHingeType type, string label, Transform part, Vector3 openEuler,
            float seconds, AudioClip opening, AudioClip closing)
        {
            hingeType = type;
            displayName = label;
            movingPart = part != null ? part : transform;
            localOpenEuler = openEuler;
            travelTime = seconds;
            openSound = opening;
            closeSound = closing;
        }
    }
}
