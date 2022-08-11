#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using AnimatorAsCode.V0;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using ValueType = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.ValueType;
using UnityEditor;
using UnityEditor.Animations;

namespace AnimatorAsCodeFramework.Razgriz.VRCFTGenerator
{

    [Serializable]
    public class ParamSpec
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

        public SkinnedMeshRenderer[] blendshapeTargetMeshRenderers = new SkinnedMeshRenderer[1];
        public float remoteSmoothingTimeConstant = 0.7f;
        [Header("Use VRCFT name of params in synced params - ex. Face/Combined/General/TongueX => TongueX")]
        [Space(25)]
        public ParamSpec[] paramsToAnimate;
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
            if (GUILayout.Button("Remove Selected Features")){ RemoveFeatures(); }
            serializedObject.ApplyModifiedProperties();
        }

        // // //

        private void AddFeatures()
        {
            InitializeAAC();
            CreateFTBlendshapeController();
            AssetDatabase.SaveAssets();
        }

        private void RemoveFeatures()
        {
            InitializeAAC();
            RemoveFTBlendshapeController();
            AssetDatabase.SaveAssets();
        }

        private static string[] FTBaseParameters = VRCFTValues.BlendshapeMapping.Keys.ToArray();
        private static string[] FTBlendshapes = VRCFTValues.BlendshapeMapping.Values.ToArray();

        private void RemoveFTBlendshapeController()
        {
            // Find FX Layer
            AnimatorController fxLayer = (AnimatorController) my.avatar.baseAnimationLayers.First(it => it.type == VRCAvatarDescriptor.AnimLayerType.FX).animatorController;
            foreach(var layer in fxLayer.layers)
            {
                string layerName = layer.name;
                string systemPrefix = SystemName+"__";
                string supportingLayerName = layerName.Replace(systemPrefix, "");

                if(layerName.StartsWith(systemPrefix))
                {
                    aac.RemoveAllSupportingLayers(supportingLayerName);
                }
            }
        }

        private void CreateFTBlendshapeController()
        {
            // Target a non-existent GameObject
            var clipDoNothing = aac.NewClip("DoNothing").Animating(clip => clip.Animates("DoNothing", typeof(GameObject), "m_IsEnabled").WithOneFrame(0f));

            var fx = aac.CreateMainFxLayer();

            // AacFlFloatParameter frameTimeMeasured = CreateFramerateMeasurementSystem();

            // Set up Local/Remote Smoothing
            // AacFlFloatParameter smoothingFactorParameter = CreateLocalRemoteSmoothingLayer(frameTimeMeasured);

            var smoothingFactorParameter = fx.FloatParameter("FT_SmoothingAlpha");
            fx.OverrideValue(smoothingFactorParameter, my.remoteSmoothingTimeConstant);


            Dictionary<string, VRCFTValues.ParamMode> SelectedParameters = new Dictionary<string, VRCFTValues.ParamMode>();

            // BINARY
            List<string> blendshapes = new List<string>();
            // List<string> prioFloats = new List<string>();
            List<string> selectedBinaryLayers = new List<string>();
            List<string> selectedFloatParams = new List<string>();
            List<string> parseLayers = new List<string>();
            List<string> AveragedParam = new List<string>();
            List<string> smoothingParams = new List<string>();

            int FTParamCost = 0;

            foreach (ParamSpec paramSpec in my.paramsToAnimate)
            {
                SelectedParameters.Add(Enum.GetName(typeof(VRCFTValues.ParameterName), paramSpec.VRCFTParameter), paramSpec.mode);
            }

            // Pre-parsing
            foreach (string param in SelectedParameters.Keys.ToArray())
            {
                if (VRCFTValues.CombinedMapping.ContainsKey(param))
                {
                    smoothingParams.Add(VRCFTValues.CombinedMapping[param][0]);
                    smoothingParams.Add(VRCFTValues.CombinedMapping[param][1]);

                    bool param0IsAverage = VRCFTValues.AveragedMapping.ContainsKey(VRCFTValues.CombinedMapping[param][0]);
                    bool param1IsAverage = VRCFTValues.AveragedMapping.ContainsKey(VRCFTValues.CombinedMapping[param][1]);

                    if (param0IsAverage)
                    {
                        blendshapes.Add(VRCFTValues.AveragedMapping[VRCFTValues.CombinedMapping[param][0]][0]);
                        blendshapes.Add(VRCFTValues.AveragedMapping[VRCFTValues.CombinedMapping[param][0]][1]);
                    } else {
                        blendshapes.Add(VRCFTValues.CombinedMapping[param][0]);
                    }

                    if (param1IsAverage)
                    {
                        blendshapes.Add(VRCFTValues.AveragedMapping[VRCFTValues.CombinedMapping[param][1]][0]);
                        blendshapes.Add(VRCFTValues.AveragedMapping[VRCFTValues.CombinedMapping[param][1]][1]);
                    } else {
                        blendshapes.Add(VRCFTValues.CombinedMapping[param][1]);
                    }

                    FTParamCost += (int)SelectedParameters[param];

                    if(SelectedParameters[param] != VRCFTValues.ParamMode.floatParam)
                    {
                        selectedBinaryLayers.Add(param);
                        FTParamCost += 1;
                    } else {
                        selectedFloatParams.Add(param);
                        fx.FloatParameter(param);
                        // prioFloats.Add(VRCFTValues.CombinedMapping[param][0]);
                        // prioFloats.Add(VRCFTValues.CombinedMapping[param][1]);
                    }
                } else {
                    smoothingParams.Add(param);

                    blendshapes.Add(param);
                    FTParamCost += (int)SelectedParameters[param];
                    if(( (VRCFTValues.CombinedMapping.ContainsKey(param) || VRCFTValues.AveragedMapping.ContainsKey(param)) 
                            && SelectedParameters[param] != VRCFTValues.ParamMode.floatParam))
                    {
                        FTParamCost++; 
                    }

                    if(SelectedParameters[param] != VRCFTValues.ParamMode.floatParam)
                    {
                        selectedBinaryLayers.Add(param);
                    } else {
                        fx.FloatParameter(param);
                        selectedFloatParams.Add(param);
                    }
                }
            }

            parseLayers = selectedBinaryLayers.Concat(selectedFloatParams).ToList();

            // Create Binary Cast/Decode Layers
            int summingLayerCount = parseLayers.ToArray().Length;
            if (summingLayerCount > 0)
            {
                var binaryCastLayer = aac.CreateSupportingFxLayer("BinaryCast");
                var binaryCastState = binaryCastLayer.NewState("Cast");
                binaryCastState.TransitionsTo(binaryCastState).AfterAnimationIsAtLeastAtPercent(0f).WithTransitionToSelf(); // re-evaluate every frame

                var decodeLayer = aac.CreateSupportingFxLayer("DecodeLayer");
                var binarySumState = decodeLayer.NewState("DecodeBlendTree");
                var binarySumTopLevelNormalizerParam = decodeLayer.FloatParameter("DecodeBlendTreeNormalizer");
                decodeLayer.OverrideValue(binarySumTopLevelNormalizerParam, 1f/(float)summingLayerCount);

                var binarySumTopLevelDirectBlendTree = aac.NewBlendTreeAsRaw();
                List<ChildMotion> binarySumTopLevelChildMotions = new List<ChildMotion>();

                binarySumTopLevelDirectBlendTree.blendType = BlendTreeType.Direct;
                binarySumTopLevelDirectBlendTree.blendParameter = binarySumTopLevelNormalizerParam.Name;
                binarySumTopLevelDirectBlendTree.minThreshold = 0;
                binarySumTopLevelDirectBlendTree.maxThreshold = 1;
                binarySumTopLevelDirectBlendTree.useAutomaticThresholds = false;

                int pr = 0;

                foreach (string param in selectedBinaryLayers)
                {
                    int paramBits = (int)SelectedParameters[param];
                    bool isCombinedParam = VRCFTValues.CombinedMapping.ContainsKey(param);
                    string paramPositiveName = isCombinedParam ? VRCFTValues.CombinedMapping[param][0] : param;
                    string paramNegativeName = isCombinedParam ? VRCFTValues.CombinedMapping[param][1] : param;

                    var paramPositive = binaryCastLayer.FloatParameter(paramPositiveName);
                    var paramNegative = binaryCastLayer.FloatParameter(paramPositiveName);

                    var zeroClip = aac.NewClip(param+"_0_scaled");
                    var oneClipParam1  = aac.NewClip(param+"_1_scaled");
                    var oneClipParam2 = aac.NewClip(param+"_2_scaled");

                    var boolParamNegative = binaryCastLayer.BoolParameter(param+"Negative");
                    var floatBoolParamNegative = binaryCastLayer.FloatParameter(param+"Negative_Float");

                    if(isCombinedParam) 
                    {
                        binaryCastState.DrivingCasts(boolParamNegative, 0f, 1f, floatBoolParamNegative, 0f, 1f);
                    }
                    
                    foreach(string keyframeParam in parseLayers)
                    {
                        var paramToAnimate = decodeLayer.FloatParameter(keyframeParam);

                        // zeroClip.Animating(clip => clip.AnimatesAnimator(paramToAnimate).WithOneFrame(0f));

                        float oneClipVal = keyframeParam == param ? 1f*(float)summingLayerCount : 0f;

                        bool keyframeIsCombined = VRCFTValues.CombinedMapping.ContainsKey(keyframeParam);
                        var keyframePositiveName = keyframeIsCombined ? VRCFTValues.CombinedMapping[keyframeParam][0] : keyframeParam;
                        var keyframeNegativeName = keyframeIsCombined ? VRCFTValues.CombinedMapping[keyframeParam][1] : keyframeParam;

                        var keyframePositive = binaryCastLayer.FloatParameter(keyframePositiveName);
                        var keyframeNegative = binaryCastLayer.FloatParameter(keyframeNegativeName);

                        if(keyframeIsCombined)
                        {
                            zeroClip.Animating(clip => clip.AnimatesAnimator(keyframePositive).WithOneFrame(0f));
                            zeroClip.Animating(clip => clip.AnimatesAnimator(keyframeNegative).WithOneFrame(0f));

                            oneClipParam1.Animating(clip => clip.AnimatesAnimator(keyframePositive).WithOneFrame(oneClipVal));
                            oneClipParam1.Animating(clip => clip.AnimatesAnimator(keyframeNegative).WithOneFrame(0f));

                            oneClipParam2.Animating(clip => clip.AnimatesAnimator(keyframePositive).WithOneFrame(0f));
                            oneClipParam2.Animating(clip => clip.AnimatesAnimator(keyframeNegative).WithOneFrame(oneClipVal));
                        } else {
                            zeroClip.Animating(clip => clip.AnimatesAnimator(keyframePositive).WithOneFrame(0f));
                            oneClipParam1.Animating(clip => clip.AnimatesAnimator(keyframePositive).WithOneFrame(oneClipVal));
                            oneClipParam2.Animating(clip => clip.AnimatesAnimator(keyframePositive).WithOneFrame(0f));
                        }
                    }

                    BlendTree[] binarySumChildBlendTreesPositive = new BlendTree[paramBits];
                    BlendTree[] binarySumChildBlendTreesNegative = new BlendTree[paramBits];
                    // BlendTree[] binarySumChildBlendTrees = new BlendTree[paramBits];
                    AacFlFloatParameter[] binarySumChildBlendTreesBlendParams = new AacFlFloatParameter[paramBits];
                    var parameterDirectTreeNormalizer = decodeLayer.FloatParameter(param+"_Normalizer");
                    fx.OverrideValue(parameterDirectTreeNormalizer, 1f);

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

                    pr++;
                }

//                pr = 0;
                foreach (string param in selectedFloatParams)
                {

                    var zeroClip  = aac.NewClip(param+"_0_scaled");
                    var oneClip   = aac.NewClip(param+"_1_scaled");
                    var minusClip = aac.NewClip(param+"_-1_scaled");

                    foreach(string keyframeParam in parseLayers)
                    {
                        var paramToAnimate = decodeLayer.FloatParameter(keyframeParam);

                        // zeroClip.Animating(clip => clip.AnimatesAnimator(paramToAnimate).WithOneFrame(0f));

                        float oneClipVal = keyframeParam == param ? 1f*(float)summingLayerCount : 0f;

                        bool keyframeIsCombined = VRCFTValues.CombinedMapping.ContainsKey(keyframeParam);
                        var keyframePositiveName = keyframeIsCombined ? VRCFTValues.CombinedMapping[keyframeParam][0] : keyframeParam;
                        var keyframeNegativeName = keyframeIsCombined ? VRCFTValues.CombinedMapping[keyframeParam][1] : keyframeParam;

                        var keyframePositive = binaryCastLayer.FloatParameter(keyframePositiveName);
                        var keyframeNegative = binaryCastLayer.FloatParameter(keyframeNegativeName);

                        zeroClip.Animating(clip => clip.AnimatesAnimator(keyframePositive).WithOneFrame(0f));
                        oneClip.Animating(clip => clip.AnimatesAnimator(keyframePositive).WithOneFrame(oneClipVal));
                        minusClip.Animating(clip => clip.AnimatesAnimator(keyframeNegative).WithOneFrame(oneClipVal));
                    }

                    BlendTree childMotion = CreateCombinedTree(decodeLayer.FloatParameter(param), minusClip, zeroClip, oneClip);

                    binarySumTopLevelChildMotions.Add(new ChildMotion {motion = childMotion, directBlendParameter = binarySumTopLevelNormalizerParam.Name, timeScale = 1.0f, threshold = 0.0f});

                    pr++;
                }

                binarySumTopLevelDirectBlendTree.children = binarySumTopLevelChildMotions.ToArray();
                binarySumState.WithAnimation(binarySumTopLevelDirectBlendTree);
            }

            string[] UsedBlendshapes = blendshapes.ToArray();
            // string[] UsedBlendshapes = FTBaseParameters.ToArray();
            
            int numDirectStates;
            numDirectStates = smoothingParams.ToArray().Length;

            // Smoothing
            if(true)
            {
                var smoothingLayer = aac.CreateSupportingFxLayer("SmoothingLayer"); // Adds or Overwrites

                var faceTrackingToggle = smoothingLayer.BoolParameter("FaceTracking");
                smoothingLayer.OverrideValue(faceTrackingToggle, true);
                var smoothingDirectBlendTree = aac.NewBlendTreeAsRaw();

                // var prioritySmoothingAlpha = smoothingLayer.FloatParameter("PrioritySmoothingAlpha");
                smoothingLayer.OverrideValue(smoothingFactorParameter, 0.7f);

                // Need to normalize all direct blendtree weights to sum to 1
                var directNormalizerName = "Blend1";
                var directNormalizer = fx.FloatParameter(directNormalizerName);

                var directNormalizerName2 = "Blend2";
                var directNormalizer2 = fx.FloatParameter(directNormalizerName2);
                fx.OverrideValue(directNormalizer2, 1/(float)numDirectStates);

                smoothingDirectBlendTree.blendType = BlendTreeType.Direct;
                smoothingDirectBlendTree.blendParameter = smoothingFactorParameter.Name;
                smoothingDirectBlendTree.minThreshold = 0;
                smoothingDirectBlendTree.maxThreshold = 1;
                smoothingDirectBlendTree.useAutomaticThresholds = false;

                int i;

                i = 0;

                fx.OverrideValue(directNormalizer, 1/(float)numDirectStates);

                ChildMotion[] smoothingChildMotions = new ChildMotion[numDirectStates];

                foreach (string parameterToSmooth in smoothingParams)
                {
                    var parameter = smoothingLayer.FloatParameter(parameterToSmooth);
                    var smoothedParameter = smoothingLayer.FloatParameter(parameterToSmooth + "_Smoothed");

                    var zeroClip = aac.NewClip(parameterToSmooth+"param_0_scaled");
                    var oneClip = aac.NewClip(parameterToSmooth+"param_1_scaled");

                    foreach(string paramName in smoothingParams)
                    {
                        var paramNameSmoothed = smoothingLayer.FloatParameter(paramName + "_Smoothed");

                        zeroClip.Animating(clip => clip.AnimatesAnimator(paramNameSmoothed).WithOneFrame(0f));

                        float driveVal = paramName == parameterToSmooth ? 1f*(float)numDirectStates : 0f;
                        oneClip.Animating(clip => clip.AnimatesAnimator(paramNameSmoothed).WithOneFrame(driveVal));
                        
                        foreach (var target in my.blendshapeTargetMeshRenderers)
                        {
                            if(VRCFTValues.AveragedMapping.ContainsKey(paramName))
                            {
                                string blendshape1 = VRCFTValues.BlendshapeMapping[VRCFTValues.AveragedMapping[paramName][0]];
                                string blendshape2 = VRCFTValues.BlendshapeMapping[VRCFTValues.AveragedMapping[paramName][1]];
                                
                                zeroClip.Animating(clip => {clip.Animates(target, "blendShape."+blendshape1).WithOneFrame(0f);});
                                zeroClip.Animating(clip => {clip.Animates(target, "blendShape."+blendshape2).WithOneFrame(0f);});
                                oneClip.Animating(clip => {clip.Animates(target, "blendShape."+blendshape1).WithOneFrame(driveVal*100f);});
                                oneClip.Animating(clip => {clip.Animates(target, "blendShape."+blendshape2).WithOneFrame(driveVal*100f);});
                            } else {
                                string blendshape = VRCFTValues.BlendshapeMapping[paramName];

                                zeroClip.Animating(clip => {clip.Animates(target, "blendShape."+blendshape).WithOneFrame(0f);});
                                oneClip.Animating(clip => {clip.Animates(target, "blendShape."+blendshape).WithOneFrame(driveVal*100f);});
                            }
                        }
                    }


                    var factorTree = CreateFactorTree(smoothingFactorParameter, CreateProxyTree(parameter, zeroClip, oneClip), CreateSmoothingTree(smoothedParameter, zeroClip, oneClip));
                    // var prioFactorTree = CreateFactorTree(smoothingFactorParameter, CreateProxyTree(parameter, zeroClip, oneClip), CreateSmoothingTree(smoothedParameter, zeroClip, oneClip));

                    smoothingChildMotions[i] = new ChildMotion {motion = factorTree, directBlendParameter = directNormalizerName, timeScale = 1.0f, threshold = 0.0f};

                /*
                    if (prioFloats.Contains(parameterToSmooth))
                    {
                        smoothingChildMotions[i] = new ChildMotion {motion = prioFactorTree, directBlendParameter = directNormalizerName, timeScale = 1.0f, threshold = 0.0f};
                    } else {
                        smoothingChildMotions[i] = new ChildMotion {motion = factorTree, directBlendParameter = directNormalizerName, timeScale = 1.0f, threshold = 0.0f};
                    }
                */

                    i++;
                }

                smoothingDirectBlendTree.children = smoothingChildMotions;

                var smoothingState = smoothingLayer.NewState("Smoothing").WithAnimation(smoothingDirectBlendTree);
                var ftDisabledClip = aac.NewClip("FaceTracking_Off");
                var offState = smoothingLayer.NewState("FaceTracking_Off").WithAnimation(ftDisabledClip);

                foreach (string paramName in smoothingParams)
                {
                    var smoothedParameter = smoothingLayer.FloatParameter(paramName + "_Smoothed");
                    ftDisabledClip.Animating(clip => clip.AnimatesAnimator(smoothedParameter).WithOneFrame(0f));

                    foreach (var target in my.blendshapeTargetMeshRenderers)
                    {
                        if(VRCFTValues.AveragedMapping.ContainsKey(paramName))
                        {

                                string blendshape1 = VRCFTValues.BlendshapeMapping[VRCFTValues.AveragedMapping[paramName][0]];
                                string blendshape2 = VRCFTValues.BlendshapeMapping[VRCFTValues.AveragedMapping[paramName][1]];
                            
                            ftDisabledClip.Animating(clip => {clip.Animates(target, "blendShape."+blendshape1).WithOneFrame(0f);});
                            ftDisabledClip.Animating(clip => {clip.Animates(target, "blendShape."+blendshape2).WithOneFrame(0f);});
                        } else {
                            ftDisabledClip.Animating(clip => {clip.Animates(target, "blendShape."+VRCFTValues.BlendshapeMapping[paramName]).WithOneFrame(0f);});
                        }
                    }
                }

                smoothingState.TransitionsTo(offState).When(faceTrackingToggle.IsFalse());
                offState.TransitionsTo(smoothingState).When(faceTrackingToggle.IsTrue());
            }

            aac.RemoveAllMainLayers();

            EditorUtility.DisplayDialog("Face Tracking Generator","Generated Face Tracking, Parameter Cost: " + FTParamCost.ToString(), "OK");
        }

        // // // // // // // // // // // // // // // // // // // // // // // // // // // 

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

        private BlendTree CreateDirectTree(AacFlLayer layer, AacFlFloatParameter normalizingParameter, AacFlFloatParameter[] blendParameters, BlendTree[] children)
        {
            if(blendParameters.Length != children.Length)
            {
                throw new Exception("Error in CreateDirectTree: blendParameters.Length != children.Length");
            }

            var directTree = aac.NewBlendTreeAsRaw();
            directTree.blendType = BlendTreeType.Direct;
            directTree.blendParameter = normalizingParameter.Name;
            directTree.minThreshold = 0;
            directTree.maxThreshold = 1;
            directTree.useAutomaticThresholds = false;
            ChildMotion[] childMotions = new ChildMotion[blendParameters.Length];
            
            for(int ch = 0; ch < children.Length; ch++)
            {
                childMotions[ch] = new ChildMotion {motion = children[ch], directBlendParameter = blendParameters[ch].Name, timeScale = 1.0f, threshold = 0.0f};
            }

            directTree.children = childMotions;

            return directTree;
        }
    }
}
#endif
