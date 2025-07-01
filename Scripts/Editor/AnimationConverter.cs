using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System;


namespace HarmonyAnimatorToolkit
{
    public class AnimationConverter : EditorWindow
    {
        private Vector2 scrollPosition;
        private readonly float spacing = 5f;
        private GUIStyle headerStyle;
        private GUIStyle boxStyle;
        private bool showTransitions = true;

        public GameObject targetObject;
        public string animationFolderPath = "Assets/";
        public List<AnimationTransition> transitions = new List<AnimationTransition>();
        public AnimatorConverterSO transitionData;
        public MonoBehaviour customFinderScript;

        public AnimatorController animator;
        public bool transitionsGenerated;

        [MenuItem("Tools/Harmony Anim Toolkit/Animation Controller Generator")]
        public static void ShowWindow()
        {
            GetWindow<AnimationConverter>("Animator Generator");
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
            DrawSetupSection();
            DrawAnimatorSection();
            DrawTransitionsSection();
            DrawGenerateButtons();

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        private void DrawMainHeader()
        {
            GUILayout.Label("Animation Controller Generator", headerStyle);
            EditorGUILayout.HelpBox("Generate an Animator Easily.", MessageType.Info);
        }

        private void DrawSetupSection()
        {
            EditorGUILayout.BeginVertical(boxStyle);

            targetObject = (GameObject)EditorGUILayout.ObjectField(
                new GUIContent("Target Object", "Select the GameObject that the animator will be on."),
                targetObject,
                typeof(GameObject),
                true
            );

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

            transitionData = (AnimatorConverterSO)EditorGUILayout.ObjectField(
                new GUIContent("Transition Data", "Assign a ScriptableObject containing animation transition data."),
                transitionData,
                typeof(AnimatorConverterSO),
                false
            );
            if (transitionData != null && transitionData.transitions != null)
            {
                transitions = new List<AnimationTransition>(transitionData.transitions);
            }

            customFinderScript = (MonoBehaviour)EditorGUILayout.ObjectField(
                new GUIContent("Custom Finder Script", "Assign a MonoBehaviour script that helps locate animation assets. Uses default if null"),
                customFinderScript,
                typeof(MonoBehaviour),
                false
            );

            EditorGUILayout.EndVertical();
        }

        private void DrawAnimatorSection()
        {
            EditorGUILayout.BeginVertical(boxStyle);

            EditorGUILayout.BeginHorizontal();
            // Animator selection with tooltip
            animator = EditorGUILayout.ObjectField(
                new GUIContent("Animator Controller",
                    "An existing Animator Controller asset to use"),
                animator, typeof(AnimatorController), false) as AnimatorController;
            EditorGUILayout.EndHorizontal();

            // Check if animator is assigned
            if (animator != null)
            {
                if (transitionsGenerated)
                {
                    // Show info message if transitions have already been generated
                    EditorGUILayout.HelpBox("Transitions have been generated for this animator.", MessageType.Info);
                }
                else
                {
                    // Show button if transitions haven't been generated yet
                    if (GUILayout.Button(new GUIContent("Generate Transitions for Animator",
                        "Generate and add transitions to the selected animator controller")))
                    {
                        GenerateTransitions();

                        // Set the flag to true after generating transitions
                        transitionsGenerated = true;
                    }
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void GenerateTransitions()
        {
            Debug.Log("Generating transitions for animator: " + animator.name);

            List<AnimationTransition> existingTransitions = new List<AnimationTransition>();
            if (animator)
            {
                existingTransitions = TransitionJSONImporter.GetExistingAnimations(animator);
            }

            if (existingTransitions != null && existingTransitions.Count > 0) transitions.AddRange(existingTransitions);
        }

        private void DrawTransitionsSection()
        {
            EditorGUILayout.Space(spacing);

            showTransitions = EditorGUILayout.Foldout(showTransitions, "Transitions", true);
            if (!showTransitions) return;

            if (GUILayout.Button("Add Transition", GUILayout.Height(30)))
            {
                transitions.Add(new AnimationTransition());
            }

            for (int i = 0; i < transitions.Count; i++)
            {
                DrawTransition(transitions[i], i);
            }
        }

        private void DrawTransition(AnimationTransition transition, int index)
        {
            EditorGUILayout.BeginVertical(boxStyle);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"Transition {index + 1}", headerStyle);
            if (GUILayout.Button("Remove", GUILayout.Width(70)))
            {
                transitions.RemoveAt(index);
                GUIUtility.ExitGUI();
                return;
            }
            EditorGUILayout.EndHorizontal();

            DrawStateSection("Start State", ref transition.startType, ref transition.startAnimation,
                ref transition.startAnimationName, ref transition.startBlendTree);

            DrawStateSection("End State", ref transition.endType, ref transition.endAnimation,
                ref transition.endAnimationName, ref transition.endBlendTree);

            DrawConditions(transition);

            EditorGUILayout.EndVertical();
        }

        private void DrawStateSection(string label, ref StateType stateType, ref AnimationClip animation,
            ref string animationName, ref BlendTreeData blendTree)
        {
            EditorGUILayout.BeginVertical(boxStyle);

            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            stateType = (StateType)EditorGUILayout.EnumPopup("State Type", stateType);

            if (stateType == StateType.Animation)
            {
                animation = (AnimationClip)EditorGUILayout.ObjectField("Animation", animation, typeof(AnimationClip), false);
                animationName = EditorGUILayout.TextField("Animation Name", animationName);
            }
            else
            {
                DrawBlendTree(ref blendTree);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawBlendTree(ref BlendTreeData blendTree)
        {
            if (blendTree == null) blendTree = new BlendTreeData();

            blendTree.blendType = (BlendType)EditorGUILayout.EnumPopup("Blend Type", blendTree.blendType);

            if (blendTree.blendType == BlendType.OneD)
            {
                blendTree.parameterName = EditorGUILayout.TextField("Parameter Name", blendTree.parameterName);
            }
            else
            {
                EditorGUILayout.BeginHorizontal();

                blendTree.parameterNames[0] = EditorGUILayout.TextField("X Parameter", blendTree.parameterNames[0]);
                blendTree.parameterNames[1] = EditorGUILayout.TextField("Y Parameter", blendTree.parameterNames[1]);

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(spacing);
            EditorGUILayout.LabelField("Motions", EditorStyles.boldLabel);

            for (int i = 0; i < blendTree.motions.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                blendTree.motions[i].animation = (AnimationClip)EditorGUILayout.ObjectField(
                    "Animation", blendTree.motions[i].animation, typeof(AnimationClip), false);

                if (blendTree.blendType == BlendType.OneD)
                {
                    blendTree.motions[i].threshold = EditorGUILayout.FloatField(
                        "Threshold", blendTree.motions[i].threshold);
                }
                else
                {
                    blendTree.motions[i].threshold2D = EditorGUILayout.Vector2Field(
                        "Threshold", blendTree.motions[i].threshold2D);
                }

                if (GUILayout.Button("X", GUILayout.Width(20)))
                {
                    blendTree.motions.RemoveAt(i);
                    GUIUtility.ExitGUI();
                    return;
                }
                EditorGUILayout.EndHorizontal();
            }

            //if (GUILayout.Button("Add Motion"))
            //{
            //    blendTree.motions.Add(new BlendTreeMotion());
            //}
        }

        private void DrawConditions(AnimationTransition transition)
        {
            EditorGUILayout.Space(spacing);
            EditorGUILayout.LabelField("Conditions", EditorStyles.boldLabel);

            for (int i = 0; i < transition.conditions.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                var condition = transition.conditions[i];

                condition.name = EditorGUILayout.TextField("Name", condition.name);
                condition.type = (ConditionType)EditorGUILayout.EnumPopup("Type", condition.type);

                switch (condition.type)
                {
                    case ConditionType.Bool:
                        condition.boolValue = EditorGUILayout.Toggle("Value", condition.boolValue);
                        break;
                    case ConditionType.Float:
                    case ConditionType.Int:
                        condition.comparison = (ComparisonType)EditorGUILayout.EnumPopup("Comparison", condition.comparison);
                        condition.numberValue = EditorGUILayout.FloatField("Value", condition.numberValue);
                        break;
                }

                if (GUILayout.Button("X", GUILayout.Width(20)))
                {
                    transition.conditions.RemoveAt(i);
                    GUIUtility.ExitGUI();
                    return;
                }

                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("Add Condition"))
            {
                transition.conditions.Add(new TransitionCondition());
            }
        }

        private void DrawGenerateButtons()
        {
            EditorGUILayout.Space(10);
            if (GUILayout.Button("Generate Transition SO"))
            {
                GenerateTransitionSO();
            }

            EditorGUILayout.Space(5);
            if (GUILayout.Button("Generate Animator"))
            {
                GenerateAnimator();
            }
        }

        private void GenerateTransitionSO()
        {
            AnimatorConverterSO asset = ScriptableObject.CreateInstance<AnimatorConverterSO>();
            asset.transitions = new List<AnimationTransition>(transitions);

            string path = "Assets/GeneratedTransitionData.asset";
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();

            EditorUtility.FocusProjectWindow();
            Selection.activeObject = asset;
        }

        private void GenerateAnimator()
        {
            if (targetObject == null)
            {
                Debug.LogError("Please assign a target object.");
                return;
            }

            AnimatorController controller;

            string path = EditorUtility.SaveFilePanelInProject(
                "Save Animator Controller",
                "GeneratedAnimator",
                "controller",
                "Please select a save location for the Animator Controller"
            );

            if (!string.IsNullOrEmpty(path))
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(path);
                Debug.Log("Animator Controller saved at: " + path);
            }
            else
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath("Assets/GeneratedAnimator.controller");
                Debug.LogWarning("Animator Controller save was cancelled.");
            }

            var rootStateMachine = controller.layers[0].stateMachine;
            Dictionary<string, AnimatorState> stateDict = new();

            foreach (var transition in transitions)
            {
                AnimatorState fromState = CreateState(transition.startType, transition.startAnimation,
                    transition.startAnimationName, transition.startBlendTree, rootStateMachine, controller, stateDict);

                AnimatorState toState = CreateState(transition.endType, transition.endAnimation,
                    transition.endAnimationName, transition.endBlendTree, rootStateMachine, controller, stateDict);

                if (fromState != null && toState != null)
                {
                    CreateTransition(fromState, toState, transition.conditions, controller);
                }
            }


            if (targetObject.TryGetComponent(out Animator animator))
            {
                animator.runtimeAnimatorController = controller;
            }
            else
            {
                targetObject.AddComponent<Animator>().runtimeAnimatorController = controller;
            }
        }

        private AnimatorState CreateState(StateType stateType, AnimationClip clip, string stateName,
            BlendTreeData blendTreeData, AnimatorStateMachine stateMachine,
            AnimatorController controller, Dictionary<string, AnimatorState> stateDict)
        {
            if (stateDict.ContainsKey(stateName))
                return stateDict[stateName];

            AnimatorState state = stateMachine.AddState(stateName);

            if (stateType == StateType.Animation)
            {
                state.motion = clip != null ? clip : FindAnimationByName(stateName, animationFolderPath);
            }
            else if (stateType == StateType.BlendTree && blendTreeData != null)
            {
                // Create a unique blend tree name to avoid conflicts
                string blendTreeName = $"{stateName}_BlendTree";
                controller.CreateBlendTreeInController(blendTreeName, out BlendTree blendTree);

                // Set blend type based on BlendTreeData
                if (blendTreeData.blendType == BlendType.OneD)
                {
                    blendTree.blendType = BlendTreeType.Simple1D;
                    blendTree.blendParameter = blendTreeData.parameterName;

                    // Add parameter if it doesn’t exist
                    if (!controller.parameters.Any(p => p.name == blendTreeData.parameterName))
                    {
                        controller.AddParameter(blendTreeData.parameterName, AnimatorControllerParameterType.Float);
                    }

                    // Add motions with 1D thresholds
                    foreach (var motion in blendTreeData.motions)
                    {
                        if (motion.animation != null)
                        {
                            blendTree.AddChild(motion.animation, motion.threshold);
                        }
                        else
                        {
                            Debug.LogWarning($"Motion animation is null for state '{stateName}' in 1D blend tree.");
                        }
                    }
                }
                else if (blendTreeData.blendType == BlendType.TwoD)
                {
                    blendTree.blendType = BlendTreeType.SimpleDirectional2D;
                    blendTree.blendParameter = blendTreeData.parameterNames[0] ?? "Default_X"; // X component
                    blendTree.blendParameterY = blendTreeData.parameterNames[1] ?? "Default_Y"; // Y component

                    // Add X and Y parameters if they don’t exist
                    if (!controller.parameters.Any(p => p.name == blendTree.blendParameter))
                    {
                        controller.AddParameter(blendTree.blendParameter, AnimatorControllerParameterType.Float);
                    }
                    if (!controller.parameters.Any(p => p.name == blendTree.blendParameterY))
                    {
                        controller.AddParameter(blendTree.blendParameterY, AnimatorControllerParameterType.Float);
                    }

                    // Add motions with 2D thresholds
                    foreach (var motion in blendTreeData.motions)
                    {
                        if (motion.animation != null)
                        {
                            blendTree.AddChild(motion.animation, motion.threshold2D);
                        }
                        else
                        {
                            Debug.LogWarning($"Motion animation is null for state '{stateName}' in 2D blend tree.");
                        }
                    }
                }
                else
                {
                    Debug.LogError($"Unsupported blend type '{blendTreeData.blendType}' for state '{stateName}'.");
                }

                state.motion = blendTree;
            }

            stateDict[stateName] = state;
            return state;
        }

        private void CreateTransition(AnimatorState fromState, AnimatorState toState,
            List<TransitionCondition> conditions, AnimatorController controller)
        {
            var transition = fromState.AddTransition(toState);
            transition.hasExitTime = false;

            foreach (var condition in conditions)
            {
                if (!controller.parameters.Any(p => p.name == condition.name))
                {
                    controller.AddParameter(condition.name, GetParameterType(condition.type));
                }

                if (condition.type == ConditionType.Bool)
                {
                    transition.AddCondition(
                        AnimatorConditionMode.If,
                        condition.boolValue ? 1 : 0,
                        condition.name
                    );
                }
                else if (condition.type == ConditionType.Float || condition.type == ConditionType.Int)
                {

                    transition.AddCondition(
                        GetComparisonMode(condition.comparison, condition.type),
                        condition.numberValue,
                        condition.name
                    );
                }
            }
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

            // Custom Finder Script Check
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
                    if (clip != null && !clip.name.Contains("__preview__"))
                    {
                        if (clip.name.Equals(animationName, StringComparison.OrdinalIgnoreCase))
                        {
                            //Debug.Log($"FindAnimationByName: Found animation '{animationName}' inside model file {modelPath}");
                            return clip;
                        }
                    }
                }
            }

            Debug.LogWarning($"FindAnimationByName: Animation clip '{animationName}' not found in folder: {animationFolderPath}");
            return null;
        }

        private AnimatorControllerParameterType GetParameterType(ConditionType type)
        {
            return type switch
            {
                ConditionType.Bool => AnimatorControllerParameterType.Bool,
                ConditionType.Float => AnimatorControllerParameterType.Float,
                ConditionType.Int => AnimatorControllerParameterType.Int,
                ConditionType.Trigger => AnimatorControllerParameterType.Trigger,
                _ => AnimatorControllerParameterType.Float,
            };
        }

        private AnimatorConditionMode GetComparisonMode(ComparisonType comparison, ConditionType condType)
        {
            if (condType == ConditionType.Float)
            {
                return comparison switch
                {
                    ComparisonType.Greater => AnimatorConditionMode.Greater,
                    ComparisonType.Less => AnimatorConditionMode.Less,
                    _ => AnimatorConditionMode.Greater,
                };
            }
            else
            {
                return comparison switch
                {
                    ComparisonType.Equals => AnimatorConditionMode.Equals,
                    ComparisonType.NotEquals => AnimatorConditionMode.NotEqual,
                    ComparisonType.Greater => AnimatorConditionMode.Greater,
                    ComparisonType.Less => AnimatorConditionMode.Less,
                    _ => AnimatorConditionMode.Equals,
                };
            }

        }
    }
}



