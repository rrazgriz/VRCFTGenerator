#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using AnimatorAsCode.V0;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using UnityEditor;
using UnityEditor.Animations;

namespace AnimatorAsCodeFramework.Razgriz.VRCFTGenerator
{

    [Serializable]
    public class ParamSpecifier
    {
        public VRCFTValues.ParamMode mode;
        public VRCFTValues.ParameterName VRCFTParameter;
    }

    public class VRCFTGenerator : MonoBehaviour
    {
        public VRCAvatarDescriptor avatar;
        public AnimatorController assetContainer;
        public string assetKey;
        public bool writeDefaults = false;
        public bool removeAnimatorParams = true;
        public bool manageExpressionParameters = true;

        public SkinnedMeshRenderer[] blendshapeTargetMeshRenderers = new SkinnedMeshRenderer[1];
        public float remoteSmoothingTimeConstant = 0.7f;
        [Header("Use VRCFT name of params in synced params - ex. Face/Combined/General/TongueX => TongueX")]
        [Space(25)]
        public ParamSpecifier[] paramsToAnimate;
    }

    [CustomEditor(typeof(VRCFTGenerator), true)]
    public class VRCFTGeneratorEditor : Editor
    {
        public static void DrawUILine(Color color, int thickness = 2, int padding = 8)
        {
            Rect r = EditorGUILayout.GetControlRect(GUILayout.Height(padding+thickness));
            r.height = thickness;
            r.y+=padding/2;
            r.x-=2;
            EditorGUI.DrawRect(r, color);
        }

        private const string SystemName = "FT";
        private VRCFTGenerator my;
        private AacFlBase aac;

        private void InitializeAAC()
        {
            my = (VRCFTGenerator) target;
            var wd = my.writeDefaults ? AACTemplate.Options().WriteDefaultsOn() : AACTemplate.Options().WriteDefaultsOff();
            aac = AACTemplate.AnimatorAsCode(SystemName, my.avatar, my.assetContainer, my.assetKey, wd);
        }

        // // // // // // // // // // // // // // // // // // // // // // // // // // // 

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            // Unity GUI Magic?
            var prop = serializedObject.FindProperty("assetKey");
            if (prop.stringValue.Trim() == "")
            {
                prop.stringValue = GUID.Generate().ToString();
                serializedObject.ApplyModifiedProperties();
            }

            this.DrawDefaultInspector();

            Color darkgray = new Color(0.184f, 0.184f, 0.184f, 1.0f);
            DrawUILine(darkgray);

            // Add More Buttons Here :)

            DrawUILine(darkgray);
            if (GUILayout.Button("Add Selected Features")){ AddFeatures(); }
            if (GUILayout.Button("Remove FT System")){ RemoveFeatures(); }
            serializedObject.ApplyModifiedProperties();
        }

        // // //

        private void AddFeatures()
        {
            InitializeAAC();
            int cost = ValidateFTSetup();
            if(cost > 0)
            {
                bool add = EditorUtility.DisplayDialog("Add Face Tracking","Generate Face Tracking, Parameter Cost: " + cost.ToString(), "OK", "Cancel");
                if(add)
                {
                    CreateFTBlendshapeController();
                    AssetDatabase.SaveAssets();
                }
            }
            
        }

        private void RemoveFeatures()
        {
            InitializeAAC();
            bool remove = EditorUtility.DisplayDialog("Remove Face Tracking", "Remove Face Tracking from Animator (and items selected for deletion)?", "OK", "Cancel");
            if (remove)
            {
                RemoveFTBlendshapeController();
                AssetDatabase.SaveAssets();
            }
        }

        private void RemoveFTBlendshapeController()
        {
            string systemPrefix = SystemName+"__";
            // Find FX Layer
            AnimatorController fxLayer = (AnimatorController) my.avatar.baseAnimationLayers.First(it => it.type == VRCAvatarDescriptor.AnimLayerType.FX).animatorController;
            aac.RemoveAllMainLayers();

            foreach(var layer in fxLayer.layers)
            {
                string layerName = layer.name;
                string supportingLayerName = layerName.Replace(systemPrefix, "");

                if(layerName.StartsWith(systemPrefix))
                {
                    aac.RemoveAllSupportingLayers(supportingLayerName);
                }
            }

            List<string> parameterNames = VRCFTValues.CombinedMapping.Keys.ToList();

            parameterNames  = parameterNames.Concat(VRCFTValues.BlendshapeMapping.Keys.ToList())
                                            .Concat(VRCFTValues.AveragedMapping.Keys.ToList())
                                            .ToList();

            AnimatorControllerParameter[] fxParameters = fxLayer.parameters;

            if (my.manageExpressionParameters)
            {
                VRCExpressionParameters.Parameter[] expressionParameters = my.avatar.expressionParameters.parameters;

                foreach (VRCExpressionParameters.Parameter expressionParameter in expressionParameters)
                {
                    if (expressionParameter.name.Contains(systemPrefix) || expressionParameter.name == "FaceTracking")
                    {
                        VRCFTGenerator_ParameterUtility.RemoveParameter(expressionParameter.name, my.avatar);
                    }

                    foreach (string p in parameterNames)
                    {
                        if (expressionParameter.name.Contains(p))
                        {
                            VRCFTGenerator_ParameterUtility.RemoveParameter(expressionParameter.name, my.avatar);
                        }
                    }
                }
            }

            if(my.removeAnimatorParams)
            {
                int i = 0;

                foreach (AnimatorControllerParameter animatorParameter in fxParameters)
                {
                    if (animatorParameter.name.Contains(systemPrefix) || animatorParameter.name == "FaceTracking")
                    {
                        fxLayer.RemoveParameter(fxParameters[i]);
                    } else {
                        foreach (string name in parameterNames)
                        {
                            if (animatorParameter.name.Contains(name))
                            {
                                fxLayer.RemoveParameter(fxParameters[i]);
                            }
                        }
                    }

                    i++;
                }
            }
        }

