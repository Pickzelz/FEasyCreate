using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FEasyCreate.Editor
{
    /// <summary>Jenis file yang dibuat. <see cref="Auto"/> = tebak dari <see cref="FileEntry.source"/>.</summary>
    public enum ECreateKind
    {
        Auto,
        ScriptableObject,
        PrefabVariant,
        EmptyPrefab,
        Copy
    }

    /// <summary>Satu file yang akan dibuat dalam sebuah preset.</summary>
    [Serializable]
    public class FileEntry
    {
        [Tooltip("Cara file ini dibuat. Auto = tebak dari Source.")]
        public ECreateKind kind = ECreateKind.Auto;

        [Tooltip("Object yang akan dibuat — drag/pilih apa saja:\n" +
                 "• Prefab (GameObject) → dibuat Prefab Variant-nya\n" +
                 "• Script ScriptableObject (mis. PlantData) → aset SO baru\n" +
                 "• Script Component (MonoBehaviour) → prefab kosong + komponen itu\n" +
                 "• Aset lain (SO/Material/dll) → salinan penuh")]
        public UnityEngine.Object source;

        [Tooltip("Pola nama file.\n" +
                 "• {name} = Base Name (nama yang diketik saat Create)\n" +
                 "• {edit} = sama seperti {name}, TAPI menandai file INI yang mendapat fokus rename pertama\n" +
                 "Contoh: {edit}  atau  {name}_plant. Tanpa token, Base Name ditaruh di depan.")]
        public string namePattern = "{name}";
    }

    /// <summary>Satu preset = satu set file yang dibuat sekaligus (di-CRUD di window, dijalankan dari menu klik-kanan).</summary>
    [Serializable]
    public class CreatePreset
    {
        public string presetName = "New Preset";

        [Tooltip("Base Name — nilai awal token {name}. Bisa ditimpa saat Create lewat inline-rename.")]
        public string baseName = "";

        [Tooltip("Jika ON, Create membuat sebuah FOLDER berisi semua file; nama folder yang kamu ketik " +
                 "sekaligus mengisi {name} tiap file. Jika OFF, file dibuat langsung di folder yang diklik-kanan.")]
        public bool groupInFolder = false;

        public List<FileEntry> files = new List<FileEntry>();

        public CreatePreset Clone()
        {
            var c = new CreatePreset
            {
                presetName = presetName + " Copy",
                baseName = baseName,
                groupInFolder = groupInFolder,
                files = new List<FileEntry>(files.Count)
            };
            foreach (var f in files)
                c.files.Add(new FileEntry { kind = f.kind, source = f.source, namePattern = f.namePattern });
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

        public CreatePreset FindPreset(string name) => presets.Find(p => p != null && p.presetName == name);

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
