using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace HarmonyAnimatorToolkit
{
    public class SpriteAnimationGenerator : EditorWindow
    {
        #region Fields
        private Texture2D spritesheet;
        private List<AnimationDefinition> animations = new List<AnimationDefinition>();
        private Vector2 scrollPosition;
        private Vector2 mainScrollPosition;
        private string savePath = "Assets/Animations/";
        private bool showPreview = true;
        private int previewAnimIndex = 0;
        private double previewTime = 0;
        private double lastRepaintTime;
        private SpriteOrderMethod orderMethod = SpriteOrderMethod.NumericSuffix;
        private GUIStyle headerStyle;
        private GUIStyle sectionStyle;
        private float previewSize = 100f;
        private SpriteSourceType sourceType = SpriteSourceType.Spritesheet;
        // Added to store sprite scroll position for each animation
        private Dictionary<int, Vector2> specificSpriteScrollPositions = new Dictionary<int, Vector2>();

        private SpriteConverterSO spriteSO;
        #endregion

        #region Enums
        public enum SpriteSourceType
        {
            Spritesheet,
            IndividualFiles
        }

        public enum SpriteOrderMethod
        {
            BottomToTop,
            TopToBottom,
            LeftToRight,
            RightToLeft,
            NameAlphabetical,
            // Added new sort method for numeric ordering
            NumericSuffix
        }
        #endregion

        #region Editor Window Setup
        [MenuItem("Tools/Harmony Anim Toolkit/Sprite Animation Generator")]
        public static void ShowWindow()
        {
            GetWindow<SpriteAnimationGenerator>("Sprite Animation Generator");
        }

        private void OnEnable()
        {
            InitializeStyles();
        }

        private void InitializeStyles()
        {
            headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                margin = new RectOffset(0, 0, 10, 10)
            };

            sectionStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                margin = new RectOffset(0, 0, 10, 5)
            };
        }
        #endregion

        #region Main GUI
        private void OnGUI()
        {
            if (headerStyle == null || sectionStyle == null)
            {
                InitializeStyles();
            }

            mainScrollPosition = EditorGUILayout.BeginScrollView(mainScrollPosition);

            EditorGUILayout.Space(5);
            GUILayout.Label("Sprite Animation Generator", headerStyle);
            EditorGUILayout.Space(5);

            DrawSourceSettings();

            EditorGUILayout.Space(10);
            DrawAnimationDefinitions();

            EditorGUILayout.Space(10);
            DrawPreviewSection();

            EditorGUILayout.Space(10);
            DrawGenerateButton();

            EditorGUILayout.Space(5);
            EditorGUILayout.EndScrollView();
        }

        private void DrawSourceSettings()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Source Settings", sectionStyle);

            // Source type selection
            sourceType = (SpriteSourceType)EditorGUILayout.EnumPopup(
                new GUIContent("Source Type", "How to source sprite frames"),
                sourceType
            );

            // Different UI based on source type
            switch (sourceType)
            {
                case SpriteSourceType.Spritesheet:
                    spritesheet = (Texture2D)EditorGUILayout.ObjectField(
                        new GUIContent("Spritesheet", "The texture containing all your sprite frames"),
                        spritesheet,
                        typeof(Texture2D),
                        false
                    );

                    orderMethod = (SpriteOrderMethod)EditorGUILayout.EnumPopup(
                        new GUIContent("Sprite Order", "How sprites should be ordered in the sheet"),
                        orderMethod
                    );
                    break;
            }

            spriteSO = (SpriteConverterSO)EditorGUILayout.ObjectField(
                new GUIContent("Sprite Settings", "Assign your ScriptableObject here"),
                spriteSO,
                typeof(SpriteConverterSO), // or your specific type
                false
            );

            if (spriteSO != null && spriteSO.animations != null)
            {
                animations = new List<AnimationDefinition>(spriteSO.animations);
            }

            DrawSavePathField();
            EditorGUILayout.EndVertical();
        }

        private void DrawSavePathField()
        {
            EditorGUILayout.BeginHorizontal();
            savePath = EditorGUILayout.TextField(
                new GUIContent("Save Path", "Where to save the generated animations"),
                savePath
            );

            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                string selectedPath = EditorUtility.OpenFolderPanel("Select Animation Save Folder", "Assets", "");
                if (!string.IsNullOrEmpty(selectedPath))
                {
                    // Convert to project-relative path
                    if (selectedPath.StartsWith(Application.dataPath))
                    {
                        savePath = "Assets" + selectedPath.Substring(Application.dataPath.Length);
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawAnimationDefinitions()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Animation Definitions", sectionStyle);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Animation"))
            {
                animations.Add(new AnimationDefinition { name = "New Animation", frameCount = 1 });
            }
            EditorGUILayout.EndHorizontal();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(Mathf.Min(animations.Count * 160, 320)));

            for (int i = 0; i < animations.Count; i++)
            {
                DrawAnimationDefinition(i);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawAnimationDefinition(int index)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Animation name and delete button
            if (!DrawAnimNameAndDeleteButton(index))
            {
                return; // If animation was deleted, exit early
            }

            // Source override options
            DrawAnimSourceOptions(index);

            // Animation timing settings
            DrawAnimTimingSettings(index);

            // Preview button
            DrawAnimPreviewButton(index);

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }

        private bool DrawAnimNameAndDeleteButton(int index)
        {
            EditorGUILayout.BeginHorizontal();
            animations[index].name = EditorGUILayout.TextField(
                new GUIContent("Name", "Name of this animation"),
                animations[index].name
            );
            GUI.backgroundColor = new Color(1f, 0.7f, 0.7f);
            if (GUILayout.Button("X", GUILayout.Width(25)))
            {
                animations.RemoveAt(index);
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                // Clean up scroll position for this animation
                if (specificSpriteScrollPositions.ContainsKey(index))
                {
                    specificSpriteScrollPositions.Remove(index);
                }
                return false; // Signal that animation was deleted
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
            return true;
        }

        private void DrawAnimSourceOptions(int index)
        {
            AnimationDefinition anim = animations[index];

            if (sourceType != SpriteSourceType.Spritesheet)
            {
                // Source Override UI
                anim.useSpecificSource = EditorGUILayout.Toggle(
                    new GUIContent("Use Specific Source", "Override the global source for this animation"),
                    anim.useSpecificSource
                );
            }

            if (anim.useSpecificSource)
            {
                DrawSpecificSourceUI(index);
            }

            // Only show start frame and frame count for spritesheet source
            if (sourceType == SpriteSourceType.Spritesheet && !anim.useSpecificSource)
            {
                anim.startFrame = EditorGUILayout.IntField(
                    new GUIContent("Start Frame", "First frame to use (0-based)"),
                    anim.startFrame
                );

                anim.frameCount = EditorGUILayout.IntField(
                    new GUIContent("Frame Count", "Number of frames in this animation"),
                    anim.frameCount
                );
            }
        }

        private void DrawSpecificSourceUI(int index)
        {
            AnimationDefinition anim = animations[index];

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Option to use either specific sprites or a folder
            anim.useAnimationFolder = EditorGUILayout.Toggle(
                new GUIContent("Use Folder", "Use a folder containing sprites for this animation"),
                anim.useAnimationFolder
            );

            if (anim.useAnimationFolder)
            {
                DrawFolderSourceUI(index);
            }
            else
            {
                DrawSpecificSpritesUI(index);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawFolderSourceUI(int index)
        {
            AnimationDefinition anim = animations[index];

            // Folder selection
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Animation Folder");
            if (GUILayout.Button("Select", GUILayout.Width(60)))
            {
                string selectedPath = EditorUtility.OpenFolderPanel("Select Animation Folder", "Assets", "");
                if (!string.IsNullOrEmpty(selectedPath) && selectedPath.StartsWith(Application.dataPath))
                {
                    anim.specificFolder = "Assets" + selectedPath.Substring(Application.dataPath.Length);
                }
            }
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(anim.specificFolder))
            {
                EditorGUILayout.LabelField($"Selected: {anim.specificFolder}");

                anim.folderSortMethod = (SpriteOrderMethod)EditorGUILayout.EnumPopup(
                    new GUIContent("Sort Method", "How sprites should be sorted when loaded from folder"),
                    anim.folderSortMethod
                );
            }
        }

        private void AddSpriteToAnimation(AnimationDefinition anim)
        {
            string path = EditorUtility.OpenFilePanel("Select Sprite", "Assets", "png");
            if (!string.IsNullOrEmpty(path) && path.StartsWith(Application.dataPath))
            {
                string assetPath = "Assets" + path.Substring(Application.dataPath.Length);
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                if (sprite != null)
                    anim.specificSprites.Add(sprite);
            }
        }

        private void DrawSpriteDragDropArea(AnimationDefinition anim)
        {
            Rect dropArea = GUILayoutUtility.GetRect(0, 40, GUILayout.ExpandWidth(true));
            GUI.Box(dropArea, "Drag & Drop Sprites Here", EditorStyles.helpBox);

            if (Event.current.type == EventType.DragUpdated || Event.current.type == EventType.DragPerform)
            {
                if (dropArea.Contains(Event.current.mousePosition))
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                    if (Event.current.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();

                        foreach (Object obj in DragAndDrop.objectReferences)
                        {
                            if (obj is Sprite sprite)
                            {
                                anim.specificSprites.Add(sprite);
                            }
                            else if (obj is Texture2D texture)
                            {
                                string path = AssetDatabase.GetAssetPath(texture);
                                Object[] sprites = AssetDatabase.LoadAllAssetsAtPath(path).Where(x => x is Sprite).ToArray();
                                foreach (Sprite s in sprites)
                                {
                                    anim.specificSprites.Add(s);
                                }
                            }
                        }
                    }
                    Event.current.Use();
                }
            }
        }

        private void DrawSpecificSpritesUI(int index)
        {
            AnimationDefinition anim = animations[index];

            // Individual sprite selection controls
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Specific Sprites");
            if (GUILayout.Button("Add", GUILayout.Width(50)))
            {
                AddSpriteToAnimation(anim);
            }
            if (GUILayout.Button("Clear", GUILayout.Width(50)))
            {
                anim.specificSprites.Clear();
            }
            EditorGUILayout.EndHorizontal();

            // Drag and drop area
            DrawSpriteDragDropArea(anim);

            // List of specific sprites with its own scroll view
            // Initialize scroll position for this animation if not exists
            if (!specificSpriteScrollPositions.ContainsKey(index))
            {
                specificSpriteScrollPositions[index] = Vector2.zero;
            }

            // Modified to use specific scroll position for each animation
            specificSpriteScrollPositions[index] = EditorGUILayout.BeginScrollView(
                specificSpriteScrollPositions[index],
                GUILayout.Height(100)
            );

            for (int j = 0; j < anim.specificSprites.Count; j++)
            {
                EditorGUILayout.BeginHorizontal();
                anim.specificSprites[j] = (Sprite)EditorGUILayout.ObjectField(
                    $"Frame {j}",
                    anim.specificSprites[j],
                    typeof(Sprite),
                    false
                );

                // Up/Down buttons for reordering
                GUI.enabled = j > 0;
                if (GUILayout.Button("↑", GUILayout.Width(25)))
                {
                    Sprite temp = anim.specificSprites[j - 1];
                    anim.specificSprites[j - 1] = anim.specificSprites[j];
                    anim.specificSprites[j] = temp;
                }

                GUI.enabled = j < anim.specificSprites.Count - 1;
                if (GUILayout.Button("↓", GUILayout.Width(25)))
                {
                    Sprite temp = anim.specificSprites[j + 1];
                    anim.specificSprites[j + 1] = anim.specificSprites[j];
                    anim.specificSprites[j] = temp;
                }

                GUI.enabled = true;
                if (GUILayout.Button("X", GUILayout.Width(25)))
                {
                    anim.specificSprites.RemoveAt(j);
                    j--;
                    EditorGUILayout.EndHorizontal();
                    continue;
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawAnimTimingSettings(int index)
        {
            AnimationDefinition anim = animations[index];

            anim.timeInterval = EditorGUILayout.Slider(
                new GUIContent("Frame Duration", "Time in seconds for each frame"),
                anim.timeInterval,
                0.01f,
                0.5f
            );

            EditorGUILayout.LabelField($"Speed: {(1f / anim.timeInterval):F1} FPS");

            anim.loop = EditorGUILayout.Toggle(
                new GUIContent("Loop Animation", "Should this animation play repeatedly?"),
                anim.loop
            );
        }

        private void DrawAnimPreviewButton(int index)
        {
            // Preview button only for spritesheet mode
            if (sourceType == SpriteSourceType.Spritesheet && !animations[index].useSpecificSource)
            {
                if (GUILayout.Button("Preview This Animation"))
                {
                    previewAnimIndex = index;
                    previewTime = 0;
                    showPreview = true;
                }
            }
        }

        private void DrawPreviewSection()
        {
            if (!showPreview || sourceType != SpriteSourceType.Spritesheet || animations.Count <= previewAnimIndex)
                return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label($"Preview: {animations[previewAnimIndex].name}", sectionStyle);

            // Preview size slider
            previewSize = EditorGUILayout.Slider("Preview Size", previewSize, 50f, 300f);

            // Modified to show only sprite names instead of animated preview
            DrawSpriteNamePreview();

            EditorGUILayout.EndVertical();
        }

        // New method to display sprite names in preview
        private void DrawSpriteNamePreview()
        {
            Sprite[] previewSprites = GetSpritesForAnimation(animations[previewAnimIndex]);

            if (previewSprites.Length == 0)
            {
                EditorGUILayout.HelpBox("No sprites found for preview", MessageType.Warning);
                return;
            }

            var anim = animations[previewAnimIndex];
            int totalFrames = 0;

            if (sourceType == SpriteSourceType.Spritesheet && !anim.useSpecificSource)
            {
                totalFrames = Mathf.Min(anim.frameCount, previewSprites.Length - anim.startFrame);
            }
            else
            {
                totalFrames = previewSprites.Length;
            }

            if (totalFrames <= 0)
            {
                EditorGUILayout.HelpBox("No sprites found for preview", MessageType.Warning);
                return;
            }

            // Display total frame count
            EditorGUILayout.LabelField($"Total Frames: {totalFrames}");

            // Show sprite names for each frame in the animation
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Sprite Names:", EditorStyles.boldLabel);

            for (int i = 0; i < totalFrames; i++)
            {
                int spriteIndex = (sourceType == SpriteSourceType.Spritesheet && !anim.useSpecificSource)
                    ? anim.startFrame + i
                    : i;

                if (spriteIndex < previewSprites.Length)
                {
                    EditorGUILayout.LabelField($"Frame {i + 1}: {previewSprites[spriteIndex].name}");
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawGenerateButton()
        {

            if (GUILayout.Button("Generate All Animations", GUILayout.Height(30)))
            {
                GenerateAnimations();
            }

            GUILayout.Space(5);

            if (GUILayout.Button("Save Animations as SO", GUILayout.Height(30)))
            {
                string path = EditorUtility.SaveFilePanelInProject(
                    "Save Animation List",
                    "NewAnimationList",
                    "asset",
                    "Choose a file name and location to save the animation list"
                );

                if (!string.IsNullOrEmpty(path))
                {
                    SpriteConverterSO so = ScriptableObject.CreateInstance<SpriteConverterSO>();
                    so.animations = new List<AnimationDefinition>(animations);

                    AssetDatabase.CreateAsset(so, path);
                    EditorUtility.SetDirty(so);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();

                    EditorUtility.FocusProjectWindow();
                    Selection.activeObject = so;
                }
            }
        }
        #endregion

        #region Animation Preview
        private void OnInspectorUpdate()
        {
            // Force repaint to update the animation preview
            if (showPreview)
                Repaint();
        }
        #endregion

        #region Sprite Processing
        private Sprite[] GetSpritesInOrder(string path)
        {
            Sprite[] allSprites = AssetDatabase.LoadAllAssetsAtPath(path)
                .Where(x => x is Sprite)
                .Cast<Sprite>()
                .ToArray();

            switch (orderMethod)
            {
                case SpriteOrderMethod.BottomToTop:
                    return allSprites.OrderBy(s => (s.rect.y * -10000) + s.rect.x).ToArray();

                case SpriteOrderMethod.TopToBottom:
                    return allSprites.OrderBy(s => (s.rect.y * 10000) + s.rect.x).ToArray();

                case SpriteOrderMethod.LeftToRight:
                    return allSprites.OrderBy(s => (s.rect.x * 10000) + s.rect.y).ToArray();

                case SpriteOrderMethod.RightToLeft:
                    return allSprites.OrderBy(s => (s.rect.x * -10000) + s.rect.y).ToArray();

                case SpriteOrderMethod.NameAlphabetical:
                    return allSprites.OrderBy(s => s.name).ToArray();

                // Added new sort method for numeric ordering
                case SpriteOrderMethod.NumericSuffix:
                    return allSprites.OrderBy(s => GetSpriteOrderByNumericSuffix(s.name)).ToArray();

                default:
                    return allSprites;
            }
        }

        // New method to sort sprites by numeric suffix
        private string GetSpriteOrderByNumericSuffix(string spriteName)
        {
            // Pattern matches any name ending with underscore/dash followed by numbers
            // Examples: player_1, enemy-42, hero_001
            Match match = Regex.Match(spriteName, @"^(.*?)[-_](\d+)$");

            if (match.Success)
            {
                string baseName = match.Groups[1].Value;
                string numStr = match.Groups[2].Value;

                // Pad with zeros to ensure proper sorting (e.g., 001, 002, ... 010, ...)
                int numValue = int.Parse(numStr);
                return $"{baseName}_{numValue:D10}"; // Sort by base name, then padded number
            }

            // If no number pattern found, just return the original name
            return spriteName;
        }

        private Sprite[] GetSpritesForAnimation(AnimationDefinition animDef)
        {
            // If animation has specific source, use that instead of global source
            if (animDef.useSpecificSource)
            {
                if (animDef.useAnimationFolder && !string.IsNullOrEmpty(animDef.specificFolder))
                {
                    // Get sprites from specific folder
                    return GetSpritesFromFolder(animDef.specificFolder, animDef.folderSortMethod);
                }
                else if (animDef.specificSprites.Count > 0)
                {
                    // Use specific sprites
                    return animDef.specificSprites.ToArray();
                }
            }

            // Otherwise use global source
            switch (sourceType)
            {
                case SpriteSourceType.Spritesheet:
                    if (spritesheet == null) return new Sprite[0];
                    string path = AssetDatabase.GetAssetPath(spritesheet);
                    return GetSpritesInOrder(path);

                default:
                    return new Sprite[0];
            }
        }

        private Sprite[] GetSpritesFromFolder(string folderPath, SpriteOrderMethod sortMethod)
        {
            if (string.IsNullOrEmpty(folderPath) || !System.IO.Directory.Exists(folderPath))
            {
                Debug.LogWarning($"Folder path is invalid or not found: {folderPath}");
                return new Sprite[0];
            }

            // Get all PNG files in the folder
            string[] pngFiles = System.IO.Directory.GetFiles(folderPath, "*.png");
            List<Sprite> sprites = new List<Sprite>();

            // Load all sprites first
            foreach (string file in pngFiles)
            {
                // Fix path separators for Unity's asset database (always use forward slashes)
                string assetPath = file.Replace('\\', '/');

                // If not a project-relative path, make it one
                if (!assetPath.StartsWith("Assets/") && assetPath.Contains(Application.dataPath))
                {
                    assetPath = "Assets" + assetPath.Substring(Application.dataPath.Length);
                }

                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                if (sprite != null)
                {
                    sprites.Add(sprite);
                }
                else
                {
                    Debug.LogWarning($"Failed to load sprite at path: {assetPath}");
                }
            }

            // Sort based on selected method
            switch (sortMethod)
            {
                case SpriteOrderMethod.NameAlphabetical:
                    return sprites.OrderBy(s => s.name).ToArray();
                case SpriteOrderMethod.BottomToTop:
                    return sprites.OrderByDescending(s => s.name).ToArray();
                // Added numeric suffix sorting for folder sources too
                case SpriteOrderMethod.NumericSuffix:
                    return sprites.OrderBy(s => GetSpriteOrderByNumericSuffix(s.name)).ToArray();
                default:
                    return sprites.OrderBy(s => s.name).ToArray();
            }
        }
        #endregion

        #region Animation Generation
        private void GenerateAnimations()
        {
            // Ensure save directory exists
            if (!System.IO.Directory.Exists(savePath))
            {
                System.IO.Directory.CreateDirectory(savePath);
            }

            int animationsCreated = 0;

            foreach (var animDef in animations)
            {
                if (GenerateAnimation(animDef))
                {
                    animationsCreated++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Success", $"{animationsCreated} animations generated successfully!", "OK");
        }

        private bool GenerateAnimation(AnimationDefinition animDef)
        {
            // Get sprites for this animation based on source type
            Sprite[] animationSprites = GetSpritesForAnimation(animDef);

            if (animationSprites.Length == 0)
            {
                Debug.LogWarning($"No sprites found for animation {animDef.name}. Skipping.");
                return false;
            }

            // Create animation clip
            var clip = new AnimationClip();
            clip.name = animDef.name;

            if (animDef.loop)
            {
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
            }

            // For individual files mode, use all available sprites
            // For spritesheet mode, respect frameCount and startFrame
            int frameCount = animationSprites.Length;
            if (sourceType == SpriteSourceType.Spritesheet && !animDef.useSpecificSource)
            {
                frameCount = Mathf.Min(animDef.frameCount, animationSprites.Length - animDef.startFrame);
            }

            var spriteKeyFrames = new ObjectReferenceKeyframe[frameCount];

            for (int i = 0; i < frameCount; i++)
            {
                int spriteIndex = (sourceType == SpriteSourceType.Spritesheet && !animDef.useSpecificSource)
                    ? animDef.startFrame + i
                    : i;

                if (spriteIndex >= animationSprites.Length) break;

                spriteKeyFrames[i] = new ObjectReferenceKeyframe
                {
                    time = i * animDef.timeInterval,
                    value = animationSprites[spriteIndex]
                };
            }

            var binding = EditorCurveBinding.PPtrCurve(
                "",
                typeof(SpriteRenderer),
                "m_Sprite"
            );
            AnimationUtility.SetObjectReferenceCurve(clip, binding, spriteKeyFrames);

            // Ensure the save path ends with a slash
            string finalSavePath = savePath.EndsWith("/") ? savePath : savePath + "/";
            string clipPath = $"{finalSavePath}{animDef.name}.anim";

            AssetDatabase.CreateAsset(clip, clipPath);
            return true;
        }
        #endregion
    }

    [System.Serializable]
    public class AnimationDefinition
    {
        public string name = "New Animation";
        public int startFrame = 0;
        public int frameCount = 1;
        public float timeInterval = 0.1f;
        public bool loop = true;
        public bool useSpecificSource = false;

        // For folder source
        public bool useAnimationFolder = false;
        public string specificFolder = "";
        public SpriteAnimationGenerator.SpriteOrderMethod folderSortMethod = SpriteAnimationGenerator.SpriteOrderMethod.NameAlphabetical;

        // For individual sprites source
        public List<Sprite> specificSprites = new List<Sprite>();
    }


    [CreateAssetMenu(fileName = "Sprite Data", menuName = "HAS/Sprite Data")]
    public class SpriteConverterSO : ScriptableObject
    {
        public List<AnimationDefinition> animations = new();
    }
}