        private int ValidateFTSetup()
        {
            int cost = 1;
            Dictionary<string, VRCFTValues.ParamMode> SelectedParameters = new Dictionary<string, VRCFTValues.ParamMode>();
            Dictionary<string, string> SelectedShapes = new Dictionary<string, string>();
            Dictionary<string, List<string>> BadParameters = new Dictionary<string, List<string>>();
            List<string> DuplicateParameters = new List<string>();

            bool isValid = true;

            foreach (ParamSpecifier specification in my.paramsToAnimate)
            {
                if(!SelectedParameters.ContainsKey(specification.VRCFTParameter.ToString()))
                {
                    SelectedParameters.Add(Enum.GetName(typeof(VRCFTValues.ParameterName), specification.VRCFTParameter), specification.mode);
                    cost += (int) specification.mode;
                    cost += VRCFTValues.CombinedMapping.ContainsKey(Enum.GetName(typeof(VRCFTValues.ParameterName), specification.VRCFTParameter)) && specification.mode != VRCFTValues.ParamMode.floatParam ? 1 : 0;
                } else {
                    DuplicateParameters.Add(specification.VRCFTParameter.ToString());
                    Debug.LogWarning("VRCFTGenerator: Parameter " + Enum.GetName(typeof(VRCFTValues.ParameterName), specification.VRCFTParameter) + " Is Present Multiple Times");
                    isValid = false;
                }
            }

            foreach (string param in SelectedParameters.Keys.ToArray())
            {
                List<string> shapesToAdd = new List<string>();

                string param0 = "";

                if (VRCFTValues.CombinedMapping.ContainsKey(param))
                {
                    param0 = VRCFTValues.CombinedMapping[param][0];
                    string param1 = VRCFTValues.CombinedMapping[param][1];

                    if (VRCFTValues.AveragedMapping.ContainsKey(param1))
                    {
                        shapesToAdd.Add(VRCFTValues.AveragedMapping[param1][0]);
                        shapesToAdd.Add(VRCFTValues.AveragedMapping[param1][1]);
                    } else {
                        shapesToAdd.Add(param1);
                    }
                } else {
                    param0 = param;
                }

                if (VRCFTValues.AveragedMapping.ContainsKey(param0))
                {
                    shapesToAdd.Add(VRCFTValues.AveragedMapping[param0][0]);
                    shapesToAdd.Add(VRCFTValues.AveragedMapping[param0][1]);
                } else {
                    shapesToAdd.Add(param0);
                }


                foreach (string shape in shapesToAdd)
                {
                    if (!SelectedShapes.Keys.Contains(shape))
                    {
                        SelectedShapes.Add(shape, param);
                    } else {
                        isValid = false;
                        if (!BadParameters.Keys.Contains(shape))
                        {
                            BadParameters.Add(shape, new List<string> { SelectedShapes[shape] });
                        }

                        BadParameters[shape].Add(param);
                        
                        Debug.LogWarning("VRCFTGenerator: Parameter " + param + " adds the shape " + shape + ", but it's already been added by " + SelectedShapes[shape]);
                    }
                }
            }

            if (!isValid)
            {
                string duplicateShapeList = Environment.NewLine;
                if (BadParameters.Keys.Count > 0)
                {
                    duplicateShapeList = "The following shapes are specified by multiple parameters: " + Environment.NewLine;
                    foreach (string shape in BadParameters.Keys)
                    {
                        duplicateShapeList += shape + ", added by " + Environment.NewLine;
                        foreach (string param in BadParameters[shape])
                        {
                            duplicateShapeList += "   - " + param + Environment.NewLine;
                        }
                        duplicateShapeList += Environment.NewLine;
                    }
                }

                if (DuplicateParameters.Count > 0)
                {
                    duplicateShapeList += Environment.NewLine + "The following Parameters are specified multiple times:" + Environment.NewLine;

                    foreach (string param in DuplicateParameters)
                    {
                        duplicateShapeList += param + Environment.NewLine;
                    }
                }


                EditorUtility.DisplayDialog("VRCFTGenerator: Duplicate Shapes or Parameters", duplicateShapeList, "OK");
            }

            if (!isValid)
            {
                cost = 0;
            }

            return cost;
        }

