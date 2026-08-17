using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FEasyCreate.Editor
{
    /// <summary>
    /// Logika pembuatan file untuk FEasyCreate. Murni statis; window hanya memanggil <see cref="Generate"/>.
    /// Semua entri kini memakai SATU field <see cref="FileEntry.source"/> — jenis file ditebak dari apa yang
    /// di-drop ke sana (lihat <see cref="ResolveKind"/>).
    /// </summary>
    public static class FEasyCreateGenerator
    {
        public struct Result
        {
            public List<UnityEngine.Object> created;
            public List<string> warnings;
            public bool AnyCreated => created != null && created.Count > 0;
        }

        /// <summary>Buat semua file dalam sebuah preset. Mengembalikan aset yang berhasil dibuat + peringatan.</summary>
        public static Result Generate(CreatePreset preset)
        {
            var result = new Result { created = new List<UnityEngine.Object>(), warnings = new List<string>() };
            if (preset == null) return result;

            if (string.IsNullOrWhiteSpace(preset.baseName))
                result.warnings.Add("Base Name kosong — token {name} akan menghasilkan nama kosong.");

            try
            {
                AssetDatabase.StartAssetEditing();
                for (int i = 0; i < preset.files.Count; i++)
                {
                    var entry = preset.files[i];
                    try
                    {
                        var obj = CreateOne(preset, entry, result.warnings);
                        if (obj != null) result.created.Add(obj);
                    }
                    catch (Exception e)
                    {
                        result.warnings.Add($"Baris {i + 1}: gagal — {e.Message}");
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            if (result.AnyCreated)
            {
                Selection.objects = result.created.ToArray();
                EditorGUIUtility.PingObject(result.created[0]);
            }
            return result;
        }

        // ---- satu entri ---- //

        private static UnityEngine.Object CreateOne(CreatePreset preset, FileEntry entry, List<string> warnings)
        {
            ECreateKind kind = ResolveKind(entry);
            string folder = string.IsNullOrWhiteSpace(entry.fileLocation) ? preset.defaultLocation : entry.fileLocation;
            folder = string.IsNullOrWhiteSpace(folder) ? "Assets" : folder.Replace('\\', '/').TrimEnd('/');

            if (!folder.StartsWith("Assets"))
            {
                warnings.Add($"Lokasi '{folder}' di luar folder Assets — dilewati.");
                return null;
            }
            FEasyCreateSettings.EnsureFolder(folder);

            string fileName = ResolveName(preset.baseName, entry.namePattern);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                warnings.Add("Nama file kosong — entri dilewati.");
                return null;
            }

            switch (kind)
            {
                case ECreateKind.ScriptableObject: return CreateScriptableObject(entry, folder, fileName, warnings);
                case ECreateKind.PrefabVariant:    return CreatePrefabVariant(entry, folder, fileName, warnings);
                case ECreateKind.EmptyPrefab:      return CreateEmptyPrefab(entry, folder, fileName, warnings);
                case ECreateKind.Copy:             return CreateCopy(entry, folder, fileName, warnings);
                default:
                    warnings.Add($"'{fileName}': tak bisa menebak jenis file — isi field Source (prefab / script / aset).");
                    return null;
            }
        }

        /// <summary>
        /// Tebak apa yang dibuat dari <see cref="FileEntry.source"/> (kecuali Kind di-override manual):
        /// Prefab → Variant; Script SO → SO baru; Script Component → prefab kosong + komponen; aset lain → salinan.
        /// </summary>
        public static ECreateKind ResolveKind(FileEntry entry)
        {
            if (entry.kind != ECreateKind.Auto) return entry.kind;

            var src = entry.source;
            if (src == null) return ECreateKind.Auto;                 // tak terselesaikan
            if (src is GameObject) return ECreateKind.PrefabVariant;  // aset prefab → variant

            if (src is MonoScript ms)
            {
                Type t = ms.GetClass();
                if (t != null && typeof(ScriptableObject).IsAssignableFrom(t)) return ECreateKind.ScriptableObject;
                if (t != null && typeof(Component).IsAssignableFrom(t)) return ECreateKind.EmptyPrefab;
                return ECreateKind.Auto;                              // script tak terselesaikan
            }

            return ECreateKind.Copy;                                  // SO / Material / aset lain → salinan penuh
        }

        private static UnityEngine.Object CreateScriptableObject(FileEntry entry, string folder, string fileName, List<string> warnings)
        {
            Type type = SoTypeFromSource(entry.source);
            if (type == null)
            {
                warnings.Add($"'{fileName}': Source harus script ScriptableObject yang valid.");
                return null;
            }
            var so = ScriptableObject.CreateInstance(type);
            string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{fileName}.asset");
            AssetDatabase.CreateAsset(so, path);
            return so;
        }

        private static UnityEngine.Object CreatePrefabVariant(FileEntry entry, string folder, string fileName, List<string> warnings)
        {
            var source = entry.source as GameObject;
            if (source == null)
            {
                warnings.Add($"'{fileName}': Source untuk Prefab Variant harus sebuah prefab (GameObject).");
                return null;
            }
            // Meng-instantiate prefab lalu SaveAsPrefabAsset menghasilkan VARIANT dari sumbernya.
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            try
            {
                string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{fileName}.prefab");
                return PrefabUtility.SaveAsPrefabAsset(instance, path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static UnityEngine.Object CreateEmptyPrefab(FileEntry entry, string folder, string fileName, List<string> warnings)
        {
            var go = new GameObject(fileName);
            try
            {
                var ms = entry.source as MonoScript;
                Type comp = ms != null ? ms.GetClass() : null;
                if (comp != null && typeof(Component).IsAssignableFrom(comp))
                    go.AddComponent(comp);
                else if (entry.source != null)
                    warnings.Add($"'{fileName}': Source bukan script Component yang valid (prefab dibuat tanpa komponen).");
                string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{fileName}.prefab");
                return PrefabUtility.SaveAsPrefabAsset(go, path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        /// <summary>Salin penuh aset sumber (SO, Material, dll) ke file baru — mempertahankan datanya.</summary>
        private static UnityEngine.Object CreateCopy(FileEntry entry, string folder, string fileName, List<string> warnings)
        {
            if (entry.source == null)
            {
                warnings.Add($"'{fileName}': Source kosong untuk mode Copy.");
                return null;
            }
            string srcPath = AssetDatabase.GetAssetPath(entry.source);
            if (string.IsNullOrEmpty(srcPath))
            {
                warnings.Add($"'{fileName}': Source bukan aset di project (tak bisa disalin).");
                return null;
            }
            string ext = Path.GetExtension(srcPath); // termasuk titik, mis. .asset / .mat
            string dst = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{fileName}{ext}");
            if (!AssetDatabase.CopyAsset(srcPath, dst))
            {
                warnings.Add($"'{fileName}': gagal menyalin dari '{srcPath}'.");
                return null;
            }
            return AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(dst);
        }

        // ---- helper ---- //

        /// <summary>Ganti {name} dengan baseName. Bila pola tak punya {name}, baseName ditaruh di depan.</summary>
        public static string ResolveName(string baseName, string pattern)
        {
            baseName = baseName ?? "";
            pattern = string.IsNullOrEmpty(pattern) ? "{name}" : pattern;
            if (pattern.Contains("{name}"))
                return pattern.Replace("{name}", baseName);
            return baseName + pattern;
        }

        /// <summary>Type ScriptableObject dari sebuah source: script SO → class-nya; aset SO → tipe aslinya.</summary>
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
