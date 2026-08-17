using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FEasyCreate.Editor
{
    /// <summary>
    /// Logika pembuatan file untuk FEasyCreate. Murni statis; window hanya memanggil <see cref="Generate"/>.
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
                default:
                    warnings.Add($"'{fileName}': tak bisa menebak jenis file (isi Class Name atau Source Prefab).");
                    return null;
            }
        }

        private static ECreateKind ResolveKind(FileEntry entry)
        {
            if (entry.kind != ECreateKind.Auto) return entry.kind;
            if (entry.sourcePrefab is GameObject) return ECreateKind.PrefabVariant;
            if (ResolveScriptableType(entry.className) != null) return ECreateKind.ScriptableObject;
            if (!string.IsNullOrWhiteSpace(entry.componentClassName)) return ECreateKind.EmptyPrefab;
            return ECreateKind.Auto; // tak terselesaikan
        }

        private static UnityEngine.Object CreateScriptableObject(FileEntry entry, string folder, string fileName, List<string> warnings)
        {
            Type type = ResolveScriptableType(entry.className);
            if (type == null)
            {
                warnings.Add($"'{fileName}': class ScriptableObject '{entry.className}' tak ditemukan.");
                return null;
            }
            var so = ScriptableObject.CreateInstance(type);
            string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{fileName}.asset");
            AssetDatabase.CreateAsset(so, path);
            return so;
        }

        private static UnityEngine.Object CreatePrefabVariant(FileEntry entry, string folder, string fileName, List<string> warnings)
        {
            var source = entry.sourcePrefab as GameObject;
            if (source == null)
            {
                warnings.Add($"'{fileName}': Source Prefab belum diisi untuk Prefab Variant.");
                return null;
            }
            // Meng-instantiate prefab lalu SaveAsPrefabAsset menghasilkan VARIANT dari sumbernya.
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            try
            {
                string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{fileName}.prefab");
                var variant = PrefabUtility.SaveAsPrefabAsset(instance, path);
                return variant;
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
                if (!string.IsNullOrWhiteSpace(entry.componentClassName))
                {
                    Type comp = ResolveComponentType(entry.componentClassName);
                    if (comp != null) go.AddComponent(comp);
                    else warnings.Add($"'{fileName}': Component '{entry.componentClassName}' tak ditemukan (prefab dibuat tanpa komponen itu).");
                }
                string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{fileName}.prefab");
                return PrefabUtility.SaveAsPrefabAsset(go, path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        // ---- helper nama & tipe ---- //

        /// <summary>Ganti {name} dengan baseName. Bila pola tak punya {name}, baseName ditaruh di depan.</summary>
        public static string ResolveName(string baseName, string pattern)
        {
            baseName = baseName ?? "";
            pattern = string.IsNullOrEmpty(pattern) ? "{name}" : pattern;
            if (pattern.Contains("{name}"))
                return pattern.Replace("{name}", baseName);
            return baseName + pattern;
        }

        public static Type ResolveScriptableType(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            foreach (var t in TypeCache.GetTypesDerivedFrom<ScriptableObject>())
                if (t.Name == name || t.FullName == name) return t;
            return null;
        }

        public static Type ResolveComponentType(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            foreach (var t in TypeCache.GetTypesDerivedFrom<Component>())
                if (t.Name == name || t.FullName == name) return t;
            return null;
        }
    }
}