        private void CreateFTBlendshapeController()
        {
            var fx = aac.CreateMainFxLayer();

            Dictionary<string, VRCFTValues.ParamMode> SelectedParameters = new Dictionary<string, VRCFTValues.ParamMode>();
            List<string> selectedBinaryDecodeParams = new List<string>();
            List<string> selectedFloatDecodeParams = new List<string>();
            List<string> decodeParams = new List<string>();
            List<string> smoothingParams = new List<string>();

            int parameterMemoryCost = 1; // 1 for Face Tracking toggle

            foreach (ParamSpecifier specification in my.paramsToAnimate)
            {
                SelectedParameters.Add(Enum.GetName(typeof(VRCFTValues.ParameterName), specification.VRCFTParameter), specification.mode);
            }

            // Pre-Parse Selected Parameters
            foreach (string param in SelectedParameters.Keys.ToArray())
            {
                VRCFTValues.ParamMode mode = SelectedParameters[param];
                bool isParamFloat = mode == VRCFTValues.ParamMode.floatParam;
                bool isParamCombined = VRCFTValues.CombinedMapping.ContainsKey(param);

                parameterMemoryCost += (int)SelectedParameters[param];

                if(isParamFloat)
                {
                    fx.FloatParameter(param);
                    if(isParamCombined)
                    {
                        selectedFloatDecodeParams.Add(param);
                    }
                } else {
                    selectedBinaryDecodeParams.Add(param);
                    // Extra bit for negative flag bool
                    parameterMemoryCost += isParamCombined ? 1 : 0;
                }

                if (isParamCombined)
                {
                    smoothingParams.Add(VRCFTValues.CombinedMapping[param][0]);
                    smoothingParams.Add(VRCFTValues.CombinedMapping[param][1]);
                } else {
                    smoothingParams.Add(param);
                }
            }

            



            decodeParams = selectedFloatDecodeParams.Concat(selectedBinaryDecodeParams).ToList();

            // Create Binary Cast/Decode Layers (only if needed)
            int decodeBlendtreeChildren = decodeParams.Count;
            if (decodeBlendtreeChildren > 0)
            {
                var binaryCastLayer = aac.CreateSupportingFxLayer("BinaryCast");
                var binaryCastState = binaryCastLayer.NewState("Cast");
                binaryCastState.TransitionsTo(binaryCastState).AfterAnimationIsAtLeastAtPercent(0f).WithTransitionToSelf(); // re-evaluate every frame

                var decodeLayer = aac.CreateSupportingFxLayer("BinaryCombinedDecode");
                var binarySumState = decodeLayer.NewState("DecodeBlendTree");
                var binarySumTopLevelNormalizerParam = decodeLayer.FloatParameter(SystemName + "__" + "DecodeBlendTreeNormalizer");
                decodeLayer.OverrideValue(binarySumTopLevelNormalizerParam, 1f/(float)decodeBlendtreeChildren);

                var parameterDirectTreeNormalizer = decodeLayer.FloatParameter(SystemName + "__" + "Constant_1");
                fx.OverrideValue(parameterDirectTreeNormalizer, 1f);
                binaryCastState.Drives(parameterDirectTreeNormalizer, 1f);

                List<ChildMotion> binarySumTopLevelChildMotions = new List<ChildMotion>();

                foreach (string param in selectedBinaryDecodeParams)
                {
                    int paramBits = (int)SelectedParameters[param];
                    bool isCombinedParam = VRCFTValues.CombinedMapping.ContainsKey(param);
                    string paramPositiveName = isCombinedParam ? VRCFTValues.CombinedMapping[param][0] : param;
                    string paramNegativeName = isCombinedParam ? VRCFTValues.CombinedMapping[param][1] : param;

                    var paramPositive = binaryCastLayer.FloatParameter(paramPositiveName);
                    var paramNegative = binaryCastLayer.FloatParameter(paramPositiveName);

                    var zeroClip = aac.NewClip(param + "_0_scaled");
                    var oneClipParam1  = aac.NewClip(param + "_1_scaled");
                    var oneClipParam2 = aac.NewClip(param + "_2_scaled");

                    var boolParamNegative = binaryCastLayer.BoolParameter(param + "Negative");
                    var floatBoolParamNegative = binaryCastLayer.FloatParameter(param + "Negative_Float");

                    if(isCombinedParam) 
                    {
                        binaryCastState.DrivingCasts(boolParamNegative, 0f, 1f, floatBoolParamNegative, 0f, 1f);
                    }
                    
                    foreach(string keyframeParam in decodeParams)
                    {
                        var paramToAnimate = decodeLayer.FloatParameter(keyframeParam);

                        float oneClipVal = keyframeParam == param ? 1f*(float)decodeBlendtreeChildren : 0f;

                        bool keyframeParamIsCombined = VRCFTValues.CombinedMapping.ContainsKey(keyframeParam);
                        var keyframePositiveName = keyframeParamIsCombined ? VRCFTValues.CombinedMapping[keyframeParam][0] : keyframeParam;
                        var keyframeNegativeName = keyframeParamIsCombined ? VRCFTValues.CombinedMapping[keyframeParam][1] : keyframeParam;

                        var keyframePositive = binaryCastLayer.FloatParameter(keyframePositiveName);
                        var keyframeNegative = binaryCastLayer.FloatParameter(keyframeNegativeName);

                        zeroClip.Animating(clip => clip.AnimatesAnimator(keyframePositive).WithOneFrame(0f));
                        oneClipParam1.Animating(clip => clip.AnimatesAnimator(keyframePositive).WithOneFrame(oneClipVal));
                        oneClipParam2.Animating(clip => clip.AnimatesAnimator(keyframePositive).WithOneFrame(0f));

                        if(keyframeParamIsCombined)
                        {
                            zeroClip.Animating(clip => clip.AnimatesAnimator(keyframeNegative).WithOneFrame(0f));
                            oneClipParam1.Animating(clip => clip.AnimatesAnimator(keyframeNegative).WithOneFrame(0f));
                            oneClipParam2.Animating(clip => clip.AnimatesAnimator(keyframeNegative).WithOneFrame(oneClipVal));
                        }
                    }

                    BlendTree[] binarySumChildBlendTreesPositive = new BlendTree[paramBits];
                    BlendTree[] binarySumChildBlendTreesNegative = new BlendTree[paramBits];
                    AacFlFloatParameter[] binarySumChildBlendTreesBlendParams = new AacFlFloatParameter[paramBits];

                    for (int j = 0; j < paramBits; j++)
                    {
                        int binarySuffix = (int)Mathf.Pow(2, j);
                        string p = param+binarySuffix.ToString();

                        var boolParam = binaryCastLayer.BoolParameter(p);
                        var floatBoolParam = binaryCastLayer.FloatParameter(p + "_Float");

                        binaryCastState.DrivingCasts(boolParam, 0f, 1f, floatBoolParam, 0f, 0.5f * Mathf.Pow(2,j+1) * 1f/(Mathf.Pow(2,(float)paramBits) - 1f));

                        var positiveTree = CreateProxyTree(floatBoolParam, zeroClip, oneClipParam1);
                        var negativeTree = CreateProxyTree(floatBoolParam, zeroClip, oneClipParam2);

                        binarySumChildBlendTreesPositive[j] = positiveTree;
                        binarySumChildBlendTreesNegative[j] = negativeTree;
                        binarySumChildBlendTreesBlendParams[j] = parameterDirectTreeNormalizer;
                    }
                    
                    BlendTree childMotion;
                    BlendTree parameterDirectBlendTreePositive = CreateDirectTree(decodeLayer, parameterDirectTreeNormalizer, binarySumChildBlendTreesBlendParams, binarySumChildBlendTreesPositive);
                    BlendTree parameterDirectBlendTreeNegative = CreateDirectTree(decodeLayer, parameterDirectTreeNormalizer, binarySumChildBlendTreesBlendParams, binarySumChildBlendTreesNegative);

                    if (isCombinedParam)
                    {
                        var combinedTree = CreateFactorTree(floatBoolParamNegative, parameterDirectBlendTreePositive, parameterDirectBlendTreeNegative);
                        childMotion = combinedTree;
                    } else {
                        childMotion = parameterDirectBlendTreePositive;
                    }

                    binarySumTopLevelChildMotions.Add(new ChildMotion {motion = childMotion, directBlendParameter = binarySumTopLevelNormalizerParam.Name, timeScale = 1.0f, threshold = 0.0f});
                }

                foreach (string param in selectedFloatDecodeParams)
                {
                    var zeroClip  = aac.NewClip(param + "_0_scaled");
                    var oneClip   = aac.NewClip(param + "_1_scaled");
                    var minusClip = aac.NewClip(param + "_-1_scaled");

                    foreach(string keyframeParam in decodeParams)
                    {
                        var paramToAnimate = decodeLayer.FloatParameter(keyframeParam);

                        float oneClipVal = keyframeParam == param ? 1f*(float)decodeBlendtreeChildren : 0f;

                        bool keyframeParamIsCombined = VRCFTValues.CombinedMapping.ContainsKey(keyframeParam);
                        var keyframePositiveName = keyframeParamIsCombined ? VRCFTValues.CombinedMapping[keyframeParam][0] : keyframeParam;
                        var keyframeNegativeName = keyframeParamIsCombined ? VRCFTValues.CombinedMapping[keyframeParam][1] : keyframeParam;

                        var keyframePositive = binaryCastLayer.FloatParameter(keyframePositiveName);
                        var keyframeNegative = binaryCastLayer.FloatParameter(keyframeNegativeName);

                        zeroClip.Animating(clip  => clip.AnimatesAnimator(keyframePositive).WithOneFrame(0f));
                        oneClip.Animating(clip   => clip.AnimatesAnimator(keyframePositive).WithOneFrame(oneClipVal));
                        minusClip.Animating(clip => clip.AnimatesAnimator(keyframeNegative).WithOneFrame(oneClipVal));
                    }

                    BlendTree childMotion = CreateCombinedTree(decodeLayer.FloatParameter(param), minusClip, zeroClip, oneClip);
                    binarySumTopLevelChildMotions.Add(new ChildMotion {motion = childMotion, directBlendParameter = binarySumTopLevelNormalizerParam.Name, timeScale = 1.0f, threshold = 0.0f});
                }

                var binarySumTopLevelDirectBlendTree = CreateDirectTree(decodeLayer, binarySumTopLevelNormalizerParam, binarySumTopLevelChildMotions.ToArray());
                binarySumState.WithAnimation(binarySumTopLevelDirectBlendTree);
            }
            

            // Smoothing/Driving Layer
            if(smoothingParams.Count > 0)
            {
                var smoothingLayer = aac.CreateSupportingFxLayer("SmoothingDriving"); // Adds or Overwrites

                var smoothingFactorParameter = fx.FloatParameter(SystemName + "__" + "SmoothingAlpha");
                fx.OverrideValue(smoothingFactorParameter, my.remoteSmoothingTimeConstant);

                var faceTrackingToggle = fx.BoolParameter("FaceTracking");
                fx.OverrideValue(faceTrackingToggle, true);

                int numDirectStates = smoothingParams.Count;

                // Need to normalize all direct blendtree weights to sum to 1
                var directNormalizer = fx.FloatParameter(SystemName + "__" + "SmoothingDrivingTreeNormalizer");
                fx.OverrideValue(directNormalizer, 1/(float)numDirectStates);

                List<ChildMotion> smoothingChildMotions = new List<ChildMotion>();

                foreach (string parameterToSmooth in smoothingParams)
                {
                    var parameter = fx.FloatParameter(parameterToSmooth);
                    var smoothedParameter = fx.FloatParameter(parameterToSmooth + "_Smoothed");

                    var zeroClip = aac.NewClip(parameterToSmooth + "param_0_scaled");
                    var oneClip  = aac.NewClip(parameterToSmooth + "param_1_scaled");

                    foreach(string paramName in smoothingParams)
                    {
                        var paramNameSmoothed = fx.FloatParameter(paramName + "_Smoothed");

                        float driveVal = paramName == parameterToSmooth ? 1f*(float)numDirectStates : 0f;
                        zeroClip.Animating(clip => clip.AnimatesAnimator(paramNameSmoothed).WithOneFrame(0f));
                        oneClip.Animating(clip => clip.AnimatesAnimator(paramNameSmoothed).WithOneFrame(driveVal));
                        
                        foreach (var target in my.blendshapeTargetMeshRenderers)
                        {
                            if(VRCFTValues.AveragedMapping.ContainsKey(paramName))
                            {
                                string blendshapeName1 = VRCFTValues.BlendshapeMapping[VRCFTValues.AveragedMapping[paramName][0]];
                                string blendshapeName2 = VRCFTValues.BlendshapeMapping[VRCFTValues.AveragedMapping[paramName][1]];
                                
                                zeroClip.Animating(clip => {clip.Animates(target, "blendShape." + blendshapeName1).WithOneFrame(0f);});
                                zeroClip.Animating(clip => {clip.Animates(target, "blendShape." + blendshapeName2).WithOneFrame(0f);});
                                oneClip.Animating(clip  => {clip.Animates(target, "blendShape." + blendshapeName1).WithOneFrame(driveVal*100f);});
                                oneClip.Animating(clip  => {clip.Animates(target, "blendShape." + blendshapeName2).WithOneFrame(driveVal*100f);});
                            } else {
                                string blendshapeName = VRCFTValues.BlendshapeMapping[paramName];

                                zeroClip.Animating(clip => {clip.Animates(target, "blendShape." + blendshapeName).WithOneFrame(0f);});
                                oneClip.Animating(clip =>  {clip.Animates(target, "blendShape." + blendshapeName).WithOneFrame(driveVal*100f);});
                            }
                        }
                    }

                    var factorTree = CreateFactorTree(smoothingFactorParameter, CreateProxyTree(parameter, zeroClip, oneClip), CreateSmoothingTree(smoothedParameter, zeroClip, oneClip));
                    smoothingChildMotions.Add(new ChildMotion {motion = factorTree, directBlendParameter = directNormalizer.Name, timeScale = 1.0f, threshold = 0.0f});
                }

                var smoothingDirectBlendTree = aac.NewBlendTreeAsRaw();
                smoothingDirectBlendTree = CreateDirectTree(smoothingLayer, directNormalizer, smoothingChildMotions.ToArray());

                var smoothingState = smoothingLayer.NewState("Smoothing").WithAnimation(smoothingDirectBlendTree);
                var ftDisabledClip = aac.NewClip("FaceTracking_Off");
                var offState = smoothingLayer.NewState("FaceTracking_Off").WithAnimation(ftDisabledClip);

                foreach (string paramName in smoothingParams)
                {
                    var smoothedParameter = fx.FloatParameter(paramName + "_Smoothed");
                    ftDisabledClip.Animating(clip => clip.AnimatesAnimator(smoothedParameter).WithOneFrame(0f));

                    foreach (var target in my.blendshapeTargetMeshRenderers)
                    {
                        if(VRCFTValues.AveragedMapping.ContainsKey(paramName))
                        {
                            string blendshapeName1 = VRCFTValues.BlendshapeMapping[VRCFTValues.AveragedMapping[paramName][0]];
                            string blendshapeName2 = VRCFTValues.BlendshapeMapping[VRCFTValues.AveragedMapping[paramName][1]];
                            
                            ftDisabledClip.Animating(clip => {clip.Animates(target, "blendShape." + blendshapeName1).WithOneFrame(0f);});
                            ftDisabledClip.Animating(clip => {clip.Animates(target, "blendShape." + blendshapeName2).WithOneFrame(0f);});
                        } else {
                            ftDisabledClip.Animating(clip => {clip.Animates(target, "blendShape." + VRCFTValues.BlendshapeMapping[paramName]).WithOneFrame(0f);});
                        }
                    }
                }

                smoothingState.TransitionsTo(offState).When(faceTrackingToggle.IsFalse());
                offState.TransitionsTo(smoothingState).When(faceTrackingToggle.IsTrue());
            }

            aac.RemoveAllMainLayers();
            if (my.manageExpressionParameters)
            {
                AddParametersToAvatar(my.avatar, SelectedParameters);
            }
            
        }

