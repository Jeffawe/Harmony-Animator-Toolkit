using System;
using System.Collections.Generic;
using UnityEngine;

namespace HarmonyAnimatorConverter
{
    public class AnimatorJsonValidator
    {
        public static string ConvertKeysToLower(string json)
        {
            // Find all the keys and convert them to lowercase
            var regex = new System.Text.RegularExpressions.Regex("\"[a-zA-Z0-9_]+\":");
            json = regex.Replace(json, match => match.Value.ToLower());
            return json;
        }

        public static bool ValidateJson(string jsonText, out List<string> errors)
        {
            errors = new List<string>();

            try
            {
                AnimatorJsonData jsonData = JsonUtility.FromJson<AnimatorJsonData>(jsonText.ToLower());
                if (jsonData == null)
                {
                    errors.Add("Invalid JSON structure. Could not parse.");
                    return false;
                }

                // Validate Transitions
                foreach (JsonTransition transition in jsonData.transitions)
                {
                    ValidateState(transition.startstate, errors, "StartState");
                    ValidateState(transition.endstate, errors, "EndState");

                    foreach (JsonCondition condition in transition.conditions)
                    {
                        ValidateCondition(condition, errors);
                    }
                }

                return errors.Count == 0;
            }
            catch (Exception ex)
            {
                errors.Add($"JSON parsing error: {ex.Message}");
                return false;
            }
        }

        private static void ValidateState(JsonState state, List<string> errors, string stateName)
        {
            if (state == null)
            {
                errors.Add($"{stateName} is missing.");
                return;
            }

            if (state.type != "animation" && state.type != "blendtree")
            {
                errors.Add($"{stateName} has invalid type: {state.type}. Must be 'Animation' or 'BlendTree'.");
            }

            if (state.type == "animation" && string.IsNullOrEmpty(state.animationname))
            {
                errors.Add($"{stateName} is 'Animation' but missing animationName.");
            }

            if (state.type == "blendtree" && state.blendtree == null)
            {
                errors.Add($"{stateName} is 'BlendTree' but blendTree data is missing.");
            }
            else if (state.type == "blendtree")
            {
                ValidateBlendTree(state.blendtree, errors, $"{stateName} BlendTree");
            }
        }

        private static void ValidateBlendTree(JsonBlendTreeData blendTree, List<string> errors, string blendTreeName)
        {
            if (blendTree == null) return;

            if (blendTree.blendtype != "oned" && blendTree.blendtype != "twod")
            {
                errors.Add($"{blendTreeName} has invalid blendType: {blendTree.blendtype}. Must be 'OneD' or 'TwoD'.");
            }

            if(blendTree.blendtype == "oned")
            {
                if (string.IsNullOrEmpty(blendTree.parametername))
                {
                    errors.Add($"{blendTreeName} is missing parameterName.");
                }
            }
            else
            {
                if(blendTree.parameternames.Length <= 0)
                {
                    errors.Add($"Parameter Names are missing. It's 2D");
                }
            }

            foreach (JsonBlendTreeMotion motion in blendTree.motions)
            {
                if (string.IsNullOrEmpty(motion.animationname))
                {
                    errors.Add($"{blendTreeName} motion is missing animationName.");
                }

                if (blendTree.blendtype == "twod" && (motion.threshold2d == null || motion.threshold2d.Length != 2))
                {
                    errors.Add($"{blendTreeName} motion is TwoD but has invalid threshold2D (must have exactly 2 values).");
                }
            }
        }

        private static void ValidateCondition(JsonCondition condition, List<string> errors)
        {
            if (string.IsNullOrEmpty(condition.name))
            {
                errors.Add("Condition is missing name.");
            }

            if (condition.type != "bool" && condition.type != "float" && condition.type != "int" && condition.type != "trigger")
            {
                errors.Add($"Condition '{condition.name}' has invalid type: {condition.type}. Must be 'Bool', 'Float', 'Int', or 'Trigger'.");
            }

            if (condition.comparison != "equals" && condition.comparison != "greater" && condition.comparison != "less")
            {
                errors.Add($"Condition '{condition.name}' has invalid comparison: {condition.comparison}. Must be 'Equals', 'Greater', or 'Less'.");
            }
        }
    }
}
