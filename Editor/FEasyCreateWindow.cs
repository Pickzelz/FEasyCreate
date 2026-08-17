using System.IO;
using UnityEditor;
using UnityEngine;

namespace FEasyCreate.Editor
{
    /// <summary>
    /// Window utama FEasyCreate (Tools ▸ FEasyCreate). Kiri = daftar preset (CRUD),
    /// kanan = editor preset + tombol Create untuk membuat semua file sekaligus.
    /// </summary>
    public class FEasyCreateWindow : EditorWindow
    {
        private FEasyCreateSettings _settings;
        private int _selected = -1;
        private Vector2 _leftScroll, _rightScroll;

        [MenuItem("Tools/FEasyCreate")]
        public static void Open()
        {
            var w = GetWindow<FEasyCreateWindow>("FEasyCreate");
            w.minSize = new Vector2(720, 420);
        }

        private void OnEnable()
        {
            _settings = FEasyCreateSettings.GetOrCreate();
            if (_settings.presets.Count > 0 && _selected < 0) _selected = 0;
        }

        private void OnDisable()
        {
            if (_settings != null) AssetDatabase.SaveAssets();
        }

        private void OnGUI()
        {
            if (_settings == null) { OnEnable(); return; }

            EditorGUILayout.BeginHorizontal();
            DrawPresetList();
            DrawPresetEditor();
            EditorGUILayout.EndHorizontal();
        }

        // ---------------- kiri: daftar preset ---------------- //

        private void DrawPresetList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(220));
            EditorGUILayout.LabelField("Presets", EditorStyles.boldLabel);

