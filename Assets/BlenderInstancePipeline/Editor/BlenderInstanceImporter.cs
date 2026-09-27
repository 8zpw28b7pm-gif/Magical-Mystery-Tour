using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BlenderInstancePipeline.EditorTools
{
    /// <summary>Alle indstillinger for en import. Gemmes i EditorPrefs af vinduet.</summary>
    [Serializable]
    public class BlenderImportSettings
    {
        public string jsonPath = "";

        [Tooltip("Mappe der søges i efter prefabs med samme navn som kildeobjektet")]
        public string prefabSearchFolder = "Assets";

        [Tooltip("Kildeobjekter hvis navn starter med dette bliver til græs-data i stedet for GameObjects")]
        public string grassPrefix = "Grass_";

        [Tooltip("Hvor græs-asset'et gemmes (<json-navn>_Grass.asset)")]
        public string grassAssetFolder = "Assets/BlenderInstances";

        [Tooltip("Asset-sti til materiale der bruges til græstyper uden prefab/materiale")]
        public string fallbackGrassMaterialPath = "";

        [Tooltip("Navn på parent-objektet. Tomt = 'Blender Instances (<json-navn>)'")]
        public string rootName = "";

        public AxisPreset axisPreset = AxisPreset.FbxDefault;
        public AxisMapping customMapping = new AxisMapping(SignedAxis.NegX, SignedAxis.PosZ, SignedAxis.NegY);

        [Tooltip("Ganges på alle positioner (fx 0.01 hvis Blender-scenen er i cm)")]
        public float positionScale = 1f;

        [Tooltip("Læg instance-transformen oven på prefab'ens egen root-transform (fx -90° X fra FBX)")]
        public bool compensatePrefabRoot = true;

        [Tooltip("'Tree_Oak.001' matches mod prefab'en 'Tree_Oak', hvis der ikke findes en prefab med det eksakte navn")]
        public bool stripBlenderSuffix = true;

        [Tooltip("Mangler en prefab, men findes et objekt med samme navn i en FBX i prefab-mappen, " +
                 "oprettes prefab'en automatisk derfra (kun hvis den ikke findes i forvejen)")]
        public bool autoCreatePrefabs = true;

        public StaticEditorFlags staticFlags = DefaultStaticFlags;

        public const StaticEditorFlags DefaultStaticFlags =
            StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic |
            StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic;

        public AxisMapping Mapping => AxisMapping.FromPreset(axisPreset, customMapping);
        public string SourceKey => Path.GetFileNameWithoutExtension(jsonPath);
        public string ResolvedRootName => string.IsNullOrWhiteSpace(rootName) ? $"Blender Instances ({SourceKey})" : rootName;
    }

    /// <summary>Resultatet af en import – vises i vinduet og logges.</summary>
    public class ImportReport
    {
        public GameObject root;
        public GrassInstanceData grassAsset;
        public int spawnedObjects;
        public int grassInstances;
        public readonly SortedDictionary<string, int> spawnedPerPrefab = new SortedDictionary<string, int>();
        public readonly SortedDictionary<string, int> grassPerType = new SortedDictionary<string, int>();
        public readonly SortedDictionary<string, int> missingPrefabs = new SortedDictionary<string, int>();
        public readonly List<string> warnings = new List<string>();
        public double seconds;

        public int MissingInstanceCount => missingPrefabs.Values.Sum();
    }

    public static class BlenderInstanceImporter
    {
        static readonly Regex BlenderSuffix = new Regex(@"\.\d{3,}$");

        public static ImportReport Import(BlenderImportSettings s)
        {
            var report = new ImportReport();
            var started = DateTime.Now;

            // --- 1. Læs og validér JSON -------------------------------------------------------
            if (string.IsNullOrEmpty(s.jsonPath) || !File.Exists(s.jsonPath))
                throw new FileNotFoundException("JSON-filen findes ikke", s.jsonPath);

            var file = JsonUtility.FromJson<BlenderInstanceFile>(File.ReadAllText(s.jsonPath));
            if (file == null || file.format != BlenderInstanceFile.ExpectedFormat)
                throw new InvalidDataException("Filen er ikke en 'blender-instances' JSON fra unity_instance_exporter.py");
            if (file.stride != BlenderInstanceFile.ExpectedStride)
                throw new InvalidDataException($"Uventet stride {file.stride} (forventede {BlenderInstanceFile.ExpectedStride})");

            AxisMapping mapping = s.Mapping;
            int det = mapping.Determinant;
            if (det == 0)
                throw new InvalidOperationException($"Ugyldig akse-mapping ({mapping}): samme Blender-akse bruges to gange.");
            if (det > 0)
                report.warnings.Add($"Akse-mappingen {mapping} skifter ikke håndethed – resultatet bliver spejlvendt.");

            if (file.version < 2)
                report.warnings.Add("JSON-filen er lavet med en ældre version af Blender-exporteren (version 1). " +
                                    "Hvis kildeobjekterne i Blender ikke står i origo med rotation 0 og skala 1, bliver " +
                                    "objekterne roteret/skaleret forkert. Eksportér igen med den nyeste unity_instance_exporter.py.");

            if (Math.Abs(file.unit_scale - 1f) > 1e-4f && Math.Abs(s.positionScale - 1f) < 1e-6f)
                report.warnings.Add($"Blender-scenen har Unit Scale = {file.unit_scale}. Positioner er i Blender units; " +
                                    "justér 'Positions-skala', hvis tingene lander forkert i forhold til terrænet.");

            var prefabs = BuildPrefabLookup(s.prefabSearchFolder, report.warnings);

            // --- 2. Find/opret parent og ryd gamle objekter (re-import uden dubletter) ------
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Import Blender Instances");

            try
            {
                BlenderInstanceRoot root = FindRoot(s.SourceKey);
                if (root == null)
                {
                    var go = new GameObject(s.ResolvedRootName);
                    Undo.RegisterCreatedObjectUndo(go, "Create Blender Instances root");
                    root = Undo.AddComponent<BlenderInstanceRoot>(go);
                    root.sourceKey = s.SourceKey;
                }
                else
                {
                    Undo.RecordObject(root.gameObject, "Rename root");
                    root.gameObject.name = s.ResolvedRootName;
                    for (int i = root.transform.childCount - 1; i >= 0; i--)
                        Undo.DestroyObjectImmediate(root.transform.GetChild(i).gameObject);
                }
                report.root = root.gameObject;

                // --- 3. Sortér grupper: græs → data, alt andet → prefabs ---------------------
                var grass = new Dictionary<string, List<ConvertedTransform>>();
                int processed = 0;

                foreach (var group in file.groups ?? Array.Empty<BlenderInstanceGroup>())
                {
                    string key = ResolveKey(group.source, prefabs, s, report.warnings);
                    int count = group.data != null ? group.data.Length / file.stride : 0;
                    if (count != group.count)
                        report.warnings.Add($"'{group.source}': count={group.count}, men data indeholder {count} instances.");

                    if (!string.IsNullOrEmpty(s.grassPrefix) && key.StartsWith(s.grassPrefix, StringComparison.Ordinal))
                    {
                        if (!grass.TryGetValue(key, out var list))
                            grass[key] = list = new List<ConvertedTransform>();
                        for (int i = 0; i < count; i++)
                            list.Add(ReadInstance(group.data, i * file.stride, mapping, s.positionScale));
                        continue;
                    }

                    if (!prefabs.TryGetValue(key, out GameObject prefab))
                    {
                        report.missingPrefabs.TryGetValue(key, out int missing);
                        report.missingPrefabs[key] = missing + count;
                        continue;
                    }

                    SpawnGroup(root.transform, key, prefab, group.data, file.stride, count, mapping, s, report, ref processed, file.total_instances);
                }

                // --- 4. Græs → ScriptableObject + renderer ------------------------------------
                ImportGrass(root, grass, prefabs, s, report);

                // --- 5. Opdatér rod-info og markér scenen som ændret ----------------------------
                Undo.RecordObject(root, "Update root info");
                root.lastJsonPath = s.jsonPath;
                root.lastImportTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                root.spawnedObjects = report.spawnedObjects;
                root.grassInstances = report.grassInstances;
                EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                Undo.CollapseUndoOperations(undoGroup);
            }

            report.seconds = (DateTime.Now - started).TotalSeconds;
            LogReport(report, s);
            return report;
        }

        // ------------------------------------------------------------------------------------
        // Prefabs
        // ------------------------------------------------------------------------------------

        static void SpawnGroup(Transform root, string key, GameObject prefab, float[] data, int stride, int count,
                               AxisMapping mapping, BlenderImportSettings s, ImportReport report,
                               ref int processed, int total)
        {
            // Én container pr. prefab-type holder hierarkiet overskueligt. Hvis flere Blender-navne
            // ender på samme prefab (fx 'Rock.001' og 'Rock.002' → 'Rock'), genbruges containeren.
            Transform existing = root.Find(key);
            bool isNewContainer = existing == null;
            GameObject container = isNewContainer ? new GameObject(key) : existing.gameObject;
            if (isNewContainer)
            {
                container.transform.SetParent(root, false);
                GameObjectUtility.SetStaticEditorFlags(container, s.staticFlags);
            }

            for (int i = 0; i < count; i++)
            {
                ConvertedTransform t = ReadInstance(data, i * stride, mapping, s.positionScale);
                Vector3 pos = t.position;
                Quaternion rot = t.rotation;
                Vector3 scale = t.scale;
                if (s.compensatePrefabRoot)
                    BlenderToUnity.ComposeWithPrefabRoot(t.position, t.rotation, t.scale, prefab.transform, out pos, out rot, out scale);

                // InstantiatePrefab (ikke Object.Instantiate) => objektet forbliver koblet til prefab'en.
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, container.transform);
                go.transform.SetLocalPositionAndRotation(pos, rot);
                go.transform.localScale = scale;

                foreach (var child in go.GetComponentsInChildren<Transform>(true))
                    GameObjectUtility.SetStaticEditorFlags(child.gameObject, s.staticFlags);

                if (++processed % 250 == 0)
                    EditorUtility.DisplayProgressBar("Importerer Blender instances", $"{key} ({i + 1}/{count})",
                                                     total > 0 ? (float)processed / total : 0f);
            }

            // Registrér containeren EFTER den er fyldt: ét undo-trin for hele gruppen (meget hurtigere
            // end at registrere tusindvis af objekter enkeltvis).
            if (isNewContainer)
                Undo.RegisterCreatedObjectUndo(container, "Spawn " + key);

            report.spawnedObjects += count;
            report.spawnedPerPrefab.TryGetValue(key, out int already);
            report.spawnedPerPrefab[key] = already + count;
        }

        /// <summary>
        /// Finder prefab-nøglen for et Blender-navn, i denne rækkefølge:
        ///   1. prefab med det eksakte navn ('tree.002' → 'tree.002.prefab')
        ///   2. (auto) opret prefab'en fra et objekt med det eksakte navn i en FBX i prefab-mappen
        ///   3. (suffiks) samme to trin uden Blenders .001-suffiks ('Tree_Oak.001' → 'Tree_Oak')
        /// Et eksakt navn vinder altså altid, så 'tree' og 'tree.002' kan være forskellige prefabs.
        /// Oprettede prefabs lægges i <paramref name="prefabs"/>.
        /// </summary>
        static string ResolveKey(string source, Dictionary<string, GameObject> prefabs, BlenderImportSettings s, List<string> notes)
        {
            if (TryGetOrCreate(source, prefabs, s, notes))
                return source;
            if (s.stripBlenderSuffix)
            {
                string stripped = BlenderSuffix.Replace(source, "");
                if (stripped != source && TryGetOrCreate(stripped, prefabs, s, notes))
                    return stripped;
            }
            return source; // ingen prefab – rapporteres som manglende med det originale navn
        }

        static bool TryGetOrCreate(string name, Dictionary<string, GameObject> prefabs, BlenderImportSettings s, List<string> notes)
        {
            if (prefabs.ContainsKey(name))
                return true;
            if (!s.autoCreatePrefabs)
                return false;
            GameObject created = CreatePrefabFromModelPart(name, s.prefabSearchFolder, notes);
            if (created == null)
                return false;
            prefabs[name] = created;
            return true;
        }

        /// <summary>
        /// Finder alle prefabs (og FBX-modeller) i mappen, indekseret på filnavn.
        /// Rigtige .prefab-filer vinder over FBX-modeller med samme navn.
        /// </summary>
        static Dictionary<string, GameObject> BuildPrefabLookup(string folder, List<string> warnings)
        {
            var result = new Dictionary<string, GameObject>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder))
            {
                warnings.Add($"Prefab-mappen '{folder}' findes ikke – ingen prefabs kan matches.");
                return result;
            }

            var paths = AssetDatabase.FindAssets("t:Prefab t:Model", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct()
                .OrderBy(p => p.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(p => p, StringComparer.Ordinal);

            var isPrefabFile = new Dictionary<string, string>();
            foreach (string path in paths)
            {
                string name = Path.GetFileNameWithoutExtension(path);
                bool prefabFile = path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase);
                if (result.ContainsKey(name))
                {
                    if (prefabFile && isPrefabFile.TryGetValue(name, out string first))
                        warnings.Add($"Flere prefabs hedder '{name}': bruger '{first}', ignorerer '{path}'.");
                    continue;
                }
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null)
                    continue;
                result[name] = asset;
                if (prefabFile)
                    isPrefabFile[name] = path;
            }
            return result;
        }

        /// <summary>
        /// Opretter prefab'en '&lt;navn&gt;.prefab' ud fra et objekt med samme navn inde i en FBX/model
        /// i prefab-mappen (fx 'tree' inde i 'Instance_test.fbx'). Modellen instantieres, pakkes ud,
        /// og objektet gemmes som ny prefab med sin world-transform fra FBX'en (inkl. akse-korrektion).
        /// Mesh og materialer peger stadig ind i FBX'en, så en ny FBX-eksport slår igennem.
        /// </summary>
        static GameObject CreatePrefabFromModelPart(string name, string folder, List<string> notes)
        {
            if (string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder) ||
                name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                return null;

            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { folder }))
            {
                string modelPath = AssetDatabase.GUIDToAssetPath(guid);
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                if (model == null || FindDeep(model.transform, name) == null)
                    continue;

                var temp = (GameObject)PrefabUtility.InstantiatePrefab(model);
                try
                {
                    PrefabUtility.UnpackPrefabInstance(temp, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    Transform part = FindDeep(temp.transform, name);
                    if (part != temp.transform)
                        part.SetParent(null, true); // behold world-transformen fra FBX'en

                    string path = $"{folder.TrimEnd('/')}/{name}.prefab";
                    GameObject prefab = PrefabUtility.SaveAsPrefabAsset(part.gameObject, path, out bool ok);
                    if (part != temp.transform)
                        UnityEngine.Object.DestroyImmediate(part.gameObject);
                    if (ok)
                    {
                        notes.Add($"Oprettede prefab'en '{path}' ud fra objektet '{name}' i '{modelPath}'. " +
                                  "Slet den, hvis den skal genskabes efter en ny FBX-eksport med ændrede transforms.");
                        return prefab;
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(temp);
                }
            }
            return null;
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name)
                return t;
            foreach (Transform child in t)
            {
                Transform found = FindDeep(child, name);
                if (found != null)
                    return found;
            }
            return null;
        }

        static BlenderInstanceRoot FindRoot(string sourceKey)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                    continue;
                foreach (var go in scene.GetRootGameObjects())
                    foreach (var r in go.GetComponentsInChildren<BlenderInstanceRoot>(true))
                        if (r.sourceKey == sourceKey)
                            return r;
            }
            return null;
        }

        // ------------------------------------------------------------------------------------
        // Græs
        // ------------------------------------------------------------------------------------

        /// <summary>En instance-transform konverteret til Unity-koordinater (før evt. prefab-kompensation).</summary>
        struct ConvertedTransform
        {
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 scale;
        }

        /// <summary>Læser én instance (10 floats) og konverterer den til Unity-koordinater.</summary>
        static ConvertedTransform ReadInstance(float[] d, int o, AxisMapping mapping, float positionScale)
        {
            return new ConvertedTransform
            {
                position = BlenderToUnity.Position(new Vector3(d[o], d[o + 1], d[o + 2]), mapping) * positionScale,
                rotation = BlenderToUnity.Rotation(d[o + 3], d[o + 4], d[o + 5], d[o + 6], mapping),
                scale = BlenderToUnity.Scale(new Vector3(d[o + 7], d[o + 8], d[o + 9]), mapping),
            };
        }

        static void ImportGrass(BlenderInstanceRoot root, Dictionary<string, List<ConvertedTransform>> grass,
                                Dictionary<string, GameObject> prefabs, BlenderImportSettings s, ImportReport report)
        {
            string assetPath = $"{s.grassAssetFolder.TrimEnd('/')}/{s.SourceKey}_Grass.asset";
            var asset = AssetDatabase.LoadAssetAtPath<GrassInstanceData>(assetPath);

            // Intet græs og intet eksisterende asset => intet at gøre.
            if (grass.Count == 0 && asset == null)
                return;

            if (asset == null)
            {
                EnsureFolder(s.grassAssetFolder);
                asset = ScriptableObject.CreateInstance<GrassInstanceData>();
                AssetDatabase.CreateAsset(asset, assetPath);
            }

            // Bemærk: asset-ændringer registreres ikke i Undo (et snapshot af store græs-arrays er tungt).
            var fallbackMaterial = string.IsNullOrEmpty(s.fallbackGrassMaterialPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<Material>(s.fallbackGrassMaterialPath);

            // Fjern typer der ikke længere findes i Blender-scenen.
            foreach (var removed in asset.types.Where(t => !grass.ContainsKey(t.name)).ToList())
            {
                report.warnings.Add($"Græstypen '{removed.name}' findes ikke længere i JSON og er fjernet fra asset'et.");
                asset.types.Remove(removed);
            }

            foreach (var kv in grass.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                // Eksisterende type beholder mesh/materiale/offset/skygge-indstillinger ved re-import.
                GrassType type = asset.Find(kv.Key);
                if (type == null)
                {
                    type = new GrassType { name = kv.Key };
                    asset.types.Add(type);
                }

                type.positions = kv.Value.Select(g => g.position).ToArray();
                type.rotations = kv.Value.Select(g => g.rotation).ToArray();
                type.scales = kv.Value.Select(g => g.scale).ToArray();

                AutoAssignMesh(type, prefabs, s.compensatePrefabRoot);
                if (type.material == null)
                    type.material = fallbackMaterial;

                // Kun .mat-filer under Assets/ kan gemme 'Enable GPU Instancing'. Materialer indlejret i
                // en FBX eller fra en pakke (fx URP's standard 'Lit') er skrivebeskyttede.
                string matPath = type.material != null ? AssetDatabase.GetAssetPath(type.material) : "";
                bool readOnly = type.material != null &&
                                !(matPath.StartsWith("Assets/", StringComparison.Ordinal) &&
                                  matPath.EndsWith(".mat", StringComparison.OrdinalIgnoreCase));
                if (readOnly && fallbackMaterial != null)
                {
                    type.material = fallbackMaterial;
                    readOnly = false;
                }
                else if (readOnly)
                {
                    report.warnings.Add($"Græs-materialet '{type.material.name}' ligger i '{matPath}' og er skrivebeskyttet " +
                                        "(indlejret i FBX'en eller fra en pakke), så 'Enable GPU Instancing' kan ikke gemmes. " +
                                        "Vælg et 'Fallback græs-materiale' i vinduet, eller udtræk FBX'ens materialer " +
                                        "(vælg FBX'en › Inspector › Materials › Extract Materials...).");
                }

                if (type.material != null && !type.material.enableInstancing && !readOnly)
                {
                    type.material.enableInstancing = true; // krævet af Graphics.RenderMeshInstanced
                    EditorUtility.SetDirty(type.material);
                    report.warnings.Add($"Slog 'Enable GPU Instancing' til på materialet '{type.material.name}'.");
                }

                if (type.mesh == null || type.material == null)
                    report.warnings.Add($"Græstypen '{type.name}' mangler {(type.mesh == null ? "mesh" : "materiale")} – " +
                                        $"lav en prefab med navnet '{type.name}', eller udfyld feltet i {assetPath}.");

                report.grassInstances += type.Count;
                report.grassPerType[type.name] = type.Count;
            }

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
            asset.NotifyChanged();
            report.grassAsset = asset;

            // Rendereren sidder på rod-objektet, så den overlever at børnene slettes ved re-import.
            var renderer = root.GetComponent<GrassInstanceRenderer>();
            if (renderer == null)
                renderer = Undo.AddComponent<GrassInstanceRenderer>(root.gameObject);
            Undo.RecordObject(renderer, "Assign grass data");
            renderer.data = asset;
            renderer.Rebuild();
        }

        /// <summary>
        /// Henter mesh + materiale fra en prefab/FBX med samme navn som græstypen (fx "Grass_Tall").
        /// Offset'et er MeshFilter-objektets transform i prefab'en (inkl. root-rotation fra FBX),
        /// så græsset står op på samme måde som prefab'en gør.
        /// Findes prefab'en, er den "sandheden": mesh og offset opdateres ved hver import
        /// (så en ny FBX-eksport slår igennem). Materialet sættes kun, hvis feltet er tomt.
        /// </summary>
        static void AutoAssignMesh(GrassType type, Dictionary<string, GameObject> prefabs, bool useOffset)
        {
            if (!prefabs.TryGetValue(type.name, out GameObject prefab))
                return;

            var filter = prefab.GetComponentInChildren<MeshFilter>(true);
            if (filter == null)
                return;

            if (filter.sharedMesh != null)
            {
                type.mesh = filter.sharedMesh;
                Matrix4x4 m = useOffset ? filter.transform.localToWorldMatrix : Matrix4x4.identity;
                type.offsetPosition = m.GetColumn(3);
                type.offsetRotation = m.rotation.eulerAngles;
                type.offsetScale = m.lossyScale;
            }

            if (type.material == null && filter.TryGetComponent(out MeshRenderer mr))
                type.material = mr.sharedMaterial;
        }

        static void EnsureFolder(string folder)
        {
            folder = folder.TrimEnd('/');
            if (AssetDatabase.IsValidFolder(folder))
                return;
            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        // ------------------------------------------------------------------------------------

        static void LogReport(ImportReport r, BlenderImportSettings s)
        {
            string perPrefab = string.Join(", ", r.spawnedPerPrefab.Select(kv => $"{kv.Key}: {kv.Value}"));
            string perGrass = string.Join(", ", r.grassPerType.Select(kv => $"{kv.Key}: {kv.Value}"));
            Debug.Log($"[Blender Instances] Import af '{Path.GetFileName(s.jsonPath)}' færdig på {r.seconds:0.0}s. " +
                      $"Prefabs: {r.spawnedObjects} ({perPrefab}). Græs: {r.grassInstances} ({perGrass}).", r.root);

            foreach (var kv in r.missingPrefabs)
                Debug.LogWarning($"[Blender Instances] MANGLENDE PREFAB: '{kv.Key}' ({kv.Value} instances blev IKKE spawnet). " +
                                 $"Lav en prefab med præcis navnet '{kv.Key}' under '{s.prefabSearchFolder}'.");

            foreach (string w in r.warnings)
                Debug.LogWarning("[Blender Instances] " + w);
        }
    }
}
