using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

namespace HarmonyAnimatorConverter
{
    public class AnimationFileExporter : EditorWindow
    {
        private string folderPath = "Assets"; // Default folder
        private List<string> animationFiles = new List<string>();
        private Vector2 scrollPosition;

        [MenuItem("Tools/Harmony Anim Toolkit/Animation Asset Finder")]
        public static void ShowWindow()
        {
            GetWindow<AnimationFileExporter>("Animation Finder");
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Quickly find all the animations you have in a particular folder.", MessageType.Info);

            GUILayout.Label("Select Animation Folder", EditorStyles.boldLabel);

            // Folder selection button
            if (GUILayout.Button("Select Folder"))
            {
                string selectedPath = EditorUtility.OpenFolderPanel("Select Animation Folder", "Assets", "");
                if (!string.IsNullOrEmpty(selectedPath))
                {
                    // Convert absolute path to Unity relative path
                    if (selectedPath.StartsWith(Application.dataPath))
                    {
                        folderPath = "Assets" + selectedPath.Substring(Application.dataPath.Length);
                        ScanForAnimations();
                    }
                    else
                    {
                        Debug.LogError("Please select a folder inside the Assets directory.");
                    }
                }
            }

            // Display selected folder
            EditorGUILayout.LabelField("Selected Folder:", folderPath);

            // Display found animation files
            GUILayout.Label("Found Animations:", EditorStyles.boldLabel);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(200));
            string animationsText = string.Join("\n", animationFiles);
            EditorGUILayout.TextArea(animationsText, EditorStyles.textArea, GUILayout.ExpandHeight(true));

            EditorGUILayout.EndScrollView();

            // Export button
            if (animationFiles.Count > 0 && GUILayout.Button("Export to JSON"))
            {
                ExportToJson();
            }

        }

        private void ScanForAnimations()
        {
            animationFiles.Clear();

            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
            {
                Debug.LogError("ScanForAnimations: Invalid folder path.");
                return;
            }

            string relativePath = folderPath.Replace(Application.dataPath, "Assets"); // Convert to Unity's AssetDatabase path format

            // Get all animation clips (.anim) directly in the folder
            string[] animGuids = AssetDatabase.FindAssets("t:AnimationClip", new[] { relativePath });
            foreach (string guid in animGuids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
                if (clip != null && !clip.name.Contains("__preview__")) // Ignore Unity preview clips
                {
                    animationFiles.Add(clip.name);
                }
            }

            // Get all model files (.fbx, .gltf, .blend) in the folder
            string[] modelGuids = AssetDatabase.FindAssets("t:Model", new[] { relativePath });
            foreach (string guid in modelGuids)
            {
                string modelPath = AssetDatabase.GUIDToAssetPath(guid);
                UnityEngine.Object[] importedAssets = AssetDatabase.LoadAllAssetsAtPath(modelPath);

                foreach (UnityEngine.Object asset in importedAssets)
                {
                    if (asset is AnimationClip clip && !clip.name.Contains("__preview__")) // Ignore Unity-generated previews
                    {
                        animationFiles.Add(clip.name);
                    }
                }
            }

            Debug.Log($"ScanForAnimations: Found {animationFiles.Count} animation(s) in folder: {folderPath}");
        }


        private void ExportToJson()
        {
            string json = JsonUtility.ToJson(new AnimationData(animationFiles), true);
            string path = EditorUtility.SaveFilePanel("Save Animation JSON", folderPath, "animations", "json");

            if (!string.IsNullOrEmpty(path))
            {
                File.WriteAllText(path, json);
                AssetDatabase.Refresh();
                Debug.Log("Exported animations to " + path);
            }
        }

        [System.Serializable]
        private class AnimationData
        {
            public List<string> animations;
            public AnimationData(List<string> anims)
            {
                animations = anims;
            }
        }
    }
}
