using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FEasyCreate.Editor
{
    /// <summary>
    /// Jenis file yang dibuat FEasyCreate untuk satu baris entri.
    /// <see cref="Auto"/> = tebak otomatis dari apa yang kamu isi (Source Prefab → variant,
    /// Class Name → ScriptableObject, selain itu → prefab kosong).
    /// </summary>
    public enum ECreateKind
    {
        Auto,
        ScriptableObject,
        PrefabVariant,
        EmptyPrefab
    }

    /// <summary>Satu file yang akan dibuat dalam sebuah preset.</summary>
    [Serializable]
    public class FileEntry
    {
        [Tooltip("Cara file ini dibuat. Auto = tebak dari isian di bawah.")]
        public ECreateKind kind = ECreateKind.Auto;

        [Tooltip("Nama class ScriptableObject yang dibuat (mis. PlantData, ItemData, BuildingData). " +
                 "Dipakai untuk kind ScriptableObject.")]
        public string className = "";

        [Tooltip("Prefab sumber untuk Prefab Variant (drag prefab ke sini). Dipakai untuk kind PrefabVariant.")]
        public UnityEngine.Object sourcePrefab;

        [Tooltip("(Opsional) nama class Component yang ditempel pada Empty GameObject prefab.")]
        public string componentClassName = "";

        [Tooltip("Pola nama file. Pakai token {name} untuk Base Name, mis. {name}_plant → berry_plant. " +
                 "Kalau tanpa {name}, Base Name otomatis ditaruh di depan.")]
        public string namePattern = "{name}";

        [Tooltip("Folder tempat file dibuat. Kosong = pakai Default Location milik preset.")]
        public string fileLocation = "";
    }

    /// <summary>Satu preset = satu set file yang dibuat sekaligus (yang di-CRUD di window).</summary>
    [Serializable]
    public class CreatePreset
    {
        public string presetName = "New Preset";

        [Tooltip("Base Name — identitas set ini; mengisi token {name} di tiap file (mis. berry).")]
        public string baseName = "";

        [Tooltip("Folder default; dipakai saat File Location sebuah entri dikosongkan.")]
        public string defaultLocation = "Assets";

        public List<FileEntry> files = new List<FileEntry>();

        public CreatePreset Clone()
        {
            var c = new CreatePreset
            {
                presetName = presetName + " Copy",
                baseName = baseName,
                defaultLocation = defaultLocation,
                files = new List<FileEntry>(files.Count)
            };
            foreach (var f in files)
                c.files.Add(new FileEntry
                {
                    kind = f.kind,
                    className = f.className,
                    sourcePrefab = f.sourcePrefab,
                    componentClassName = f.componentClassName,
                    namePattern = f.namePattern,
                    fileLocation = f.fileLocation
                });
            return c;
        }
    }

    /// <summary>
    /// Aset penyimpan semua preset. Dibuat di PROJECT (bukan di dalam package) supaya preset-mu
    /// bersifat spesifik-project dan package tetap generik/reusable.
    /// </summary>
    public class FEasyCreateSettings : ScriptableObject
    {
        public List<CreatePreset> presets = new List<CreatePreset>();

        public const string AssetPath = "Assets/Editor/FEasyCreate/FEasyCreateSettings.asset";

        public static FEasyCreateSettings GetOrCreate()
        {
            var settings = AssetDatabase.LoadAssetAtPath<FEasyCreateSettings>(AssetPath);
            if (settings != null) return settings;

            settings = CreateInstance<FEasyCreateSettings>();
            EnsureFolder("Assets/Editor/FEasyCreate");
            AssetDatabase.CreateAsset(settings, AssetPath);
            AssetDatabase.SaveAssets();
            return settings;
        }

        /// <summary>Buat folder aset bertingkat bila belum ada (mis. Assets/Editor/FEasyCreate).</summary>
        public static void EnsureFolder(string folder)
        {
            folder = folder.Replace('\\', '/').TrimEnd('/');
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder)) return;
            if (!folder.StartsWith("Assets")) return;

            int slash = folder.LastIndexOf('/');
            string parent = slash > 0 ? folder.Substring(0, slash) : "Assets";
            string name = folder.Substring(slash + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
