using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FEasyCreate.Editor
{
    /// <summary>
    /// Logika pembuatan file untuk FEasyCreate. Dipanggil oleh <see cref="FEasyCreateRunner"/> saat
    /// menu klik-kanan dijalankan. Jenis file ditebak dari <see cref="FileEntry.source"/>.
    /// </summary>
    public static class FEasyCreateGenerator
    {
        public const string NameToken = "{name}";
        public const string EditToken = "{edit}";

        /// <summary>Buat semua file preset di <paramref name="folder"/> memakai <paramref name="baseName"/> untuk token nama.</summary>
        public static List<UnityEngine.Object> CreateFiles(CreatePreset preset, string folder, string baseName, List<string> warnings)
        {
            var made = new List<UnityEngine.Object>();
            if (preset == null) return made;

            folder = string.IsNullOrWhiteSpace(folder) ? "Assets" : folder.Replace('\\', '/').TrimEnd('/');
            FEasyCreateSettings.EnsureFolder(folder);

            foreach (var entry in preset.files)
            {
                string fileName = ResolveName(baseName, entry.namePattern);
                if (string.IsNullOrWhiteSpace(fileName)) { warnings.Add("Nama file kosong — entri dilewati."); continue; }

                UnityEngine.Object obj;
                switch (ResolveKind(entry))
                {
                    case ECreateKind.ScriptableObject: obj = CreateScriptableObject(entry, folder, fileName, warnings); break;
                    case ECreateKind.PrefabVariant:    obj = CreatePrefabVariant(entry, folder, fileName, warnings); break;
                    case ECreateKind.EmptyPrefab:      obj = CreateEmptyPrefab(entry, folder, fileName, warnings); break;
                    case ECreateKind.Copy:             obj = CreateCopy(entry, folder, fileName, warnings); break;
                    default:
                        warnings.Add($"'{fileName}': jenis tak terselesaikan — isi field Source.");
                        obj = null; break;
                }
                if (obj != null) made.Add(obj);
            }
            return made;
        }

        /// <summary>
        /// Tebak apa yang dibuat dari <see cref="FileEntry.source"/> (kecuali Kind di-override):
        /// Prefab → Variant; Script SO → SO baru; Script Component → prefab kosong + komponen; aset lain → salinan.
        /// </summary>
        public static ECreateKind ResolveKind(FileEntry entry)
        {
            if (entry.kind != ECreateKind.Auto) return entry.kind;

            var src = entry.source;
            if (src == null) return ECreateKind.Auto;
            if (src is GameObject) return ECreateKind.PrefabVariant;

            if (src is MonoScript ms)
            {
                Type t = ms.GetClass();
                if (t != null && typeof(ScriptableObject).IsAssignableFrom(t)) return ECreateKind.ScriptableObject;
                if (t != null && typeof(Component).IsAssignableFrom(t)) return ECreateKind.EmptyPrefab;
                return ECreateKind.Auto;
            }
            return ECreateKind.Copy;
        }

        // ---- create per kind ---- //

        private static UnityEngine.Object CreateScriptableObject(FileEntry entry, string folder, string fileName, List<string> warnings)
        {
            Type type = SoTypeFromSource(entry.source);
            if (type == null) { warnings.Add($"'{fileName}': Source harus script ScriptableObject yang valid."); return null; }
            var so = ScriptableObject.CreateInstance(type);
            string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{fileName}.asset");
            AssetDatabase.CreateAsset(so, path);
            return so;
        }

        private static UnityEngine.Object CreatePrefabVariant(FileEntry entry, string folder, string fileName, List<string> warnings)
        {
            var source = entry.source as GameObject;
            if (source == null) { warnings.Add($"'{fileName}': Source untuk Prefab Variant harus prefab (GameObject)."); return null; }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            try
            {
                string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{fileName}.prefab");
                return PrefabUtility.SaveAsPrefabAsset(instance, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }

        private static UnityEngine.Object CreateEmptyPrefab(FileEntry entry, string folder, string fileName, List<string> warnings)
        {
            var go = new GameObject(fileName);
            try
            {
                var ms = entry.source as MonoScript;
                Type comp = ms != null ? ms.GetClass() : null;
                if (comp != null && typeof(Component).IsAssignableFrom(comp)) go.AddComponent(comp);
                else if (entry.source != null) warnings.Add($"'{fileName}': Source bukan script Component valid (prefab dibuat tanpa komponen).");
                string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{fileName}.prefab");
                return PrefabUtility.SaveAsPrefabAsset(go, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        private static UnityEngine.Object CreateCopy(FileEntry entry, string folder, string fileName, List<string> warnings)
        {
            if (entry.source == null) { warnings.Add($"'{fileName}': Source kosong untuk mode Copy."); return null; }
            string srcPath = AssetDatabase.GetAssetPath(entry.source);
            if (string.IsNullOrEmpty(srcPath)) { warnings.Add($"'{fileName}': Source bukan aset di project."); return null; }
            string ext = Path.GetExtension(srcPath);
            string dst = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{fileName}{ext}");
            if (!AssetDatabase.CopyAsset(srcPath, dst)) { warnings.Add($"'{fileName}': gagal menyalin dari '{srcPath}'."); return null; }
            return AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(dst);
        }

        // ---- nama & token ---- //

        /// <summary>Ganti {name}/{edit} dengan baseName. Tanpa token, baseName ditaruh di depan pola.</summary>
        public static string ResolveName(string baseName, string pattern)
        {
            baseName = baseName ?? "";
            pattern = string.IsNullOrEmpty(pattern) ? NameToken : pattern;
            string p = pattern.Replace(EditToken, NameToken);
            if (p.Contains(NameToken)) return p.Replace(NameToken, baseName);
            return baseName + p;
        }

        /// <summary>True bila pola menandai file ini sebagai fokus rename pertama ({edit}).</summary>
        public static bool HasEditToken(string pattern) => pattern != null && pattern.Contains(EditToken);

        /// <summary>Dari nama yang diketik user pada file fokus, kembalikan Base Name-nya (buang literal pola).</summary>
        public static string StripToBase(string typed, string pattern)
        {
            typed = typed ?? "";
            string norm = (string.IsNullOrEmpty(pattern) ? NameToken : pattern).Replace(EditToken, NameToken);
            int idx = norm.IndexOf(NameToken, StringComparison.Ordinal);
            if (idx < 0) return typed;
            string prefix = norm.Substring(0, idx);
            string suffix = norm.Substring(idx + NameToken.Length);
            string t = typed;
            if (prefix.Length > 0 && t.StartsWith(prefix)) t = t.Substring(prefix.Length);
            if (suffix.Length > 0 && t.EndsWith(suffix)) t = t.Substring(0, t.Length - suffix.Length);
            return t;
        }

        /// <summary>Perkiraan ekstensi file (untuk placeholder inline-rename).</summary>
        public static string GuessExtension(FileEntry entry)
        {
            switch (ResolveKind(entry))
            {
                case ECreateKind.PrefabVariant:
                case ECreateKind.EmptyPrefab: return ".prefab";
                case ECreateKind.Copy:
                    string p = entry.source != null ? AssetDatabase.GetAssetPath(entry.source) : null;
                    return string.IsNullOrEmpty(p) ? ".asset" : Path.GetExtension(p);
                default: return ".asset";
            }
        }

        /// <summary>Ikon untuk placeholder inline-rename.</summary>
        public static Texture2D GuessIcon(FileEntry entry)
        {
            if (entry.source != null)
            {
                var t = AssetPreview.GetMiniThumbnail(entry.source);
                if (t != null) return t;
            }
            return EditorGUIUtility.IconContent("ScriptableObject Icon").image as Texture2D;
        }

        private static Type SoTypeFromSource(UnityEngine.Object source)
        {
            if (source is MonoScript ms)
            {
                Type t = ms.GetClass();
                return (t != null && typeof(ScriptableObject).IsAssignableFrom(t)) ? t : null;
            }
            if (source is ScriptableObject so) return so.GetType();
            return null;
        }
    }
}