        // // // // // // // // // // // // // // // // // // // // // // // // // // // 

        private void AddParametersToAvatar(VRCAvatarDescriptor avatar, Dictionary<string, VRCFTValues.ParamMode> parameters)
        {
            VRCFTGenerator_ParameterUtility.AddParameter("FaceTracking", VRCExpressionParameters.ValueType.Bool, true, true, avatar);

            foreach (var param in parameters)
            {
                if (param.Value == VRCFTValues.ParamMode.floatParam)
                {
                    if(!VRCFTGenerator_ParameterUtility.ParameterExists(param.Key, avatar))
                    {
                        VRCFTGenerator_ParameterUtility.AddParameter(param.Key, VRCExpressionParameters.ValueType.Float, 0f, false, avatar);
                    }

                }
                else
                {
                    int paramBits = (int) param.Value;
                    bool isCombinedParam = VRCFTValues.CombinedMapping.ContainsKey(param.Key);

                    if (isCombinedParam && !VRCFTGenerator_ParameterUtility.ParameterExists(param.Key, avatar))
                    {
                        VRCFTGenerator_ParameterUtility.AddParameter(param.Key + "Negative", VRCExpressionParameters.ValueType.Bool, false, false, avatar);
                    }

                    for (int j = 0; j < paramBits; j++)
                    {
                        int binarySuffix = (int) Mathf.Pow(2, j);
                        string p = param.Key + binarySuffix.ToString();

                        if (!VRCFTGenerator_ParameterUtility.ParameterExists(param.Key, avatar))
                        {
                            VRCFTGenerator_ParameterUtility.AddParameter(p, VRCExpressionParameters.ValueType.Bool, false, false, avatar);
                        }
                    }
                }

            }
        }

