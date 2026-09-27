using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BlenderInstancePipeline
{
    /// <summary>
    /// Græs-transforms importeret fra Blender. Ingen GameObjects – kun data,
    /// som <see cref="GrassInstanceRenderer"/> tegner med GPU instancing.
    ///
    /// Transforms er allerede konverteret til Unity-koordinater (Y-up, venstrehåndet)
    /// og er relative til det objekt, der har GrassInstanceRenderer'en.
    /// </summary>
    [CreateAssetMenu(menuName = "Blender Instances/Grass Instance Data", fileName = "GrassInstanceData")]
    public class GrassInstanceData : ScriptableObject
    {
        public List<GrassType> types = new List<GrassType>();

        // Tæller der stiger ved hver ændring, så rendereren ved, hvornår den skal genopbygge.
        [NonSerialized] int changeCount;
        public int ChangeCount => changeCount;
        public void NotifyChanged() => changeCount++;
        void OnValidate() => changeCount++;

        public int TotalInstances
        {
            get
            {
                int total = 0;
                foreach (var t in types) total += t.Count;
                return total;
            }
        }

        public GrassType Find(string typeName) => types.Find(t => t.name == typeName);
    }

    /// <summary>Én græstype (fx "Grass_Tall") med mesh, materiale og alle instances.</summary>
    [Serializable]
    public class GrassType
    {
        [Tooltip("Kildeobjektets navn i Blender (uden .001-suffiks)")]
        public string name;

        [Header("Rendering")]
        public Mesh mesh;
        public int subMeshIndex;
        [Tooltip("Skal have 'Enable GPU Instancing' slået til (sættes automatisk af importeren/rendereren i editoren)")]
        public Material material;
        public ShadowCastingMode castShadows = ShadowCastingMode.Off;
        public bool receiveShadows = true;

        [Header("Mesh-offset i forhold til instance-transform")]
        [Tooltip("Udfyldes af importeren fra prefab'ens MeshFilter-transform (fx -90° X fra FBX-importen). " +
                 "Overskrives ved hver import, hvis der findes en prefab med samme navn som græstypen.")]
        public Vector3 offsetPosition;
        public Vector3 offsetRotation;
        public Vector3 offsetScale = Vector3.one;

        // Selve instance-data. Skjult i inspectoren, da arrays med 100.000+ elementer gør den langsom.
        [SerializeField, HideInInspector] public Vector3[] positions = Array.Empty<Vector3>();
        [SerializeField, HideInInspector] public Quaternion[] rotations = Array.Empty<Quaternion>();
        [SerializeField, HideInInspector] public Vector3[] scales = Array.Empty<Vector3>();

        public int Count => positions != null ? positions.Length : 0;

        public Matrix4x4 OffsetMatrix => Matrix4x4.TRS(offsetPosition, Quaternion.Euler(offsetRotation), offsetScale);

        public bool IsRenderable => mesh != null && material != null && Count > 0 &&
                                    subMeshIndex >= 0 && subMeshIndex < mesh.subMeshCount;
    }
}
