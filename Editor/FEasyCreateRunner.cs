using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.ProjectWindowCallback;
using UnityEngine;

namespace FEasyCreate.Editor
{
    /// <summary>
    /// Menjalankan sebuah preset dari menu klik-kanan (Create ▸ Easy Create ▸ [Preset]).
    /// Alur meniru "Create ▸ Folder/Script" bawaan Unity: memunculkan item baru dengan nama yang
    /// bisa diketik (inline-rename); saat kamu tekan Enter, file-file dibuat memakai nama itu.
    /// </summary>
    public static class FEasyCreateRunner
    {
        public static void Run(string presetName)
        {
            var settings = FEasyCreateSettings.GetOrCreate();
            var preset = settings.FindPreset(presetName);
            if (preset == null) { Debug.LogError($"[FEasyCreate] Preset '{presetName}' tak ditemukan."); return; }
            if (preset.files == null || preset.files.Count == 0)
            { Debug.LogWarning($"[FEasyCreate] Preset '{presetName}' belum punya file."); return; }

            string folder = GetActiveFolderPath();
            string baseName = string.IsNullOrEmpty(preset.baseName) ? "New" : preset.baseName;

            var action = ScriptableObject.CreateInstance<FEasyCreateEndNameEdit>();
            action.presetName = presetName;

            if (preset.groupInFolder)
            {
                // Item yang diedit = FOLDER. Nama folder sekaligus jadi {name} untuk file di dalamnya.
                string pathName = $"{folder}/{baseName}";
                var icon = EditorGUIUtility.IconContent("Folder Icon").image as Texture2D;
                ProjectWindowUtil.StartNameEditingIfProjectWindowExists(EntityId.None, action, pathName, icon, null);
            }
            else
            {
                // Item yang diedit = file "fokus" (yang polanya {edit}, atau file pertama).
                int primary = FindPrimaryIndex(preset);
                var pe = preset.files[primary];
                action.primaryPattern = pe.namePattern;
                string proposed = FEasyCreateGenerator.ResolveName(baseName, pe.namePattern);
                string pathName = $"{folder}/{proposed}{FEasyCreateGenerator.GuessExtension(pe)}";
                var icon = FEasyCreateGenerator.GuessIcon(pe);
                ProjectWindowUtil.StartNameEditingIfProjectWindowExists(EntityId.None, action, pathName, icon, null);
            }
        }

        private static int FindPrimaryIndex(CreatePreset preset)
        {
            for (int i = 0; i < preset.files.Count; i++)
                if (FEasyCreateGenerator.HasEditToken(preset.files[i].namePattern)) return i;
            return 0;
        }

        /// <summary>Folder aktif di Project window (tempat item Create dibuat).</summary>
        private static string GetActiveFolderPath()
        {
            // ProjectWindowUtil.GetActiveFolderPath() internal — pakai refleksi bila ada.
            var m = typeof(ProjectWindowUtil).GetMethod("GetActiveFolderPath", BindingFlags.Static | BindingFlags.NonPublic);
            if (m != null)
            {
                var p = m.Invoke(null, null) as string;
                if (!string.IsNullOrEmpty(p)) return p;
            }
            // Fallback: dari objek terpilih.
            if (Selection.activeObject != null)
            {
                string p = AssetDatabase.GetAssetPath(Selection.activeObject);
                if (!string.IsNullOrEmpty(p))
                    return AssetDatabase.IsValidFolder(p) ? p : Path.GetDirectoryName(p).Replace('\\', '/');
            }
            return "Assets";
        }
    }

    /// <summary>Callback saat user menyelesaikan pengetikan nama (Enter) pada item Create.</summary>
    internal class FEasyCreateEndNameEdit : AssetCreationEndAction
    {
        public string presetName;
        public string primaryPattern; // hanya untuk mode non-folder

        public override void Action(EntityId entityId, string pathName, string resourceFile)
        {
            var settings = FEasyCreateSettings.GetOrCreate();
            var preset = settings.FindPreset(presetName);
            if (preset == null) return;

            pathName = pathName.Replace('\\', '/');
            string dir = Path.GetDirectoryName(pathName).Replace('\\', '/');
            string typed = Path.GetFileNameWithoutExtension(pathName);

            var warnings = new List<string>();
            List<UnityEngine.Object> made = null;
            try
            {
                AssetDatabase.StartAssetEditing();
                if (preset.groupInFolder)
                {
                    string baseName = typed;
                    string folderPath = AssetDatabase.GenerateUniqueAssetPath($"{dir}/{baseName}");
                    AssetDatabase.CreateFolder(dir, Path.GetFileName(folderPath));
                    made = FEasyCreateGenerator.CreateFiles(preset, folderPath, baseName, warnings);
                    var folderObj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(folderPath);
                    if (folderObj != null) EditorGUIUtility.PingObject(folderObj);
                }
                else
                {
                    string baseName = FEasyCreateGenerator.StripToBase(typed, primaryPattern);
                    made = FEasyCreateGenerator.CreateFiles(preset, dir, baseName, warnings);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            if (warnings.Count > 0) Debug.LogWarning("[FEasyCreate] " + string.Join("\n  ", warnings));
            if (made != null && made.Count > 0)
            {
                Selection.objects = made.ToArray();
                EditorGUIUtility.PingObject(made[0]);
            }
        }
    }
}