        private AacFlFloatParameter CreateFramerateMeasurementSystem()
        {
            // Framerate Measurement System (from llealoo's implementation)
            var layer_frameTimeCaptureA = aac.CreateSupportingFxLayer("FramerateInvariance_CaptureA");
            // Declare our stuff here
            var frameTimeTriggerA = layer_frameTimeCaptureA.BoolParameter ("FrameTimeTriggerA");
            var frameTimeCaptureA = layer_frameTimeCaptureA.FloatParameter("FrameTimeCaptureA");
            var frameTimeTriggerB = layer_frameTimeCaptureA.BoolParameter ("FrameTimeTriggerB");
            var frameTimeCaptureB = layer_frameTimeCaptureA.FloatParameter("FrameTimeCaptureB");
            var frameTimeMeasured = layer_frameTimeCaptureA.FloatParameter("FrameTimeMeasured");

            // Need 2 different layers to properly measure
            // Frame Capture Layer A
            var clip_frameTimeCaptureA = aac.NewClip("FrameTimeA")
                .Animating(clip => clip.AnimatesAnimator(frameTimeCaptureA)
                    .WithSecondsUnit(keyframes => keyframes.Linear(0, 0f).Linear(1, 1f)));

            var state_frameTimeFlipperA = layer_frameTimeCaptureA.NewState("CaptureA")
                .WithAnimation(clip_frameTimeCaptureA);

            state_frameTimeFlipperA.TransitionsTo(state_frameTimeFlipperA).WithTransitionToSelf().When(frameTimeTriggerB.IsTrue());

            // Frame Capture Layer B
            var layer_frameTimeCaptureB = aac.CreateSupportingFxLayer("FramerateInvariance_CaptureB");
            var clip_frameTimeCaptureB = aac.NewClip("FrameTimeB")
                .Animating(clip => clip.AnimatesAnimator(frameTimeCaptureB)
                    .WithSecondsUnit(keyframes => keyframes.Linear(0, 0f).Linear(1, 1f)));

            var state_frameTimeFlipperB = layer_frameTimeCaptureB.NewState("CaptureB")
                .WithAnimation(clip_frameTimeCaptureB);

            state_frameTimeFlipperB.TransitionsTo(state_frameTimeFlipperB).WithTransitionToSelf().When(frameTimeTriggerA.IsTrue());

            // Grab from correct capture layer
            var layer_frameTimeMeasurement = aac.CreateSupportingFxLayer("FramerateInvariance_Frametime");
            var clip_frameTimeMeasured = aac.NewClip("FrameTimeMeasured")
                .Animating(clip => clip.AnimatesAnimator(frameTimeMeasured)
                    .WithSecondsUnit(keyframes => keyframes.Linear(0, 0f).Linear(1, 1f)));

            var state_frameTimeCaptureA = layer_frameTimeMeasurement.NewState("ReadCaptureA")
                .WithAnimation(clip_frameTimeMeasured)
                .MotionTime(frameTimeCaptureA)
                .Drives(frameTimeTriggerA, true)
                .Drives(frameTimeTriggerB, false);
            
            var state_frameTimeCaptureB = layer_frameTimeMeasurement.NewState("ReadCaptureB")
                .WithAnimation(clip_frameTimeMeasured)
                .MotionTime(frameTimeCaptureB)
                .Drives(frameTimeTriggerA, false)
                .Drives(frameTimeTriggerB, true);

            state_frameTimeCaptureA.TransitionsTo(state_frameTimeCaptureB).When(frameTimeTriggerA.IsTrue());
            state_frameTimeCaptureB.TransitionsTo(state_frameTimeCaptureA).When(frameTimeTriggerB.IsTrue());

            return frameTimeMeasured;
        }