            _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll, "box");
            for (int i = 0; i < _settings.presets.Count; i++)
            {
                bool on = i == _selected;
                var style = on ? EditorStyles.toolbarButton : EditorStyles.label;
                if (GUILayout.Toggle(on, _settings.presets[i].presetName, style) && !on)
                {
                    _selected = i;
                    GUI.FocusControl(null);
                }
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("＋ Add"))
            {
                Undo.RecordObject(_settings, "Add Preset");
                _settings.presets.Add(new CreatePreset());
                _selected = _settings.presets.Count - 1;
                MarkDirty();
            }
            using (new EditorGUI.DisabledScope(!HasSelection))
            {
                if (GUILayout.Button("Duplicate"))
                {
                    Undo.RecordObject(_settings, "Duplicate Preset");
                    _settings.presets.Insert(_selected + 1, _settings.presets[_selected].Clone());
                    _selected++;
                    MarkDirty();
                }
                if (GUILayout.Button("Delete") &&
                    EditorUtility.DisplayDialog("Delete Preset",
                        $"Hapus preset '{_settings.presets[_selected].presetName}'?", "Hapus", "Batal"))
                {
                    Undo.RecordObject(_settings, "Delete Preset");
                    _settings.presets.RemoveAt(_selected);
                    _selected = Mathf.Clamp(_selected - 1, -1, _settings.presets.Count - 1);
                    MarkDirty();
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        // ---------------- kanan: editor preset ---------------- //

        private void DrawPresetEditor()
        {
            EditorGUILayout.BeginVertical();
            if (!HasSelection)
            {
                EditorGUILayout.HelpBox("Pilih atau buat sebuah preset di kiri.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            var preset = _settings.presets[_selected];
            _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.LabelField("Preset", EditorStyles.boldLabel);
            preset.presetName    = EditorGUILayout.TextField("Preset Name", preset.presetName);
            preset.baseName      = EditorGUILayout.TextField(new GUIContent("Base Name", "Mengisi token {name} di tiap file, mis. berry."), preset.baseName);
            DrawFolderField("Default Location", ref preset.defaultLocation);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Files", EditorStyles.boldLabel);

            int removeAt = -1;
            for (int i = 0; i < preset.files.Count; i++)
            {
                if (DrawFileEntry(preset, preset.files[i], i)) removeAt = i;
            }
            if (removeAt >= 0)
            {
                Undo.RecordObject(_settings, "Remove File Entry");
                preset.files.RemoveAt(removeAt);
            }

            if (GUILayout.Button("＋ Add File"))
            {
                Undo.RecordObject(_settings, "Add File Entry");
                preset.files.Add(new FileEntry());
            }

            if (EditorGUI.EndChangeCheck()) MarkDirty();

            EditorGUILayout.Space(12);
            using (new EditorGUI.DisabledScope(preset.files.Count == 0))
            {
                GUI.backgroundColor = new Color(0.55f, 0.85f, 0.55f);
                if (GUILayout.Button($"Create {preset.files.Count} File(s)", GUILayout.Height(32)))
                    RunCreate(preset);
                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        /// <summary>Gambar satu baris entri file. Return true bila tombol hapus ditekan.</summary>
        private bool DrawFileEntry(CreatePreset preset, FileEntry entry, int index)
        {
            bool remove = false;
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"#{index + 1}", EditorStyles.miniBoldLabel, GUILayout.Width(28));
            entry.kind = (ECreateKind)EditorGUILayout.EnumPopup(entry.kind, GUILayout.Width(140));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("✕", GUILayout.Width(24))) remove = true;
            EditorGUILayout.EndHorizontal();

            // Field sesuai kind (Auto menampilkan semua supaya bisa ditebak).
            bool showSO   = entry.kind == ECreateKind.Auto || entry.kind == ECreateKind.ScriptableObject;
            bool showVar  = entry.kind == ECreateKind.Auto || entry.kind == ECreateKind.PrefabVariant;
            bool showComp = entry.kind == ECreateKind.Auto || entry.kind == ECreateKind.EmptyPrefab;

            if (showSO)
                entry.className = EditorGUILayout.TextField(new GUIContent("Class Name", "Nama class ScriptableObject, mis. PlantData."), entry.className);
            if (showVar)
                entry.sourcePrefab = EditorGUILayout.ObjectField(new GUIContent("Source Prefab", "Prefab sumber untuk dibuat variant-nya."), entry.sourcePrefab, typeof(GameObject), false);
            if (showComp)
                entry.componentClassName = EditorGUILayout.TextField(new GUIContent("Component", "(Opsional) Component untuk prefab kosong."), entry.componentClassName);

            entry.namePattern = EditorGUILayout.TextField(new GUIContent("Name Pattern", "Pakai {name} untuk Base Name, mis. {name}_plant."), entry.namePattern);
            DrawFolderField("File Location", ref entry.fileLocation, preset.defaultLocation);

            // Preview nama file akhir.
            string preview = FEasyCreateGenerator.ResolveName(preset.baseName, entry.namePattern);
            EditorGUILayout.LabelField(" ", $"→ {preview}", EditorStyles.miniLabel);

            EditorGUILayout.EndVertical();
            return remove;
        }

        // ---------------- helper ---------------- //

        private void DrawFolderField(string label, ref string value, string fallbackHint = null)
        {
            EditorGUILayout.BeginHorizontal();
            string tip = fallbackHint != null ? $"Kosong = pakai Default Location ({fallbackHint})." : "Folder di dalam Assets.";
            value = EditorGUILayout.TextField(new GUIContent(label, tip), value);
            if (GUILayout.Button("…", GUILayout.Width(28)))
            {
                string start = string.IsNullOrEmpty(value) ? "Assets" : value;
                string abs = EditorUtility.OpenFolderPanel(label, start, "");
                if (!string.IsNullOrEmpty(abs))
                {
                    string rel = ToAssetRelative(abs);
                    if (rel != null) value = rel;
                    else EditorUtility.DisplayDialog("Folder tidak valid", "Pilih folder di dalam Assets/ project ini.", "OK");
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private static string ToAssetRelative(string absolute)
        {
            absolute = absolute.Replace('\\', '/');
            string dataPath = Application.dataPath.Replace('\\', '/'); // .../Assets
            if (absolute == dataPath) return "Assets";
            if (absolute.StartsWith(dataPath + "/")) return "Assets" + absolute.Substring(dataPath.Length);
            return null;
        }

        private void RunCreate(CreatePreset preset)
        {
            AssetDatabase.SaveAssets();
            var result = FEasyCreateGenerator.Generate(preset);

            if (result.warnings.Count > 0)
                Debug.LogWarning("[FEasyCreate] " + string.Join("\n  ", result.warnings.ToArray()));

            if (result.AnyCreated)
                Debug.Log($"[FEasyCreate] Membuat {result.created.Count} file untuk preset '{preset.presetName}'.");
            else
                EditorUtility.DisplayDialog("FEasyCreate", "Tidak ada file yang dibuat. Cek Console untuk detailnya.", "OK");
        }

        private bool HasSelection => _selected >= 0 && _selected < _settings.presets.Count;

        private void MarkDirty()
        {
            EditorUtility.SetDirty(_settings);
        }
    }
}
