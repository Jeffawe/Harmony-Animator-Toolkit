using System;
using System.Collections.Generic;
using UnityEngine;

namespace HarmonyAnimatorConverter
{
    [CreateAssetMenu(fileName = "Transition Data", menuName = "HAS/Transition Data")]
    public class AnimatorConverterSO : ScriptableObject
    {
        public List<AnimationTransition> transitions = new List<AnimationTransition>();
    }


    [System.Serializable]
    public class AnimationTransition
    {
        public StateType startType, endType;
        public string startAnimationName, endAnimationName;
        public AnimationClip startAnimation, endAnimation;
        public BlendTreeData startBlendTree, endBlendTree;
        public List<TransitionCondition> conditions = new List<TransitionCondition>();
    }


    [System.Serializable]
    public class BlendTreeData
    {
        public string parameterName;
        public string[] parameterNames;
        public BlendType blendType;
        public List<BlendTreeMotion> motions = new();
    }

    [System.Serializable]
    public class BlendTreeMotion
    {
        public AnimationClip animation;
        public float threshold;
        public Vector2 threshold2D;
    }

    [System.Serializable]
    public class TransitionCondition
    {
        public string name;
        public ConditionType type;
        public bool boolValue;
        public float numberValue;
        public ComparisonType comparison;
    }

    #region JSON Classes
    [Serializable]
    public class AnimatorJsonData
    {
        public List<JsonTransition> transitions = new List<JsonTransition>();
    }

    [Serializable]
    public class JsonTransition
    {
        public JsonState startstate;
        public JsonState endstate;
        public List<JsonCondition> conditions = new List<JsonCondition>();
    }

    [Serializable]
    public class JsonState
    {
        public string type = "animation"; // "Animation" or "BlendTree"
        public string animationname;
        public JsonBlendTreeData blendtree;
    }

    [Serializable]
    public class JsonBlendTreeData
    {
        public string parametername;
        public string[] parameternames;
        public string blendtype = "OneD"; // "OneD" or "TwoD"
        public List<JsonBlendTreeMotion> motions = new List<JsonBlendTreeMotion>();
    }

    [Serializable]
    public class JsonBlendTreeMotion
    {
        public string animationname;
        public float threshold; // Used for "OneD"
        public float[] threshold2d; //Used for TwoD
    }

    [Serializable]
    public class JsonCondition
    {
        public string name;
        public string type = "bool"; // "Bool", "Float", "Int", "Trigger"
        public bool boolvalue;
        public float numbervalue;
        public string comparison = "equals"; // "Equals", "Greater", "Less"
    }
    #endregion

    public enum StateType { Animation, BlendTree }
    public enum BlendType { OneD, TwoD }
    public enum ConditionType { Bool, Float, Int, Trigger }
    public enum ComparisonType { Equals, Greater, Less, NotEquals }

    public interface IAnimationFinder
    {
        AnimationClip FindAnimation(string name, string path);
    }

    public interface IJSONConverter
    {
        List<AnimationTransition> ConvertJSONToAnimationTransitions(string jsonText);

        AnimatorConverterSO ConvertJsonToSO(string jsonText);
    }
}