        private AacFlFloatParameter CreateLocalRemoteSmoothingLayer(AacFlFloatParameter frameTimeMeasured)
        {
            AacFlLayer smoothingFactorParameterLayer = aac.CreateSupportingFxLayer("SmoothingParameter");
            AacFlFloatParameter smoothingFactorParameter = smoothingFactorParameterLayer.FloatParameter("FT_SmoothingAlpha");

            var IsLocal = smoothingFactorParameterLayer.BoolParameter("IsLocal"); // Only adds if not already present
            smoothingFactorParameterLayer.OverrideValue(smoothingFactorParameter, my.remoteSmoothingTimeConstant); // Forces the value to init at remote value

            var smoothingParameterNameRemote = "FT_SmoothingAlpha_Remote";
            var smoothingParameterNameLocal = "FT_SmoothingAlpha_Local";

            var smoothingFactorParameterRemote = smoothingFactorParameterLayer.FloatParameter(smoothingParameterNameRemote);
            var smoothingFactorParameterLocal  = smoothingFactorParameterLayer.FloatParameter(smoothingParameterNameLocal);

            float tauRemote = my.remoteSmoothingTimeConstant;
            float tauLocal  = my.remoteSmoothingTimeConstant;// my.localSmoothingTimeConstant;

            float keyframeTime = 0f;
            Keyframe[] smoothingParamKeyframesRemote = new Keyframe[61];
            Keyframe[] smoothingParamKeyframesLocal  = new Keyframe[61];
            var smoothingParamCurveRemote = new AnimationCurve(); 
            var smoothingParamCurveLocal  = new AnimationCurve(); 
            float sRemote;
            float dsRemote;
            float sLocal;
            float dsLocal;

            for(int j = 0; j < 61; j++)
            {
                keyframeTime = (float)j * 1f/60f;

                sRemote = Mathf.Clamp(Mathf.Exp(-keyframeTime / tauRemote), 0.01f, 0.9f);
                sLocal  = Mathf.Clamp(Mathf.Exp(-keyframeTime / tauLocal),  0.01f, 0.9f);

                dsRemote = sRemote <= 0.01f ? 0 : -Mathf.Exp(-keyframeTime / tauRemote)/tauRemote;
                dsLocal  = sLocal  <= 0.01f ? 0 : -Mathf.Exp(-keyframeTime / tauLocal)/tauLocal;

                smoothingParamKeyframesRemote[j] = new Keyframe(keyframeTime, sRemote, dsRemote, dsRemote);
                smoothingParamKeyframesLocal[j]  = new Keyframe(keyframeTime, sLocal, dsLocal, dsLocal);
            }

            smoothingParamCurveRemote.keys = smoothingParamKeyframesRemote;
            smoothingParamCurveLocal.keys  = smoothingParamKeyframesLocal;

            var clip_remoteSmoothing = aac.NewClip("SmoothingAlphaRemote");
            var clip_localSmoothing = aac.NewClip("SmoothingAlphaLocal");

            clip_remoteSmoothing.Clip.SetCurve("", typeof(Animator), smoothingFactorParameter.Name, smoothingParamCurveRemote);
            clip_localSmoothing.Clip.SetCurve("", typeof(Animator), smoothingFactorParameter.Name, smoothingParamCurveLocal);

            var remoteSmoothingState = smoothingFactorParameterLayer.NewState("Remote Smoothing")//.Drives(smoothingFactorParameter, my.remoteSmoothingAlpha);
            .WithAnimation(clip_remoteSmoothing).MotionTime(frameTimeMeasured);
            var localSmoothingState = smoothingFactorParameterLayer.NewState("Local Smoothing")//.Drives(smoothingFactorParameter, my.localSmoothingAlpha);
            .WithAnimation(clip_localSmoothing).MotionTime(frameTimeMeasured);

            // Transition between local and remote smoothing
            localSmoothingState.TransitionsTo(remoteSmoothingState).When(IsLocal.IsFalse());
            remoteSmoothingState.TransitionsTo(localSmoothingState).When(IsLocal.IsTrue());

            return smoothingFactorParameter;
        }

