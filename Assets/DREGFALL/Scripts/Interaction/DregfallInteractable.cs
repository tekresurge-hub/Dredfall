using UnityEngine;

namespace Dregfall
{
    public sealed class DregfallInteractable : MonoBehaviour
    {
        [SerializeField] string displayName = "Object";
        [TextArea] [SerializeField] string description = "Nothing unusual.";
        [SerializeField] bool canChop;

        public string DisplayName => displayName;
        public string Description => description;
        public bool CanChop => canChop;

        public void Configure(string name, string info, bool chop)
        {
            displayName = name;
            description = info;
            canChop = chop;
        }
    }
}