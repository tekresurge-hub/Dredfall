using UnityEngine;

namespace Dregfall
{
    // Legacy streamed-grass renderer retired with the unlimited-world system.
    // Kept as a lightweight compatibility component so old scene references do not break.
    public sealed class DregfallGrassFieldRenderer : MonoBehaviour
    {
        public void Initialize() { }
        public void BuildChunk(Vector2Int coord) { }
        public void RemoveChunk(Vector2Int coord) { }
    }
}