        // // //

        private BlendTree CreateCombinedTree(AacFlFloatParameter manualControlParameter, AacFlClip minusClip, AacFlClip zeroClip, AacFlClip oneClip)
        {
            var proxyTree = aac.NewBlendTreeAsRaw();
            proxyTree.blendParameter = manualControlParameter.Name;
            proxyTree.blendType = BlendTreeType.Simple1D;
            proxyTree.minThreshold = -1;
            proxyTree.maxThreshold = 1;
            proxyTree.useAutomaticThresholds = true;
            proxyTree.children = new[]
            {
                new ChildMotion {motion = minusClip.Clip, timeScale = 1, threshold = -1},
                new ChildMotion {motion = zeroClip.Clip, timeScale = 1, threshold = 0},
                new ChildMotion {motion = oneClip.Clip, timeScale = 1, threshold = 1}
            };
            return proxyTree;
        }

        private BlendTree CreateProxyTree(AacFlFloatParameter manualControlParameter, AacFlClip zeroClip, AacFlClip oneClip)
        {
            var proxyTree = aac.NewBlendTreeAsRaw();
            proxyTree.blendParameter = manualControlParameter.Name;
            proxyTree.blendType = BlendTreeType.Simple1D;
            proxyTree.minThreshold = 0;
            proxyTree.maxThreshold = 1;
            proxyTree.useAutomaticThresholds = true;
            proxyTree.children = new[]
            {
                new ChildMotion {motion = zeroClip.Clip, timeScale = 1, threshold = 0},
                new ChildMotion {motion = oneClip.Clip, timeScale = 1, threshold = 1}
            };
            return proxyTree;
        }

