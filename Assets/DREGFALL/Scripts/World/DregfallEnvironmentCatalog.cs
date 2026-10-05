using UnityEngine;

namespace Dregfall
{
    [CreateAssetMenu(menuName = "DREGFALL/Environment Catalog", fileName = "DREGFALL_EnvironmentCatalog")]
    public sealed class DregfallEnvironmentCatalog : ScriptableObject
    {
        public GameObject[] trees;
        public GameObject[] undergrowth;
        public GameObject[] rocks;
    }
}
