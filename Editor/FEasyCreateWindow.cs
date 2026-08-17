using UnityEditor;
using UnityEngine;

namespace FEasyCreate.Editor
{
    /// <summary>
    /// Window setting FEasyCreate (Tools ▸ FEasyCreate). Hanya untuk mengatur preset (CRUD).
    /// Menjalankan-nya lewat klik-kanan Project ▸ Create ▸ Easy Create ▸ [Preset].
    /// </summary>
    public class FEasyCreateWindow : EditorWindow
    {
        private FEasyCreateSettings _settings;
        private int _selected = -1;
        private Vector2 _leftScroll, _rightScroll;
        private bool _menuDirty;

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

        private void OnDisable() => FlushMenu();
        private void OnLostFocus() => FlushMenu();

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
                if (GUILayout.Toggle(on, _settings.presets[i].presetName, on ? EditorStyles.toolbarButton : EditorStyles.label) && !on)
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
                    EditorUtility.DisplayDialog("Delete Preset", $"Hapus preset '{_settings.presets[_selected].presetName}'?", "Hapus", "Batal"))
                {
                    Undo.RecordObject(_settings, "Delete Preset");
                    _settings.presets.RemoveAt(_selected);
                    _selected = Mathf.Clamp(_selected - 1, -1, _settings.presets.Count - 1);
                    MarkDirty();
                }
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("💾 Save Settings", GUILayout.Height(26))) SaveSettings();
            if (GUILayout.Button("↻ Rebuild Create Menu")) FlushMenu(force: true);
            EditorGUILayout.LabelField("Klik-kanan Project ▸ Create ▸ Easy Create", EditorStyles.wordWrappedMiniLabel);
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
            preset.baseName      = EditorGUILayout.TextField(new GUIContent("Base Name", "Nilai awal token {name}; bisa ditimpa saat Create (inline-rename)."), preset.baseName);
            preset.groupInFolder = EditorGUILayout.Toggle(new GUIContent("Group in Folder", "ON = buat folder berisi semua file; nama folder mengisi {name}."), preset.groupInFolder);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Files", EditorStyles.boldLabel);

            int removeAt = -1;
            for (int i = 0; i < preset.files.Count; i++)
                if (DrawFileEntry(preset, preset.files[i], i)) removeAt = i;
            if (removeAt >= 0) { Undo.RecordObject(_settings, "Remove File Entry"); preset.files.RemoveAt(removeAt); }

            if (GUILayout.Button("＋ Add File")) { Undo.RecordObject(_settings, "Add File Entry"); preset.files.Add(new FileEntry()); }

            if (EditorGUI.EndChangeCheck()) MarkDirty();

            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(preset.groupInFolder
                ? "Create membuat FOLDER berisi file-file ini; kamu langsung mengetik nama folder (mengisi {name} semua file)."
                : "Create membuat file-file ini langsung; kamu langsung mengetik nama pada file fokus ({edit}) untuk mengisi semua nama.",
                MessageType.None);

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

            entry.source = EditorGUILayout.ObjectField(
                new GUIContent("Source", "Object yang dibuat: prefab → variant; script SO → aset SO; script Component → prefab+komponen; aset lain → salinan."),
                entry.source, typeof(UnityEngine.Object), false);

            entry.namePattern = EditorGUILayout.TextField(
                new GUIContent("Name Pattern", "{name} = Base Name. {edit} = sama tapi menandai file ini fokus rename pertama. mis. {edit} atau {name}_plant."),
                entry.namePattern);

            string preview = FEasyCreateGenerator.ResolveName(preset.baseName, entry.namePattern);
            ECreateKind resolved = FEasyCreateGenerator.ResolveKind(entry);
            bool focus = FEasyCreateGenerator.HasEditToken(entry.namePattern);
            EditorGUILayout.LabelField(" ", $"→ {preview}   [{resolved}]{(focus ? "   ◉ fokus rename" : "")}", EditorStyles.miniLabel);

            EditorGUILayout.EndVertical();
            return remove;
        }

        // ---------------- helper ---------------- //

        private bool HasSelection => _selected >= 0 && _selected < _settings.presets.Count;

        private void MarkDirty()
        {
            EditorUtility.SetDirty(_settings);
            _menuDirty = true;
        }

        /// <summary>Simpan aset setting ke disk dan segarkan menu Create.</summary>
        private void SaveSettings()
        {
            if (_settings == null) return;
            EditorUtility.SetDirty(_settings);
            AssetDatabase.SaveAssets();
            FlushMenu(force: true);
            ShowNotification(new GUIContent("Settings saved"));
        }

        /// <summary>Regenerate file menu bila ada perubahan (dipanggil saat window blur/close, atau paksa via tombol).</summary>
        private void FlushMenu(bool force = false)
        {
            if (_settings == null) return;
            AssetDatabase.SaveAssets();
            if (_menuDirty || force)
            {
                FEasyCreateMenuGen.Rebuild(_settings);
                _menuDirty = false;
            }
        }
    }
}