        private BlendTree CreateFactorTree(AacFlFloatParameter smoothingFactorParameter, BlendTree proxyTree, BlendTree smoothingTree)
        {
            var factorTree = aac.NewBlendTreeAsRaw();
            {
                factorTree.blendParameter = smoothingFactorParameter.Name;
                factorTree.blendType = BlendTreeType.Simple1D;
                factorTree.minThreshold = 0;
                factorTree.maxThreshold = 1;
                factorTree.useAutomaticThresholds = true;
                factorTree.children = new[]
                {
                    new ChildMotion {motion = proxyTree, timeScale = 1, threshold = 0},
                    new ChildMotion {motion = smoothingTree, timeScale = 1, threshold = 1}
                };
            }
            return factorTree;
        }

        private BlendTree CreateSmoothingTree(AacFlFloatParameter smoothedParameter, AacFlClip zeroClip, AacFlClip oneClip)
        {
            var smoothingTree = aac.NewBlendTreeAsRaw();
            smoothingTree.blendParameter = smoothedParameter.Name;
            smoothingTree.blendType = BlendTreeType.Simple1D;
            smoothingTree.minThreshold = 0;
            smoothingTree.maxThreshold = 1;
            smoothingTree.useAutomaticThresholds = true;
            smoothingTree.children = new[]
            {
                new ChildMotion {motion = zeroClip.Clip, timeScale = 1, threshold = 0},
                new ChildMotion {motion = oneClip.Clip, timeScale = 1, threshold = 1}
            };
            return smoothingTree;
        }

        private BlendTree CreateDirectTree(AacFlLayer layer, AacFlFloatParameter normalizingParameter, AacFlFloatParameter[] blendParameters, BlendTree[] children, float minThreshold = 0f, float maxThreshold = 1f, bool useAutomaticThresholds = false)
        {
            if(blendParameters.Length != children.Length)
            {
                throw new Exception("Error in CreateDirectTree: blendParameters.Length != children.Length");
            }

            var directTree = aac.NewBlendTreeAsRaw();
            directTree.blendType = BlendTreeType.Direct;
            directTree.blendParameter = normalizingParameter.Name;
            directTree.minThreshold = minThreshold;
            directTree.maxThreshold = maxThreshold;
            directTree.useAutomaticThresholds = useAutomaticThresholds;
            ChildMotion[] childMotions = new ChildMotion[blendParameters.Length];
            
            for(int ch = 0; ch < children.Length; ch++)
            {
                childMotions[ch] = new ChildMotion {motion = children[ch], directBlendParameter = blendParameters[ch].Name, timeScale = 1.0f, threshold = 0.0f};
            }

            directTree.children = childMotions;

            return directTree;
        }

        private BlendTree CreateDirectTree(AacFlLayer layer, AacFlFloatParameter normalizingParameter, ChildMotion[] childMotions, float minThreshold = 0f, float maxThreshold = 1f, bool useAutomaticThresholds = false)
        {
            var directTree = aac.NewBlendTreeAsRaw();
            directTree.blendType = BlendTreeType.Direct;
            directTree.blendParameter = normalizingParameter.Name;
            directTree.minThreshold = minThreshold;
            directTree.maxThreshold = maxThreshold;
            directTree.useAutomaticThresholds = useAutomaticThresholds;

            directTree.children = childMotions;

            return directTree;
        }
    }
}
#endif
