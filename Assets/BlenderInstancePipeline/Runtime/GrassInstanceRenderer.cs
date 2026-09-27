using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BlenderInstancePipeline
{
    /// <summary>
    /// Tegner græs fra et <see cref="GrassInstanceData"/>-asset med Graphics.RenderMeshInstanced.
    ///
    /// Hvorfor RenderMeshInstanced og ikke RenderMeshIndirect?
    ///  - RenderMeshInstanced virker med ALLE shaders der understøtter almindelig GPU instancing
    ///    (#pragma multi_compile_instancing) – også Shader Graph og URP/Lit.
    ///  - RenderMeshIndirect kræver en shader, der selv læser transforms fra en StructuredBuffer,
    ///    og giver først mening med compute-culling på GPU'en (mange hundrede tusinde instances).
    ///
    /// Opbygning:
    ///  - Ved start sorteres alle instances i et grid af celler (cellSize x cellSize meter).
    ///  - Hver celle har sit eget Matrix4x4[]-array og sine egne world bounds.
    ///  - Pr. kamera springes celler længere væk end maxDrawDistance over (afstands-culling).
    ///  - Frustum-culling af hver celle klarer Unity selv via RenderParams.worldBounds
    ///    (det gælder også shadow-casting, så skygger ikke forsvinder i kanten af skærmen).
    ///  - Hver celle tegnes i bidder af højst 1023 instances pr. kald (se MaxInstancesPerCall).
    ///
    /// Tegningen sker i RenderPipelineManager.beginCameraRendering, så græsset også ses i
    /// Scene view i edit mode (Update() kører ikke hver frame i edit mode).
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("Rendering/Grass Instance Renderer")]
    public class GrassInstanceRenderer : MonoBehaviour
    {
        // Den klassiske grænse for instancing pr. draw call (constant buffer på 64 KB / 64 byte pr. matrix).
        // Unity kan muligvis selv splitte større kald internt i nyere versioner, men vi chunker
        // eksplicit, så det virker uanset.
        const int MaxInstancesPerCall = 1023;

        public GrassInstanceData data;

        [Tooltip("Størrelse på culling-celler i meter. Mindre = mere præcis culling, flere draw calls.")]
        [Min(1f)] public float cellSize = 20f;

        [Tooltip("Celler længere væk end dette (fra kameraet) tegnes ikke. 0 = ingen grænse.")]
        [Min(0f)] public float maxDrawDistance = 120f;

        public bool drawInSceneView = true;

        [Header("Info (kun læsning)")]
        [SerializeField] int instancesDrawnLastCamera;
        [SerializeField] int drawCallsLastCamera;

        class Cell
        {
            public Bounds bounds;
            public Matrix4x4[] matrices;
        }

        class TypeBatch
        {
            public GrassType type;
            public RenderParams renderParams;
            public readonly List<Cell> cells = new List<Cell>();
        }

        readonly List<TypeBatch> batches = new List<TypeBatch>();
        bool isDirty = true;
        int builtChangeCount = -1;
        GrassInstanceData builtData;
        Matrix4x4 builtLocalToWorld;
#if !UNITY_EDITOR
        bool warnedAboutInstancing;
#endif

        void OnEnable()
        {
            isDirty = true;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            batches.Clear();
        }

        void OnValidate() => isDirty = true;

        /// <summary>Tving genopbygning af celler (fx efter re-import).</summary>
        public void Rebuild() => isDirty = true;

        void OnBeginCameraRendering(ScriptableRenderContext context, Camera cam)
        {
            if (cam.cameraType == CameraType.Preview || cam.cameraType == CameraType.Reflection)
                return;
            if (cam.cameraType == CameraType.SceneView && !drawInSceneView)
                return;

            EnsureBuilt();

            Vector3 camPos = cam.transform.position;
            float maxDistSqr = maxDrawDistance > 0f ? maxDrawDistance * maxDrawDistance : float.PositiveInfinity;
            int drawn = 0, calls = 0;

            foreach (var batch in batches)
            {
                RenderParams rp = batch.renderParams;
                rp.camera = cam; // tegn kun for dette kamera (vi kaldes én gang pr. kamera)

                foreach (var cell in batch.cells)
                {
                    // Afstands-culling: afstand fra kamera til cellens bounds (0 hvis kameraet er inde i den).
                    if (cell.bounds.SqrDistance(camPos) > maxDistSqr)
                        continue;

                    rp.worldBounds = cell.bounds; // Unity frustum-culler hele kaldet med disse bounds
                    int count = cell.matrices.Length;
                    for (int start = 0; start < count; start += MaxInstancesPerCall)
                    {
                        int n = Mathf.Min(MaxInstancesPerCall, count - start);
                        Graphics.RenderMeshInstanced(rp, batch.type.mesh, batch.type.subMeshIndex, cell.matrices, n, start);
                        calls++;
                    }
                    drawn += count;
                }
            }

            instancesDrawnLastCamera = drawn;
            drawCallsLastCamera = calls;
        }

        void EnsureBuilt()
        {
            bool dataChanged = data != builtData || (data != null && data.ChangeCount != builtChangeCount);
            bool moved = transform.localToWorldMatrix != builtLocalToWorld;
            if (!isDirty && !dataChanged && !moved)
                return;

            Build();
            isDirty = false;
            builtData = data;
            builtChangeCount = data != null ? data.ChangeCount : -1;
            builtLocalToWorld = transform.localToWorldMatrix;
        }

        void Build()
        {
            batches.Clear();
            if (data == null)
                return;

            Matrix4x4 localToWorld = transform.localToWorldMatrix;

            foreach (var type in data.types)
            {
                if (!type.IsRenderable)
                    continue;
                if (!EnsureInstancing(type))
                    continue;

                Matrix4x4 offset = type.OffsetMatrix;
                Bounds meshBounds = type.mesh.GetSubMesh(type.subMeshIndex).bounds;

                // Sortér instances i grid-celler (XZ-planet) baseret på world-position.
                var cellLists = new Dictionary<Vector2Int, List<Matrix4x4>>();
                for (int i = 0; i < type.Count; i++)
                {
                    Matrix4x4 m = localToWorld * Matrix4x4.TRS(type.positions[i], type.rotations[i], type.scales[i]) * offset;
                    Vector3 p = m.GetColumn(3);
                    var key = new Vector2Int(Mathf.FloorToInt(p.x / cellSize), Mathf.FloorToInt(p.z / cellSize));
                    if (!cellLists.TryGetValue(key, out var list))
                        cellLists[key] = list = new List<Matrix4x4>();
                    list.Add(m);
                }

                var batch = new TypeBatch
                {
                    type = type,
                    renderParams = new RenderParams(type.material)
                    {
                        shadowCastingMode = type.castShadows,
                        receiveShadows = type.receiveShadows,
                        layer = gameObject.layer,
                        lightProbeUsage = LightProbeUsage.BlendProbes, // giver ambient-lys (SH) til shaderen
                    },
                };

                foreach (var list in cellLists.Values)
                {
                    var cell = new Cell { matrices = list.ToArray() };
                    cell.bounds = TransformBounds(cell.matrices[0], meshBounds);
                    for (int i = 1; i < cell.matrices.Length; i++)
                        cell.bounds.Encapsulate(TransformBounds(cell.matrices[i], meshBounds));
                    batch.cells.Add(cell);
                }

                batches.Add(batch);
            }
        }

        /// <summary>RenderMeshInstanced kaster en exception, hvis materialet ikke har instancing slået til.</summary>
        bool EnsureInstancing(GrassType type)
        {
            if (type.material.enableInstancing)
                return true;
#if UNITY_EDITOR
            // I editoren slår vi det bare til på materialet (samme som at sætte fluebenet i inspectoren).
            type.material.enableInstancing = true;
            UnityEditor.EditorUtility.SetDirty(type.material);
            Debug.Log($"[GrassInstanceRenderer] Slog 'Enable GPU Instancing' til på materialet '{type.material.name}'.", type.material);
            return true;
#else
            if (!warnedAboutInstancing)
            {
                Debug.LogWarning($"[GrassInstanceRenderer] Materialet '{type.material.name}' har ikke 'Enable GPU Instancing' slået til – græstypen '{type.name}' springes over.", this);
                warnedAboutInstancing = true;
            }
            return false;
#endif
        }

        /// <summary>Transformér en AABB med en matrix (resultatet er en ny, akse-alignet AABB).</summary>
        static Bounds TransformBounds(in Matrix4x4 m, in Bounds b)
        {
            Vector3 center = m.MultiplyPoint3x4(b.center);
            Vector3 e = b.extents;
            Vector3 extents = new Vector3(
                Mathf.Abs(m.m00) * e.x + Mathf.Abs(m.m01) * e.y + Mathf.Abs(m.m02) * e.z,
                Mathf.Abs(m.m10) * e.x + Mathf.Abs(m.m11) * e.y + Mathf.Abs(m.m12) * e.z,
                Mathf.Abs(m.m20) * e.x + Mathf.Abs(m.m21) * e.y + Mathf.Abs(m.m22) * e.z);
            return new Bounds(center, extents * 2f);
        }
    }
}
