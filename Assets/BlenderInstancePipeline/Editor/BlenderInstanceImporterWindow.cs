using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BlenderInstancePipeline.EditorTools
{
    /// <summary>Tools > Blender Instances > Importer...</summary>
    public class BlenderInstanceImporterWindow : EditorWindow
    {
        // Indstillinger gemmes pr. projekt i EditorPrefs.
        static string PrefsKey => "BlenderInstancePipeline.Settings:" + Application.dataPath;

        BlenderImportSettings settings;
        ImportReport lastReport;
        Vector2 scroll;

        [MenuItem("Tools/Blender Instances/Importer...")]
        static void Open() => GetWindow<BlenderInstanceImporterWindow>("Blender Instances");

        [MenuItem("Tools/Blender Instances/Re-importér sidste JSON")]
        static void ReimportLast()
        {
            var s = LoadSettings();
            if (string.IsNullOrEmpty(s.jsonPath))
            {
                Open();
                return;
            }
            Run(s);
        }

        void OnEnable() => settings = LoadSettings();
        void OnDisable() => SaveSettings(settings);

        static BlenderImportSettings LoadSettings()
        {
            var s = new BlenderImportSettings();
            string json = EditorPrefs.GetString(PrefsKey, "");
            if (!string.IsNullOrEmpty(json))
                JsonUtility.FromJsonOverwrite(json, s);
            return s;
        }

        static void SaveSettings(BlenderImportSettings s)
        {
            if (s != null)
                EditorPrefs.SetString(PrefsKey, JsonUtility.ToJson(s));
        }

        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);

            // --- Fil ---------------------------------------------------------------------------
            EditorGUILayout.LabelField("JSON fra Blender", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                settings.jsonPath = EditorGUILayout.TextField(settings.jsonPath);
                if (GUILayout.Button("Vælg...", GUILayout.Width(70)))
                {
                    string dir = File.Exists(settings.jsonPath) ? Path.GetDirectoryName(settings.jsonPath) : "";
                    string picked = EditorUtility.OpenFilePanel("Vælg instances-JSON", dir, "json");
                    if (!string.IsNullOrEmpty(picked))
                        settings.jsonPath = picked;
                }
            }

            // --- Sortering / assets ------------------------------------------------------------
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Prefabs og græs", EditorStyles.boldLabel);
            settings.prefabSearchFolder = FolderField("Prefab-mappe", settings.prefabSearchFolder);
            settings.grassPrefix = EditorGUILayout.TextField(new GUIContent("Græs-prefix", Tip(nameof(settings.grassPrefix))), settings.grassPrefix);
            settings.grassAssetFolder = FolderField("Græs-asset mappe", settings.grassAssetFolder);

            var fallback = AssetDatabase.LoadAssetAtPath<Material>(settings.fallbackGrassMaterialPath);
            fallback = (Material)EditorGUILayout.ObjectField(
                new GUIContent("Fallback græs-materiale", Tip(nameof(settings.fallbackGrassMaterialPath))),
                fallback, typeof(Material), false);
            settings.fallbackGrassMaterialPath = fallback ? AssetDatabase.GetAssetPath(fallback) : "";

            settings.rootName = EditorGUILayout.TextField(new GUIContent("Parent-navn", Tip(nameof(settings.rootName))), settings.rootName);
            settings.stripBlenderSuffix = EditorGUILayout.Toggle(new GUIContent("Fjern .001-suffiks", Tip(nameof(settings.stripBlenderSuffix))), settings.stripBlenderSuffix);
            settings.autoCreatePrefabs = EditorGUILayout.Toggle(new GUIContent("Opret manglende prefabs fra FBX", Tip(nameof(settings.autoCreatePrefabs))), settings.autoCreatePrefabs);
            settings.staticFlags = (StaticEditorFlags)EditorGUILayout.EnumFlagsField("Static flags", settings.staticFlags);

            // --- Koordinater -------------------------------------------------------------------
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Koordinatkonvertering (Blender Z-up RH → Unity Y-up LH)", EditorStyles.boldLabel);
            settings.axisPreset = (AxisPreset)EditorGUILayout.EnumPopup("Akse-preset", settings.axisPreset);
            if (settings.axisPreset == AxisPreset.Custom)
            {
                EditorGUI.indentLevel++;
                settings.customMapping.unityX = (SignedAxis)EditorGUILayout.EnumPopup("Unity X = Blender", settings.customMapping.unityX);
                settings.customMapping.unityY = (SignedAxis)EditorGUILayout.EnumPopup("Unity Y = Blender", settings.customMapping.unityY);
                settings.customMapping.unityZ = (SignedAxis)EditorGUILayout.EnumPopup("Unity Z = Blender", settings.customMapping.unityZ);
                EditorGUI.indentLevel--;
            }

            AxisMapping mapping = settings.Mapping;
            int det = mapping.Determinant;
            var msgType = det == -1 ? MessageType.Info : det == 0 ? MessageType.Error : MessageType.Warning;
            string detText = det == -1 ? "skifter håndethed korrekt"
                           : det == 0 ? "UGYLDIG – samme Blender-akse bruges to gange"
                           : "skifter IKKE håndethed – alt bliver spejlvendt";
            EditorGUILayout.HelpBox($"{mapping}\n{detText}", msgType);

            settings.positionScale = EditorGUILayout.FloatField(new GUIContent("Positions-skala", Tip(nameof(settings.positionScale))), settings.positionScale);
            settings.compensatePrefabRoot = EditorGUILayout.Toggle(new GUIContent("Kompensér prefab-root", Tip(nameof(settings.compensatePrefabRoot))), settings.compensatePrefabRoot);

            // --- Import ------------------------------------------------------------------------
            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(!File.Exists(settings.jsonPath) || det == 0))
            {
                if (GUILayout.Button("Importér (sletter og spawner på ny)", GUILayout.Height(32)))
                {
                    SaveSettings(settings);
                    lastReport = Run(settings);
                    GUIUtility.ExitGUI();
                }
            }

            DrawReport();
            EditorGUILayout.EndScrollView();
        }

        static ImportReport Run(BlenderImportSettings s)
        {
            ImportReport report;
            try
            {
                report = BlenderInstanceImporter.Import(s);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorUtility.DisplayDialog("Blender Instances – import fejlede", e.Message, "OK");
                return null;
            }

            Selection.activeGameObject = report.root;

            // Tydelig advarsel om manglende prefabs.
            if (report.missingPrefabs.Count > 0)
            {
                string list = string.Join("\n", report.missingPrefabs.Select(kv => $"  • {kv.Key}  ({kv.Value} instances)"));
                EditorUtility.DisplayDialog("Manglende prefabs",
                    $"{report.MissingInstanceCount} instances blev IKKE spawnet, fordi der ikke findes en prefab med samme navn " +
                    $"under '{s.prefabSearchFolder}':\n\n{list}\n\nSe også Console.", "OK");
            }
            return report;
        }

        void DrawReport()
        {
            if (lastReport == null)
                return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Sidste import", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                $"Prefab-objekter: {lastReport.spawnedObjects}\n" +
                string.Join("\n", lastReport.spawnedPerPrefab.Select(kv => $"   {kv.Key}: {kv.Value}")) +
                $"\nGræs-instances: {lastReport.grassInstances}\n" +
                string.Join("\n", lastReport.grassPerType.Select(kv => $"   {kv.Key}: {kv.Value}")) +
                $"\nTid: {lastReport.seconds:0.0}s", MessageType.Info);

            if (lastReport.missingPrefabs.Count > 0)
                EditorGUILayout.HelpBox("MANGLENDE PREFABS (ikke spawnet):\n" +
                    string.Join("\n", lastReport.missingPrefabs.Select(kv => $"   {kv.Key}: {kv.Value} instances")), MessageType.Error);

            foreach (string w in lastReport.warnings)
                EditorGUILayout.HelpBox(w, MessageType.Warning);
        }

        static string FolderField(string label, string value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                value = EditorGUILayout.TextField(label, value);
                if (GUILayout.Button("...", GUILayout.Width(30)))
                {
                    string abs = EditorUtility.OpenFolderPanel(label, Application.dataPath, "");
                    string projectRoot = Path.GetDirectoryName(Application.dataPath)?.Replace('\\', '/') + "/";
                    abs = abs.Replace('\\', '/');
                    if (abs.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
                        value = abs.Substring(projectRoot.Length);
                    else if (!string.IsNullOrEmpty(abs))
                        Debug.LogWarning("Mappen skal ligge inde i projektets Assets-mappe.");
                }
            }
            return value;
        }

        /// <summary>Genbruger [Tooltip] fra BlenderImportSettings som tooltip i vinduet.</summary>
        static string Tip(string field)
        {
            var f = typeof(BlenderImportSettings).GetField(field);
            var attr = f != null ? (TooltipAttribute)Attribute.GetCustomAttribute(f, typeof(TooltipAttribute)) : null;
            return attr != null ? attr.tooltip : "";
        }
    }
}
