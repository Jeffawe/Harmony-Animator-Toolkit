using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System;
using UnityEditor.Animations;

namespace HarmonyAnimatorConverter
{
    public class TransitionJSONImporter : EditorWindow
    {
        private string jsonFilePath = "";
        private TextAsset jsonTextAsset;
        private AnimatorConverterSO resultSO;
        private Vector2 scrollPosition;
        private GUIStyle headerStyle;
        private GUIStyle boxStyle;
        private bool showJsonPreview = true;
        private string jsonPreviewText = "";
        private string saveAssetPath = "Assets/GeneratedTransitionData.asset";
        private bool hasValidated;
        private bool value;
        private List<string> errors;
        private AnimatorController animator;

        public MonoBehaviour customFinderScript;
        public MonoBehaviour customImporterScript;
        public string animationFolderPath = "Assets/";

        [MenuItem("Tools/Harmony Anim Toolkit/Anim JSON Importer")]
        public static void ShowWindow()
        {
            GetWindow<TransitionJSONImporter>("Anim JSON Importer");
        }

        private void InitializeStyles()
        {
            headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                margin = new RectOffset(0, 0, 10, 10)
            };

            boxStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(10, 10, 10, 10),
                margin = new RectOffset(0, 0, 5, 5)
            };
        }

        private void OnGUI()
        {
            if (headerStyle == null || boxStyle == null)
            {
                InitializeStyles();
            }

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            EditorGUILayout.BeginVertical(boxStyle);

            DrawMainHeader();
            DrawJsonSelection();
            DrawSetupSection();
            DrawExistingAnimationSection();
            DrawJsonPreview();
            DrawValidateButton();
            DrawImportButton();
            DrawResultSection();

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        private void DrawMainHeader()
        {
            GUILayout.Label("Animation JSON Importer", headerStyle);
            EditorGUILayout.HelpBox("Import JSON files to create Animator or AnimatorSO asset for use with the Animation Controller Generator.", MessageType.Info);
        }

        private void DrawJsonSelection()
        {
            EditorGUILayout.BeginVertical(boxStyle);
            EditorGUILayout.LabelField("JSON Source", EditorStyles.boldLabel);

            // TextAsset field
            EditorGUI.BeginChangeCheck();
            jsonTextAsset = (TextAsset)EditorGUILayout.ObjectField(
                new GUIContent("JSON TextAsset", "Drag and drop a JSON file here or select one."),
                jsonTextAsset,
                typeof(TextAsset),
                false
                );
            if (EditorGUI.EndChangeCheck() && jsonTextAsset != null)
            {
                jsonPreviewText = jsonTextAsset.text;
                TryParseJson(jsonPreviewText);
            }

            // File path field with browse button
            EditorGUILayout.BeginHorizontal();
            jsonFilePath = EditorGUILayout.TextField(
                new GUIContent("JSON File Path", "Manually enter a path or use the Browse button to select a file."),
                jsonFilePath
            );
            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                string path = EditorUtility.OpenFilePanel("Select JSON File", "", "json");
                if (!string.IsNullOrEmpty(path))
                {
                    jsonFilePath = path;
                    try
                    {
                        jsonPreviewText = File.ReadAllText(path);
                        TryParseJson(jsonPreviewText);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Error reading JSON file: {e.Message}");
                    }
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawSetupSection()
        {
            EditorGUILayout.BeginVertical(boxStyle);

            EditorGUILayout.BeginHorizontal();
            animationFolderPath = EditorGUILayout.TextField(
                new GUIContent("Animation Folder", "Select a folder containing animation assets."),
                animationFolderPath
            );
            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                string path = EditorUtility.OpenFolderPanel("Select Animation Folder", Application.dataPath, "");
                if (!string.IsNullOrEmpty(path) && path.StartsWith(Application.dataPath))
                {
                    animationFolderPath = "Assets" + path.Substring(Application.dataPath.Length);
                }
                else
                {
                    Debug.LogWarning("Please select a folder inside the Assets directory.");
                }
            }
            EditorGUILayout.EndHorizontal();

            customFinderScript = (MonoBehaviour)EditorGUILayout.ObjectField(
                new GUIContent("Custom Finder Script", "Assign a Custom MonoBehaviour script that helps locate animation assets. Default used if null"),
                customFinderScript,
                typeof(MonoBehaviour),
                false
            );

            customImporterScript = (MonoBehaviour)EditorGUILayout.ObjectField(
                new GUIContent("Custom JSON Importer", "Assign a custom script for custom importing of JSON data. Default used if null"),
                customImporterScript,
                typeof(MonoBehaviour),
                false
            );

            EditorGUILayout.EndVertical();
        }

        private void DrawExistingAnimationSection()
        {
            EditorGUILayout.BeginVertical(boxStyle);

            EditorGUILayout.BeginHorizontal();
            // Animator selection with tooltip
            animator = EditorGUILayout.ObjectField(
                new GUIContent("Animator Controller",
                    "An exisitng Animator Controller asset to add this JSON to"),
                animator, typeof(AnimatorController), false) as AnimatorController;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawJsonPreview()
        {
            EditorGUILayout.Space(5);
            showJsonPreview = EditorGUILayout.Foldout(showJsonPreview, "JSON Preview", true);

            if (showJsonPreview && !string.IsNullOrEmpty(jsonPreviewText))
            {
                EditorGUILayout.BeginVertical(boxStyle);
                EditorGUILayout.SelectableLabel(jsonPreviewText, GUILayout.Height(200));
                EditorGUILayout.EndVertical();
            }
        }

        private void DrawValidateButton()
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginVertical(boxStyle);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Validate JSON", GUILayout.Height(30)))
            {
                errors = new List<string>();
                value = AnimatorJsonValidator.ValidateJson(jsonPreviewText, out errors);
                hasValidated = true; // Set hasValidated flag to true after validation
            }

            EditorGUILayout.EndHorizontal();

            if (hasValidated)
            {
                if (value)
                {
                    EditorGUILayout.HelpBox("Everything is good", MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox("There are issues with the JSON. Errors are printed to the console", MessageType.Error);
                    if (errors.Count > 0)
                    {
                        foreach (var error in errors)
                        {
                            Debug.LogError(error);
                        }
                    }
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawImportButton()
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginVertical(boxStyle);

            EditorGUILayout.BeginHorizontal();
            saveAssetPath = EditorGUILayout.TextField(new GUIContent("Save Asset Path", "Location to store the Scriptable Object Created"), saveAssetPath);
            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                string path = EditorUtility.SaveFilePanelInProject("Save Scriptable Object", "AnimatorTransitions", "asset", "Choose where to save the AnimatorConverterSO");
                if (!string.IsNullOrEmpty(path))
                {
                    saveAssetPath = path;
                }
            }

            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Generate Animator", GUILayout.Height(30)))
            {
                ImportJsonAndGenerate();
            }

            if (GUILayout.Button("Create SO", GUILayout.Height(30)))
            {
                ImportJsonAndCreateSO();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawResultSection()
        {
            if (resultSO != null)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.BeginVertical(boxStyle);
                EditorGUILayout.LabelField("Import Result", EditorStyles.boldLabel);
                EditorGUILayout.ObjectField("Generated SO", resultSO, typeof(AnimatorConverterSO), false);

                if (GUILayout.Button("Open in Animation Controller Generator"))
                {
                    AnimationConverter converterWindow = GetWindow<AnimationConverter>("Animator Generator");
                    converterWindow.transitionData = resultSO;
                    converterWindow.animationFolderPath = animationFolderPath;
                    converterWindow.customFinderScript = customFinderScript;

                    this.Close();
                }

                EditorGUILayout.EndVertical();
            }
        }

        public static List<AnimationTransition> GetExistingAnimations(AnimatorController controller)
        {
            List<AnimationTransition> animationTransitions = new List<AnimationTransition>();
            List<JsonTransition> _transitions = AnimatorJsonConverterWindow.GetAllTransitions(controller);
            animationTransitions = ConvertJsonConditionToTransitionCondition(_transitions);

            return animationTransitions;
        }


        private void ImportJsonAndGenerate()
        {
            if (!string.IsNullOrEmpty(jsonPreviewText))
            {
                try
                {
                    List<AnimationTransition> existingTransitions = new List<AnimationTransition>();
                    if (animator)
                    {
                        existingTransitions = GetExistingAnimations(animator);
                    }

                    List<AnimationTransition> _transitions = ConvertJSONToAnimationTransitions(jsonPreviewText.ToLower());
                    if(existingTransitions != null && existingTransitions.Count > 0)
                    {
                        _transitions.AddRange(existingTransitions);
                    }

                    if (_transitions != null)
                    {
                        AnimationConverter converterWindow = GetWindow<AnimationConverter>("Animator Generator");
                        converterWindow.transitions = _transitions;
                        converterWindow.animator = this.animator;
                        converterWindow.transitionsGenerated = true;
                        converterWindow.animationFolderPath = animationFolderPath;
                        converterWindow.customFinderScript = customFinderScript;

                        this.Close();
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error creating AnimatorConverterSO: {e.Message}");
                }
            }
            else
            {
                Debug.LogError("No JSON data available to import");
            }
        }

        private void ImportJsonAndCreateSO()
        {
            if (!string.IsNullOrEmpty(jsonPreviewText))
            {
                try
                {
                    AnimatorConverterSO asset = ConvertJsonToSO(jsonPreviewText.ToLower(), animator);

                    if (asset != null)
                    {
                        AssetDatabase.CreateAsset(asset, saveAssetPath);
                        AssetDatabase.SaveAssets();
                        AssetDatabase.Refresh();

                        resultSO = AssetDatabase.LoadAssetAtPath<AnimatorConverterSO>(saveAssetPath);

                        EditorUtility.FocusProjectWindow();
                        Selection.activeObject = resultSO;

                        Debug.Log($"Successfully created AnimatorConverterSO at {saveAssetPath}");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error creating AnimatorConverterSO: {e.Message}");
                }
            }
            else
            {
                Debug.LogError("No JSON data available to import");
            }
        }

        private List<AnimationTransition> ConvertJSONToAnimationTransitions(string jsonText)
        {
            if (customImporterScript is IJSONConverter jsonConv)
            {
                return jsonConv.ConvertJSONToAnimationTransitions(jsonText);
            }

            AnimatorJsonData jsonData = JsonUtility.FromJson<AnimatorJsonData>(jsonText);

            List<AnimationTransition> transitions = new();

            Dictionary<string, BlendTreeData> processedBlendTrees = new Dictionary<string, BlendTreeData>();

            foreach (var jsonTransition in jsonData.transitions)
            {
                AnimationTransition transition = new AnimationTransition
                {
                    startType = ParseStateType(jsonTransition.startstate.type),
                    endType = ParseStateType(jsonTransition.endstate.type),
                    startAnimationName = jsonTransition.startstate.animationname,
                    endAnimationName = jsonTransition.endstate.animationname,
                    conditions = new List<TransitionCondition>()
                };

                // Process start state
                if (transition.startType == StateType.BlendTree)
                {
                    // If we've seen this blend tree before, reuse it
                    if (processedBlendTrees.ContainsKey(jsonTransition.endstate.animationname))
                    {
                        transition.startBlendTree = processedBlendTrees[jsonTransition.endstate.animationname];
                    }
                    // Otherwise, process it and cache it
                    else if (jsonTransition.startstate.blendtree != null)
                    {
                        transition.startBlendTree = ConvertBlendTreeData(jsonTransition.startstate.blendtree);
                        // Cache the new blend tree for future reuse
                        processedBlendTrees[jsonTransition.endstate.animationname] = transition.startBlendTree;
                    }
                }

                // Process end state
                if (transition.endType == StateType.BlendTree)
                {
                    // If we've seen this blend tree before, reuse it
                    if (processedBlendTrees.ContainsKey(jsonTransition.endstate.animationname))
                    {
                        transition.endBlendTree = processedBlendTrees[jsonTransition.endstate.animationname];
                    }
                    // Otherwise, process it and cache it
                    else if (jsonTransition.endstate.blendtree != null)
                    {
                        transition.endBlendTree = ConvertBlendTreeData(jsonTransition.endstate.blendtree);
                        // Cache the new blend tree for future reuse
                        processedBlendTrees[jsonTransition.endstate.animationname] = transition.endBlendTree;
                    }
                }

                // Process conditions
                if (jsonTransition.conditions != null)
                {
                    foreach (var jsonCondition in jsonTransition.conditions)
                    {
                        TransitionCondition condition = new TransitionCondition
                        {
                            name = jsonCondition.name,
                            type = ParseConditionType(jsonCondition.type),
                            boolValue = jsonCondition.boolvalue,
                            numberValue = jsonCondition.numbervalue,
                            comparison = ParseComparisonType(jsonCondition.comparison)
                        };

                        transition.conditions.Add(condition);
                    }
                }

                transitions.Add(transition);
            }

            return transitions;
        }

        private void TryParseJson(string jsonText)
        {
            try
            {
                // Validate JSON by attempting to deserialize it
                JsonUtility.FromJson<AnimatorJsonData>(jsonText);
                Debug.Log("JSON is valid");
            }
            catch (Exception e)
            {
                Debug.LogError($"Invalid JSON: {e.Message}");
            }
        }

        private AnimatorConverterSO ConvertJsonToSO(string jsonText, AnimatorController animator)
        {
            if (customImporterScript is IJSONConverter jsonConv)
            {
                return jsonConv.ConvertJsonToSO(jsonText);
            }

            AnimatorJsonData jsonData = JsonUtility.FromJson<AnimatorJsonData>(jsonText);

            AnimatorConverterSO so = ScriptableObject.CreateInstance<AnimatorConverterSO>();
            so.transitions = new List<AnimationTransition>();

            foreach (var jsonTransition in jsonData.transitions)
            {
                AnimationTransition transition = new AnimationTransition
                {
                    startType = ParseStateType(jsonTransition.startstate.type),
                    endType = ParseStateType(jsonTransition.endstate.type),
                    startAnimationName = jsonTransition.endstate.animationname,
                    endAnimationName = jsonTransition.endstate.animationname,
                    conditions = new List<TransitionCondition>()
                };

                // Process start state
                if (transition.startType == StateType.BlendTree && jsonTransition.startstate.blendtree != null)
                {
                    transition.startBlendTree = ConvertBlendTreeData(jsonTransition.startstate.blendtree);
                }

                // Process end state
                if (transition.endType == StateType.BlendTree && jsonTransition.endstate.blendtree != null)
                {
                    transition.endBlendTree = ConvertBlendTreeData(jsonTransition.endstate.blendtree);
                }

                // Process conditions
                if (jsonTransition.conditions != null)
                {
                    foreach (var jsonCondition in jsonTransition.conditions)
                    {
                        TransitionCondition condition = new TransitionCondition
                        {
                            name = jsonCondition.name,
                            type = ParseConditionType(jsonCondition.type),
                            boolValue = jsonCondition.boolvalue,
                            numberValue = jsonCondition.numbervalue,
                            comparison = ParseComparisonType(jsonCondition.comparison)
                        };

                        transition.conditions.Add(condition);
                    }
                }

                so.transitions.Add(transition);
            }

            List<AnimationTransition> existingTransitions = new List<AnimationTransition>();
            if (animator)
            {
                existingTransitions = GetExistingAnimations(animator);
            }

            List<AnimationTransition> _transitions = ConvertJSONToAnimationTransitions(jsonPreviewText.ToLower());
            if (existingTransitions != null && existingTransitions.Count > 0)
            {
                so.transitions.AddRange(existingTransitions);
            }

            return so;
        }

        private StateType ParseStateType(string type)
        {
            return type.ToLower() == "blendtree" ? StateType.BlendTree : StateType.Animation;
        }

        private ConditionType ParseConditionType(string type)
        {
            return type.ToLower() switch
            {
                "bool" => ConditionType.Bool,
                "float" => ConditionType.Float,
                "int" => ConditionType.Int,
                "trigger" => ConditionType.Trigger,
                _ => ConditionType.Bool
            };
        }

        private ComparisonType ParseComparisonType(string comparison)
        {
            return comparison.ToLower() switch
            {
                "greater" => ComparisonType.Greater,
                "less" => ComparisonType.Less,
                "notequals" => ComparisonType.NotEquals,
                "equals" => ComparisonType.Equals,
                _ => ComparisonType.Equals
            };
        }

        private BlendType ParseBlendType(string type)
        {
            return type.ToLower() == "twod" ? BlendType.TwoD : BlendType.OneD;
        }

        private BlendTreeData ConvertBlendTreeData(JsonBlendTreeData jsonBlendTree)
        {
            // Parse blend type once
            BlendType blendType = ParseBlendType(jsonBlendTree.blendtype);

            BlendTreeData blendTreeData = new BlendTreeData
            {
                parameterName = jsonBlendTree.parametername,
                parameterNames = jsonBlendTree.parameternames ?? (jsonBlendTree.blendtype.ToLower() == "twod" ? new string[] { "Default_X", "Default_Y" } : null),
                blendType = blendType,
                motions = new List<BlendTreeMotion>()
            };

            // Check if motions exist
            if (jsonBlendTree.motions != null && jsonBlendTree.motions.Count > 0)
            {
                foreach (var jsonMotion in jsonBlendTree.motions)
                {
                    // Find animation (assuming this returns AnimationClip or similar)
                    var animation = FindAnimationByName(jsonMotion.animationname, animationFolderPath);

                    BlendTreeMotion motion = new BlendTreeMotion
                    {
                        animation = animation
                    };

                    // Set threshold based on blend type
                    if (blendType == BlendType.OneD)
                    {
                        // For 1D, use the single threshold value
                        motion.threshold = jsonMotion.threshold;
                        motion.threshold2D = Vector2.zero; // Or leave null if BlendTreeMotion supports it
                    }
                    else // TwoD
                    {
                        // For 2D, validate and use threshold2D
                        if (jsonMotion.threshold2d != null && jsonMotion.threshold2d.Length == 2)
                        {
                            motion.threshold2D = new Vector2(jsonMotion.threshold2d[0], jsonMotion.threshold2d[1]);
                        }
                        else
                        {
                            Debug.LogError($"Invalid threshold2D for animation '{jsonMotion.animationname}' in 2D blend type. Expected array of 2 floats, got: {(jsonMotion.threshold2d == null ? "null" : jsonMotion.threshold2d.Length.ToString())} elements. Using default [0, 0].");
                            motion.threshold2D = Vector2.zero; // Fallback value
                        }
                        motion.threshold = 0f; // Irrelevant for 2D, set to a default
                    }

                    blendTreeData.motions.Add(motion);
                }
            }
            else
            {
                Debug.LogWarning("No motions found in JsonBlendTreeData.");
            }

            return blendTreeData;
        }

        private AnimationClip FindAnimationByName(string animationName, string animationFolderPath)
        {
            if (string.IsNullOrEmpty(animationFolderPath))
            {
                Debug.LogError("FindAnimationByName: Animation folder path is null or empty.");
                return null;
            }

            if (string.IsNullOrEmpty(animationName))
            {
                Debug.LogError("FindAnimationByName: Animation name is null or empty.");
                return null;
            }

            if (!Directory.Exists(animationFolderPath))
            {
                Debug.LogError($"FindAnimationByName: The specified folder does not exist: {animationFolderPath}");
                return null;
            }

            if (customFinderScript is IAnimationFinder finder)
            {
                return finder.FindAnimation(animationName, animationFolderPath);
            }

            // Normalize folder path to Unity AssetDatabase format
            string relativePath = animationFolderPath.Replace(Application.dataPath, "Assets");

            // Search for standalone animation clips in the folder
            string[] animGuids = AssetDatabase.FindAssets($"t:AnimationClip {animationName}", new[] { relativePath });
            foreach (string guid in animGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip != null && !clip.name.Contains("__preview__") && clip.name.Equals(animationName, StringComparison.OrdinalIgnoreCase))
                {
                    //Debug.Log($"FindAnimationByName: Found animation clip '{animationName}' at {path}");
                    return clip;
                }
            }

            // Search for model files in the specified folder
            string[] modelGuids = AssetDatabase.FindAssets("t:Model", new[] { relativePath });
            foreach (string guid in modelGuids)
            {
                string modelPath = AssetDatabase.GUIDToAssetPath(guid);
                UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(modelPath);

                foreach (UnityEngine.Object asset in assets)
                {
                    AnimationClip clip = asset as AnimationClip;
                    if (clip != null && !clip.name.Contains("__preview__") && clip.name.Equals(animationName, StringComparison.OrdinalIgnoreCase))
                    {
                        //Debug.Log($"FindAnimationByName: Found animation '{animationName}' inside model file {modelPath}");
                        return clip;
                    }
                }
            }

            Debug.LogWarning($"FindAnimationByName: Animation clip '{animationName}' not found in folder: {animationFolderPath}");
            return null;
        }

        public static List<AnimationTransition> ConvertJsonConditionToTransitionCondition(List<JsonTransition> transitions)
        {
            List<AnimationTransition> animationTransitions = new List<AnimationTransition>();

            foreach (JsonTransition jsonTransition in transitions)
            {
                AnimationTransition animTransition = new AnimationTransition();

                // Convert start state
                if (jsonTransition.startstate != null)
                {
                    animTransition.startType = jsonTransition.startstate.type.ToLower() == "blendtree" ?
                        StateType.BlendTree : StateType.Animation;

                    animTransition.startAnimationName = jsonTransition.startstate.animationname;

                    // Convert blend tree if present
                    if (animTransition.startType == StateType.BlendTree && jsonTransition.startstate.blendtree != null)
                    {
                        animTransition.startBlendTree = ConvertJsonBlendTreeToBlendTreeData(jsonTransition.startstate.blendtree);
                    }
                }

                // Convert end state
                if (jsonTransition.endstate != null)
                {
                    animTransition.endType = jsonTransition.endstate.type.ToLower() == "blendtree" ?
                        StateType.BlendTree : StateType.Animation;

                    animTransition.endAnimationName = jsonTransition.endstate.animationname;

                    // Convert blend tree if present
                    if (animTransition.endType == StateType.BlendTree && jsonTransition.endstate.blendtree != null)
                    {
                        animTransition.endBlendTree = ConvertJsonBlendTreeToBlendTreeData(jsonTransition.endstate.blendtree);
                    }
                }

                // Convert conditions
                if (jsonTransition.conditions != null)
                {
                    foreach (JsonCondition jsonCondition in jsonTransition.conditions)
                    {
                        TransitionCondition condition = ConvertJsonConditionToTransitionCondition(jsonCondition);
                        animTransition.conditions.Add(condition);
                    }
                }

                animationTransitions.Add(animTransition);
            }

            return animationTransitions;
        }

        private static BlendTreeData ConvertJsonBlendTreeToBlendTreeData(JsonBlendTreeData jsonBlendTree)
        {
            BlendTreeData blendTreeData = new BlendTreeData();

            blendTreeData.parameterName = jsonBlendTree.parametername;
            blendTreeData.parameterNames = jsonBlendTree.parameternames;
            blendTreeData.blendType = jsonBlendTree.blendtype.ToLower() == "twod" ? BlendType.TwoD : BlendType.OneD;

            // Convert motions
            if (jsonBlendTree.motions != null)
            {
                foreach (JsonBlendTreeMotion jsonMotion in jsonBlendTree.motions)
                {
                    BlendTreeMotion motion = new BlendTreeMotion();

                    // Just store the animation name, actual animation clip will be loaded later
                    motion.threshold = jsonMotion.threshold;

                    // Convert 2D threshold if present
                    if (jsonMotion.threshold2d != null && jsonMotion.threshold2d.Length >= 2)
                    {
                        motion.threshold2D = new Vector2(jsonMotion.threshold2d[0], jsonMotion.threshold2d[1]);
                    }

                    blendTreeData.motions.Add(motion);
                }
            }

            return blendTreeData;
        }

        private static TransitionCondition ConvertJsonConditionToTransitionCondition(JsonCondition jsonCondition)
        {
            TransitionCondition condition = new TransitionCondition();

            condition.name = jsonCondition.name;

            // Convert condition type
            switch (jsonCondition.type.ToLower())
            {
                case "bool":
                    condition.type = ConditionType.Bool;
                    break;
                case "float":
                    condition.type = ConditionType.Float;
                    break;
                case "int":
                    condition.type = ConditionType.Int;
                    break;
                case "trigger":
                    condition.type = ConditionType.Trigger;
                    break;
                default:
                    condition.type = ConditionType.Bool;
                    break;
            }

            condition.boolValue = jsonCondition.boolvalue;
            condition.numberValue = jsonCondition.numbervalue;

            // Convert comparison type
            switch (jsonCondition.comparison.ToLower())
            {
                case "equals":
                    condition.comparison = ComparisonType.Equals;
                    break;
                case "greater":
                    condition.comparison = ComparisonType.Greater;
                    break;
                case "less":
                    condition.comparison = ComparisonType.Less;
                    break;
                case "notequals":
                    condition.comparison = ComparisonType.NotEquals;
                    break;
                default:
                    condition.comparison = ComparisonType.Equals;
                    break;
            }

            return condition;
        }
    }
}