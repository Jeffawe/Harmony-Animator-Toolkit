using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HarmonyAnimatorToolkit
{
    public class AnimatorJsonConverterWindow : EditorWindow
    {
        private AnimatorController animator;
        private string folderPath = "";
        private string jsonOutput = "";
        private Vector2 scrollPos;
        private List<string> animationFiles = new List<string>();
        private Dictionary<string, string> animationReplacements = new Dictionary<string, string>();
        private string sourceFilter = "";
        private string endFilter = "";
        private bool autoMatchSimilarNames = false;
        private bool showNoAnim = false;
        private bool showJsonOutput = false;
        private GUIStyle headerStyle;
        private GUIStyle sectionStyle;

        private bool safetyCheck;

        [MenuItem("Tools/Harmony Anim Toolkit/Anim Converter")]
        public static void ShowWindow()
        {
            GetWindow<AnimatorJsonConverterWindow>("Animator JSON Converter");
        }

        private void OnEnable()
        {
            // Initialize styles when the window is enabled
            InitStyles();
        }

        private void InitStyles()
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

        void OnGUI()
        {
            if (headerStyle == null)
                InitStyles();

            // Top header
            EditorGUILayout.Space(5);
            GUILayout.Label("Animator JSON Converter", headerStyle);
            EditorGUILayout.HelpBox("Convert an Animator to JSON or Replace Animation in your Animator.", MessageType.Info);

            EditorGUILayout.Space(5);

            // Input section
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Input Settings", sectionStyle);

            // Animator selection with tooltip
            animator = EditorGUILayout.ObjectField(
                new GUIContent("Animator Controller",
                    "The Animator Controller asset to convert or modify"),
                animator, typeof(AnimatorController), false) as AnimatorController;

            // Folder path with tooltip
            EditorGUILayout.BeginHorizontal();
            folderPath = EditorGUILayout.TextField(
                new GUIContent("Animations Folder",
                    "Folder containing animation files to use as replacements"),
                folderPath);

            if (GUILayout.Button("Browse", GUILayout.Width(70)))
            {
                folderPath = EditorUtility.OpenFolderPanel("Select Animations Folder", "Assets", "");
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(10);

            // Replacement settings section
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Replacement Settings", sectionStyle);

            sourceFilter = EditorGUILayout.TextField(
                new GUIContent("Source Filter",
                    "Filter original animations by name (case-insensitive)"),
                sourceFilter);

            endFilter = EditorGUILayout.TextField(
                new GUIContent("Destination Filter",
                    "Filter replacement animations by name (case-insensitive)"),
                endFilter);

            autoMatchSimilarNames = EditorGUILayout.Toggle(
                new GUIContent("Auto-match Similar Names",
                    "Automatically match animations with similar names"),
                autoMatchSimilarNames);


            safetyCheck = EditorGUILayout.Toggle(
                new GUIContent("Safety Check",
                    "If enabled, animations with exact same names won't be replaced"),
                safetyCheck);

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(10);

            // Action buttons
            EditorGUILayout.BeginHorizontal();
            GUI.enabled = animator != null;
            if (GUILayout.Button(new GUIContent("Convert to JSON",
                "Generate JSON representation of the animator controller")))
            {
                jsonOutput = ConvertAnimatorToJson(animator);
                showJsonOutput = true;
            }
            GUI.enabled = !string.IsNullOrEmpty(folderPath);
            if (GUILayout.Button(new GUIContent("Scan Animations",
                "Scan the selected folder for available animations")))
            {
                animationFiles = ScanForAnimations(folderPath);
                SetupReplacements();
                showNoAnim = true;
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(10);

            // Replacements mapping section
            if (animationFiles.Count > 0 && animator != null)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUILayout.Label("Animation Replacements", sectionStyle);

                showNoAnim = false;
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Original Animation", EditorStyles.boldLabel, GUILayout.Width(150));
                GUILayout.Label("Replacement Animation", EditorStyles.boldLabel);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(5);
                scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(200));

                var states = GetAllStates(animator);
                // Filter source animations based on sourceFilter
                var filteredStates = states.Where(state =>
                    string.IsNullOrEmpty(sourceFilter) || state.ToLower().Contains(sourceFilter.ToLower())).ToList();

                if (filteredStates.Count == 0)
                {
                    EditorGUILayout.HelpBox("No animations match the source filter.", MessageType.Info);
                }

                foreach (var state in filteredStates)
                {
                    if (!animationReplacements.ContainsKey(state))
                        animationReplacements[state] = state;

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(state, GUILayout.Width(150));

                    string[] options = animationFiles
                        .Where(f => string.IsNullOrEmpty(endFilter) ||
                               f.ToLower().Contains(endFilter.ToLower()))
                        .ToArray();

                    if (options.Length == 0)
                    {
                        EditorGUILayout.LabelField("No matching animations found");
                    }
                    else
                    {
                        int selectedIndex = Mathf.Max(0, ArrayUtility.IndexOf(options, animationReplacements[state]));
                        selectedIndex = EditorGUILayout.Popup(selectedIndex, options);
                        animationReplacements[state] = options[selectedIndex];
                    }

                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndScrollView();

                GUI.enabled = filteredStates.Count > 0 && animationFiles.Count > 0;
                if (GUILayout.Button(new GUIContent("Replace Animations",
                    "Apply animation replacements to the animator controller")))
                {
                    ReplaceAnimations();
                }
                GUI.enabled = true;

                EditorGUILayout.EndVertical();
            }
            else
            {
                if (showNoAnim)
                {
                    EditorGUILayout.HelpBox("No animations found in the selected folder.", MessageType.Warning);
                }
            }

            // JSON output section
            if (showJsonOutput && !string.IsNullOrEmpty(jsonOutput))
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("JSON Output", sectionStyle);
                if (GUILayout.Button("Hide", GUILayout.Width(60)))
                {
                    showJsonOutput = false;
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.TextArea(jsonOutput, GUILayout.Height(200));

                if (GUILayout.Button("Copy to Clipboard"))
                {
                    EditorGUIUtility.systemCopyBuffer = jsonOutput;
                    Debug.Log("JSON copied to clipboard");
                }

                if (GUILayout.Button("Save to File"))
                {
                    string savePath = EditorUtility.SaveFilePanel(
                        "Save JSON file",
                        Application.dataPath,
                        "AnimatorData.json",
                        "json");

                    if (!string.IsNullOrEmpty(savePath))
                    {
                        System.IO.File.WriteAllText(savePath, jsonOutput);
                        Debug.Log($"JSON saved to: {savePath}");
                    }
                }

                EditorGUILayout.EndVertical();
            }
        }

        private string ConvertAnimatorToJson(AnimatorController animator)
        {
            AnimatorJsonData jsonData = new AnimatorJsonData();
            jsonData.transitions = GetAllTransitions(animator);
            return JsonUtility.ToJson(jsonData, true);
        }

        public static List<JsonTransition> GetAllTransitions(AnimatorController animator)
        {
            List<JsonTransition> transitionsList = new List<JsonTransition>();

            foreach (var layer in animator.layers)
            {
                var stateMachine = layer.stateMachine;
                foreach (var state in stateMachine.states)
                {
                    foreach (var transition in state.state.transitions)
                    {
                        transitionsList.Add(new JsonTransition
                        {
                            startstate = GetJsonState(state.state),
                            endstate = GetJsonState(transition.destinationState),
                            conditions = transition.conditions.Select(c => GetJsonCondition(c, animator)).ToList()
                        });
                    }
                }
            }

            return transitionsList;
        }

        private static JsonState GetJsonState(AnimatorState state)
        {
            JsonState jsonState = new JsonState();

            if (state.motion is BlendTree blendTree)
            {
                jsonState.type = "BlendTree";
                jsonState.animationname = state.name;
                jsonState.blendtree = GetJsonBlendTreeData(blendTree);
            }
            else
            {
                jsonState.type = "Animation";
                jsonState.animationname = state.motion?.name ?? state.name;
            }

            return jsonState;
        }

        private static JsonBlendTreeData GetJsonBlendTreeData(BlendTree blendTree)
        {
            JsonBlendTreeData blendTreeData = new JsonBlendTreeData
            {
                blendtype = blendTree.blendType == BlendTreeType.Simple1D ? "OneD" : "TwoD",
                parametername = blendTree.blendType == BlendTreeType.Simple1D ? blendTree.blendParameter : null,
                parameternames = blendTree.blendType == BlendTreeType.Simple1D ? null : new[] { blendTree.blendParameter, blendTree.blendParameterY },
                motions = blendTree.children.Select(m => new JsonBlendTreeMotion
                {
                    animationname = m.motion?.name,
                    threshold = blendTree.blendType == BlendTreeType.Simple1D ? m.threshold : 0f,
                    threshold2d = blendTree.blendType == BlendTreeType.Simple1D ? null : new[] { m.position.x, m.position.y }
                }).ToList()
            };

            return blendTreeData;
        }

        private static JsonCondition GetJsonCondition(AnimatorCondition condition, AnimatorController animator)
        {
            JsonCondition jsonCondition = new JsonCondition
            {
                name = condition.parameter
            };

            switch (condition.mode)
            {
                case AnimatorConditionMode.If:
                case AnimatorConditionMode.IfNot:
                    jsonCondition.type = "Bool";
                    jsonCondition.boolvalue = condition.mode == AnimatorConditionMode.If;
                    break;
                case AnimatorConditionMode.Greater:
                case AnimatorConditionMode.Less:
                    jsonCondition.type = "Float"; // or "Int" depending on your needs
                    jsonCondition.numbervalue = condition.threshold;
                    jsonCondition.comparison = condition.mode == AnimatorConditionMode.Greater ? "Greater" : "Less";
                    break;
                case AnimatorConditionMode.Equals:
                case AnimatorConditionMode.NotEqual:
                    jsonCondition.type = "Int"; // or "Float" depending on your needs
                    jsonCondition.numbervalue = condition.threshold;
                    jsonCondition.comparison = condition.mode == AnimatorConditionMode.Equals ? "Equals" : "NotEquals";
                    break;
            }

            // Handle Trigger case (Unity treats triggers as conditions with no threshold)
            if (animator.parameters.Any(p => p.name == condition.parameter && p.type == AnimatorControllerParameterType.Trigger))
            {
                jsonCondition.type = "Trigger";
                jsonCondition.boolvalue = false; // Triggers don't have a value in Unity
                jsonCondition.comparison = "Equals"; // Default for triggers
            }

            return jsonCondition;
        }


        private List<string> ScanForAnimations(string path)
        {
            var animations = new List<string>();

            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                return animations;

            string relativePath = path.Replace(Application.dataPath, "Assets");
            string[] animGuids = AssetDatabase.FindAssets("t:AnimationClip", new[] { relativePath });

            foreach (string guid in animGuids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
                if (clip != null && !clip.name.Contains("__preview__"))
                    animations.Add(clip.name);
            }

            string[] modelGuids = AssetDatabase.FindAssets("t:Model", new[] { relativePath });
            foreach (string guid in modelGuids)
            {
                string modelPath = AssetDatabase.GUIDToAssetPath(guid);
                UnityEngine.Object[] importedAssets = AssetDatabase.LoadAllAssetsAtPath(modelPath);
                foreach (var asset in importedAssets)
                {
                    if (asset is AnimationClip clip && !clip.name.Contains("__preview__"))
                        animations.Add(clip.name);
                }
            }

            return animations;
        }

        private List<string> GetAllStates(AnimatorController animator)
        {
            var states = new List<string>();
            foreach (var layer in animator.layers)
            {
                foreach (var state in layer.stateMachine.states)
                {
                    var motion = state.state.motion;

                    if (motion is AnimationClip clip)
                    {
                        states.Add(clip.name);
                    }
                    else if (motion is BlendTree blendTree)
                    {
                        states.AddRange(GetClipsFromBlendTree(blendTree).Select(c => c.name));
                    }
                }
            }
            return states.Distinct().ToList();
        }

        private List<AnimationClip> GetClipsFromBlendTree(BlendTree tree)
        {
            var clips = new List<AnimationClip>();
            for (int i = 0; i < tree.children.Length; i++)
            {
                var child = tree.children[i];
                if (child.motion is AnimationClip clip)
                {
                    clips.Add(clip);
                }
                else if (child.motion is BlendTree childTree)
                {
                    clips.AddRange(GetClipsFromBlendTree(childTree)); // Recursive for nested trees
                }
            }
            return clips;
        }


        private void SetupReplacements()
        {
            var states = GetAllStates(animator);
            animationReplacements.Clear();

            foreach (var state in states)
            {
                if (autoMatchSimilarNames)
                {
                    var match = animationFiles.FirstOrDefault(f => f.ToLower().Contains(state.ToLower()));
                    animationReplacements[state] = match ?? state;
                }
                else
                {
                    animationReplacements[state] = state;
                }
            }
        }

        private void ReplaceAnimations()
        {
            if (animator == null)
            {
                Debug.LogError("Animator is null");
                return;
            }
            if (animationReplacements == null || animationReplacements.Count == 0)
            {
                Debug.LogWarning("No animation replacements specified");
                return;
            }

            try
            {
                int replacedCount = 0;
                string assetsRelativePath = folderPath.Replace(Application.dataPath, "Assets");

                // Log initial information for debugging
                Debug.Log($"Starting animation replacement process in folder: {assetsRelativePath}");
                Debug.Log($"Number of animation replacements specified: {animationReplacements.Count}");

                foreach (var layer in animator.layers)
                {
                    // Process states in the state machine
                    replacedCount += ProcessStateMachine(layer.stateMachine, assetsRelativePath);
                }

                if (replacedCount > 0)
                {
                    EditorUtility.SetDirty(animator);
                    AssetDatabase.SaveAssets();
                    Debug.Log($"Successfully replaced {replacedCount} animations");
                }
                else
                {
                    Debug.LogWarning("No animations were replaced. This might indicate a problem with the replacement mappings or animation names.");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error replacing animations: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private int ProcessStateMachine(AnimatorStateMachine stateMachine, string assetsRelativePath)
        {
            int replacedCount = 0;

            // Process states in the current state machine
            foreach (var childState in stateMachine.states)
            {
                replacedCount += ProcessState(childState.state, assetsRelativePath);
            }

            // Process child state machines recursively
            foreach (var childStateMachine in stateMachine.stateMachines)
            {
                replacedCount += ProcessStateMachine(childStateMachine.stateMachine, assetsRelativePath);
            }

            return replacedCount;
        }

        private int ProcessState(AnimatorState state, string assetsRelativePath)
        {
            int replacedCount = 0;

            // Handle direct animation clips
            if (state.motion is AnimationClip clip)
            {
                AnimationClip newClip = FindReplacementAnimation(clip.name, assetsRelativePath);
                if (newClip != null)
                {
                    state.motion = newClip;
                    replacedCount++;
                }
            }
            // Handle blend trees
            else if (state.motion is BlendTree blendTree)
            {
                replacedCount += ProcessBlendTree(blendTree, assetsRelativePath);
            }

            return replacedCount;
        }

        private int ProcessBlendTree(BlendTree blendTree, string assetsRelativePath)
        {
            int replacedCount = 0;

            // Get all child motions in the blend tree
            ChildMotion[] childMotions = blendTree.children;

            for (int i = 0; i < childMotions.Length; i++)
            {
                // Handle direct animation clips in blend tree
                if (childMotions[i].motion is AnimationClip clip)
                {
                    AnimationClip newClip = FindReplacementAnimation(clip.name, assetsRelativePath);
                    if (newClip != null)
                    {
                        // Create a new child motion with the replacement clip
                        ChildMotion newChildMotion = childMotions[i];
                        newChildMotion.motion = newClip;
                        childMotions[i] = newChildMotion;
                        replacedCount++;
                    }
                }
                // Handle nested blend trees recursively
                else if (childMotions[i].motion is BlendTree nestedBlendTree)
                {
                    replacedCount += ProcessBlendTree(nestedBlendTree, assetsRelativePath);
                }
            }

            // Apply the modified child motions back to the blend tree
            blendTree.children = childMotions;

            return replacedCount;
        }

        private AnimationClip FindReplacementAnimation(string originalClipName, string assetsRelativePath)
        {
            // Check if this animation needs to be replaced
            if (!animationReplacements.TryGetValue(originalClipName, out string newAnimPath) ||
                newAnimPath == originalClipName)
            {
                if(safetyCheck) return null;

            }

            // First check if the replacement value is a full path or just a name
            AnimationClip newClip = null;
            bool isPath = newAnimPath.Contains("/") || newAnimPath.EndsWith(".anim");

            if (isPath)
            {
                // If it's a path, use it directly (ensure it starts with "Assets/")
                string fullPath = newAnimPath;
                if (!fullPath.StartsWith("Assets/"))
                {
                    // If not an absolute path, make it relative to the specified folder
                    fullPath = Path.Combine(assetsRelativePath, newAnimPath);
                }

                // Normalize path separators for Unity
                fullPath = fullPath.Replace("\\", "/");

                // Add .anim extension if missing
                if (!fullPath.EndsWith(".anim"))
                {
                    fullPath += ".anim";
                }

                newClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(fullPath);

                if (newClip != null)
                {
                    Debug.Log($"Found animation at specific path: {fullPath}");
                }
                else
                {
                    Debug.LogWarning($"Failed to find animation at path: {fullPath}");
                }
            }
            else
            {
                // Try to find by name within the specified folder first
                string[] guids = AssetDatabase.FindAssets($"t:AnimationClip {newAnimPath}", new[] { assetsRelativePath });

                List<AnimationClip> foundClips = new List<AnimationClip>();
                foreach (string guid in guids)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    AnimationClip foundClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);

                    // Check if this is a clip with exactly matching name (not just containing the search term)
                    if (foundClip != null && foundClip.name == newAnimPath)
                    {
                        foundClips.Add(foundClip);
                    }
                }

                // If we found exactly one clip with the right name in the specified folder, use it
                if (foundClips.Count == 1)
                {
                    newClip = foundClips[0];
                }
                // If we found multiple clips with the same name, log a warning and use the first one
                else if (foundClips.Count > 1)
                {
                    Debug.LogWarning($"Found multiple animations named '{newAnimPath}'. Using the first one found. Consider using full paths in your replacements dictionary.");
                    newClip = foundClips[0];

                    // Log all found locations to help with debugging
                    for (int i = 0; i < foundClips.Count; i++)
                    {
                        string path = AssetDatabase.GetAssetPath(foundClips[i]);
                        Debug.Log($"  Option {i + 1}: {path} (Used: {i == 0})");
                    }
                }
                // If we didn't find any in the specified folder, try searching the entire project
                else if (foundClips.Count == 0)
                {
                    Debug.Log($"No animation named '{newAnimPath}' found in {assetsRelativePath}, searching entire project...");
                    string[] allGuids = AssetDatabase.FindAssets($"t:AnimationClip {newAnimPath}");

                    foreach (string guid in allGuids)
                    {
                        string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                        AnimationClip foundClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);

                        if (foundClip != null && foundClip.name == newAnimPath)
                        {
                            newClip = foundClip;
                            Debug.Log($"Found animation {newAnimPath} in project at {assetPath}");
                            break;
                        }
                    }
                }
            }

            if (newClip != null)
            {
                Debug.Log($"Switched animation {originalClipName} with {newClip.name} at {AssetDatabase.GetAssetPath(newClip)} (Embedded: {AssetDatabase.IsSubAsset(newClip)})");
                return newClip;
            }
            else
            {
                Debug.LogWarning($"Could not find replacement animation: {newAnimPath}");
                return null;
            }
        }
    }
}