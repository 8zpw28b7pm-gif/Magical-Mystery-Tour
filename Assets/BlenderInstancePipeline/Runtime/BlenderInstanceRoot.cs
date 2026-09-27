using UnityEngine;

namespace BlenderInstancePipeline
{
    /// <summary>
    /// Markerer det fælles parent-objekt for en Blender-import. Importeren finder objektet
    /// igen via <see cref="sourceKey"/> ved re-import, sletter alle børn og spawner på ny,
    /// så der aldrig opstår dubletter.
    ///
    /// Du må gerne flytte/rotere dette objekt – alt spawnet ligger lokalt under det
    /// (også græsset, hvis GrassInstanceRenderer sidder her).
    /// </summary>
    [DisallowMultipleComponent]
    public class BlenderInstanceRoot : MonoBehaviour
    {
        [Tooltip("JSON-filnavnet uden sti – bruges til at genkende roden ved re-import")]
        public string sourceKey;
        public string lastJsonPath;
        public string lastImportTime;
        public int spawnedObjects;
        public int grassInstances;
    }
}
