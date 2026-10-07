using UnityEngine;

namespace Dregfall
{
    [CreateAssetMenu(menuName = "DREGFALL/Civilization Catalog", fileName = "DREGFALL_CivilizationCatalog")]
    public sealed class DregfallCivilizationCatalog : ScriptableObject
    {
        public GameObject[] isolatedBuildings;
    }
}
