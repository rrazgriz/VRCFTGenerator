#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using VRCFTGenerator.AnimatorAsCode.V0;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using UnityEditor;
using UnityEditor.Animations;

namespace Raz.VRCFTGenerator
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
        [Header("This will change how the controller is generated. Recommended to keep on, regardless of rest of controller.")]
        public bool writeDefaults = true;
        [Header("[Experimental] Instead of using an explicit cast state, implicitly cast expression Bool -> Animator Float")]
        public bool useImplicitParameterCasting = false;
        [Header("[Experimental] Generate in a single layer")]
        public bool singleLayer = false;
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
        private const string SystemName = "FT";
        private VRCFTGenerator generator;
        private AacFlBase aac;

        private void InitializeAAC()
        {
            generator = (VRCFTGenerator) target;
            var wd = generator.writeDefaults ? AACTemplate.Options().WriteDefaultsOn() : AACTemplate.Options().WriteDefaultsOff();
            aac = AACTemplate.AnimatorAsCode(SystemName, generator.avatar, generator.assetContainer, generator.assetKey, wd);
        }

        // // // // // // // // // // // // // // // // // // // // // // // // // // // 

        public static void DrawUILine(Color color, int thickness = 2, int padding = 8)
        {
            Rect r = EditorGUILayout.GetControlRect(GUILayout.Height(padding+thickness));
            r.height = thickness;
            r.y+=padding/2;
            r.x-=2;
            EditorGUI.DrawRect(r, color);
        }
        public static void DrawUILine(int thickness = 2, int padding = 8) => DrawUILine(darkgray, thickness, padding);
        static Color darkgray = new Color(0.184f, 0.184f, 0.184f, 1.0f);

        private bool paramListFoldout = false;
        private bool paramCoverageFoldout = false;

        private bool AnalyzeSetupGui()
        {
            bool hasDuplicates = false;

            paramListFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(paramListFoldout, "Shapes in use");

            HashSet<string> shapesInUse = new HashSet<string>();
            int paramCost = 1; // One base for global FT toggle

            foreach(var parameterSpec in generator.paramsToAnimate)
            {
                paramCost += (int)parameterSpec.mode;
                string paramName = parameterSpec.VRCFTParameter.ToString();
                List<string> shapeParams = new List<string>();

                if(VRCFTValues.CombinedMapping.ContainsKey(paramName))
                {
                    if(parameterSpec.mode != VRCFTValues.ParamMode.floatParam) paramCost++;
                    foreach(string constituentParam in VRCFTValues.CombinedMapping[paramName])
                    {
                        if(VRCFTValues.AveragedMapping.ContainsKey(constituentParam))
                        {
                            shapeParams.AddRange(VRCFTValues.AveragedMapping[constituentParam]);
                        }
                        else
                        {
                            shapeParams.Add(constituentParam);
                        }
                    }
                }
                else if(VRCFTValues.AveragedMapping.ContainsKey(paramName))
                {
                    if(parameterSpec.mode != VRCFTValues.ParamMode.floatParam) paramCost++;
                    shapeParams.AddRange(VRCFTValues.AveragedMapping[paramName]);
                }
                else
                {
                    shapeParams.Add(paramName);
                }

                foreach(var shape in shapeParams)
                {
                    using(new EditorGUILayout.HorizontalScope())
                    {
                        bool isDuplicate = !shapesInUse.Add(shape);
                        if(isDuplicate)
                            hasDuplicates = true;

                        if(paramListFoldout)
                        {
                            Texture icon = isDuplicate ? EditorGUIUtility.IconContent("Error").image : (Texture)null;
                            var label = new GUIContent($"    {shape}", icon);
                            EditorGUILayout.LabelField(label);

                            if(shapeParams.Count > 1)
                                EditorGUILayout.LabelField($"({paramName})", EditorStyles.centeredGreyMiniLabel);  
                        }                          
                    }
                }
            }

            int usedShapes = shapesInUse.Count;
            int availableShapes = VRCFTValues.BlendshapeMapping.Keys.Count;
            float coveragePercent = ((float)usedShapes)/((float)availableShapes) * 100f;

            if(paramListFoldout)
            {
                paramCoverageFoldout = EditorGUILayout.Foldout(paramCoverageFoldout, "Unused Blendshapes");
                if(paramCoverageFoldout)
                {
                    foreach(string shape in VRCFTValues.BlendshapeMapping.Keys)
                    {
                        if(!shapesInUse.Contains(shape))
                        {
                            EditorGUILayout.LabelField(shape);
                        }
                    }
                }
            }

            EditorGUILayout.EndFoldoutHeaderGroup();

            DrawUILine();
            EditorGUILayout.LabelField($"Shapes used: {usedShapes}/{availableShapes} ({coveragePercent.ToString("0")}%)");
            EditorGUILayout.LabelField($"Parameter Cost: {paramCost} bits");

            return hasDuplicates;
        }

        public override void OnInspectorGUI()
        {
            generator = (VRCFTGenerator) target;

            serializedObject.Update();
            // Unity GUI Magic?
            var prop = serializedObject.FindProperty("assetKey");
            if (prop.stringValue.Trim() == "")
            {
                prop.stringValue = GUID.Generate().ToString();
                serializedObject.ApplyModifiedProperties();
            }

            this.DrawDefaultInspector();

            DrawUILine();

            bool hasDuplicates = AnalyzeSetupGui();

            bool noAvatar = generator.avatar == null;
            bool noContainer = generator.assetContainer == null;
            bool noFX = false;
            if(!noAvatar)
                noFX = generator.avatar.baseAnimationLayers.First(it => it.type == VRCAvatarDescriptor.AnimLayerType.FX).animatorController == null;

            DrawUILine();

            if(noAvatar)
                EditorGUILayout.HelpBox("No avatar descriptor specified. Please provide an avatar descriptor.", MessageType.Error);

            if(noContainer)
                EditorGUILayout.HelpBox("No asset container. Please create a new, blank animator controller to contain system assets.", MessageType.Error);

            if(hasDuplicates)
                EditorGUILayout.HelpBox("Duplicate Parameters in setup. Please ensure there are no duplicates.", MessageType.Error);

            if(noFX)
                EditorGUILayout.HelpBox("No FX Layer. Please add an FX layer", MessageType.Error);

            using(new EditorGUI.DisabledScope(noAvatar || noContainer || hasDuplicates || noFX))
            {
                if (GUILayout.Button("Add Selected Features")){ AddFeatures(); }
            }
            if (GUILayout.Button("Remove FT System")){ RemoveFeatures(); }
            serializedObject.ApplyModifiedProperties();
        }

        // // //

        internal string SystemPrefix
        {
            get => SystemName + "/";
            private set {}
        }

        const string FaceTrackingToggleParameter = "FaceTracking";

        private void AddFeatures()
        {
            InitializeAAC();
            if(EditorUtility.DisplayDialog("VRCFTGenerator: Add","Generate Face Tracking?", "OK", "Cancel"))
            {
                CreateFTBlendshapeController();
                AssetDatabase.SaveAssets();
            }
        }

        private void RemoveFeatures()
        {
            InitializeAAC();
            if (EditorUtility.DisplayDialog("VRCFTGenerator: Remove", "Remove Face Tracking from Animator (and items selected for deletion)?", "OK", "Cancel"))
            {
                RemoveFTBlendshapeController();
                AssetDatabase.SaveAssets();
            }
        }

        private void RemoveFTBlendshapeController()
        {
            // Find FX Layer
            AnimatorController fxLayer = (AnimatorController) generator.avatar.baseAnimationLayers.First(it => it.type == VRCAvatarDescriptor.AnimLayerType.FX).animatorController;
            aac.RemoveAllMainLayers();

            foreach(var layer in fxLayer.layers)
            {
                string layerName = layer.name;
                string supportingLayerName = layerName.Replace(SystemName + "__", "");

                if(layerName.StartsWith(SystemName))
                {
                    aac.RemoveAllSupportingLayers(supportingLayerName);
                }
            }

            List<string> parameterNames = VRCFTValues.CombinedMapping.Keys.ToList();

            parameterNames  = parameterNames.Concat(VRCFTValues.BlendshapeMapping.Keys.ToList())
                                            .Concat(VRCFTValues.AveragedMapping.Keys.ToList())
                                            .ToList();

            AnimatorControllerParameter[] fxParameters = fxLayer.parameters;

            if (generator.manageExpressionParameters)
            {
                VRCExpressionParameters.Parameter[] expressionParameters = generator.avatar.expressionParameters.parameters;

                foreach (VRCExpressionParameters.Parameter expressionParameter in expressionParameters)
                {
                    if (expressionParameter.name.Contains(SystemPrefix) || expressionParameter.name == FaceTrackingToggleParameter)
                    {
                        VRCFTGenerator_ParameterUtility.RemoveParameter(expressionParameter.name, generator.avatar);
                    }

                    foreach (string p in parameterNames)
                    {
                        if (expressionParameter.name.Contains(p))
                        {
                            VRCFTGenerator_ParameterUtility.RemoveParameter(expressionParameter.name, generator.avatar);
                        }
                    }
                }
            }

            if(generator.removeAnimatorParams)
            {
                int i = 0;

                foreach (AnimatorControllerParameter animatorParameter in fxParameters)
                {
                    if (animatorParameter.name.Contains(SystemPrefix) || animatorParameter.name == FaceTrackingToggleParameter)
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

        private void CreateFTBlendshapeController()
        {
            var fx = aac.CreateMainFxLayer();

            Dictionary<string, VRCFTValues.ParamMode> SelectedParameters = new Dictionary<string, VRCFTValues.ParamMode>();
            List<string> selectedBinaryDecodeParams = new List<string>();
            List<string> selectedFloatDecodeParams = new List<string>();
            List<string> decodeParams = new List<string>();
            List<string> smoothingParams = new List<string>();
            List<string> directFloatParamsNoPrefix = new List<string>();

            int parameterMemoryCost = 1; // 1 for Face Tracking toggle

            var faceTrackingToggle = fx.BoolParameter(FaceTrackingToggleParameter);

            foreach (ParamSpecifier specification in generator.paramsToAnimate)
            {
                SelectedParameters.Add(Enum.GetName(typeof(VRCFTValues.ParameterName), specification.VRCFTParameter), specification.mode);
            }

            // Pre-Parse Selected Parameters
            foreach (string param in SelectedParameters.Keys.ToArray())
            {
                VRCFTValues.ParamMode mode = SelectedParameters[param];
                bool isParamFloat = mode == VRCFTValues.ParamMode.floatParam;
                bool isParamCombined = VRCFTValues.CombinedMapping.ContainsKey(param);

                int paramBits = (int)SelectedParameters[param];
                parameterMemoryCost += paramBits;

                if(isParamFloat)
                {
                    fx.FloatParameter(param);
                    if(isParamCombined)
                    {
                        selectedFloatDecodeParams.Add(param);
                    }
                    else
                    {
                        directFloatParamsNoPrefix.Add(param);
                    }
                } else {
                    for (int j = 0; j < paramBits; j++)
                    {
                        int binarySuffix = (int)Mathf.Pow(2, j);
                        if(generator.useImplicitParameterCasting)
                            fx.FloatParameter(param + binarySuffix.ToString());
                        else
                            fx.BoolParameter(param + binarySuffix.ToString());
                    }
                    
                    if(isParamCombined)
                    {
                        // Extra bit for negative flag bool
                        parameterMemoryCost++;

                        if(generator.useImplicitParameterCasting)
                            fx.FloatParameter(param + "Negative");
                        else
                            fx.BoolParameter(param + "Negative");
                    }

                    selectedBinaryDecodeParams.Add(param);
                }

                if (isParamCombined)
                {
                    smoothingParams.Add(VRCFTValues.CombinedMapping[param][0]);
                    smoothingParams.Add(VRCFTValues.CombinedMapping[param][1]);
                } else {
                    smoothingParams.Add(param);
                }
            }

            var parameterConstant1 = fx.FloatParameter(SystemPrefix + "Constant_1");
            fx.OverrideValue(parameterConstant1, 1f);
            
            decodeParams = selectedFloatDecodeParams.Concat(selectedBinaryDecodeParams).ToList();

            BlendTree combinedBlendTree = aac.NewBlendTreeAsRaw();
            BlendTree decodeBlendTreeToCombine = aac.NewBlendTreeAsRaw();
            BlendTree smoothingBlendTreeToCombine = aac.NewBlendTreeAsRaw();
            var combinedDisabledClip = aac.NewClip("FT_Off");
            var decodeDisabledClip = aac.NewClip("FT_Off_Decode");
            var smoothingDisabledClip = aac.NewClip("FT_Off_Smoothing");

            // Create Binary Cast/Decode Layers (only if needed)
            int decodeBlendtreeChildren = decodeParams.Count;
            if (decodeBlendtreeChildren > 0)
            {
                var binaryCastLayer = aac.CreateSupportingFxLayer("BinaryCast");
                var binaryCastState = binaryCastLayer.NewState("Cast");
                binaryCastState.TransitionsTo(binaryCastState).AfterAnimationIsAtLeastAtPercent(0f).WithTransitionToSelf().When(faceTrackingToggle.IsTrue()); // re-evaluate every frame

                var decodeLayer = aac.CreateSupportingFxLayer("Decode");
                var offState = decodeLayer.NewState("FaceTracking_Off").WithAnimation(decodeDisabledClip);
                var decodeState = decodeLayer.NewState("Decode");

                offState.TransitionsTo(decodeState).When(faceTrackingToggle.IsTrue());
                decodeState.TransitionsTo(offState).When(faceTrackingToggle.IsFalse());

                AacFlFloatParameter binarySumTopLevelNormalizerParam;

                if(generator.writeDefaults)
                {
                    binarySumTopLevelNormalizerParam = parameterConstant1;
                }
                else
                {
                    binarySumTopLevelNormalizerParam = fx.FloatParameter(SystemPrefix + "DecodeBlendTreeNormalizer");
                    fx.OverrideValue(binarySumTopLevelNormalizerParam, 1f/(float)decodeBlendtreeChildren);
                }

                List<ChildMotion> binarySumTopLevelChildMotions = new List<ChildMotion>();

                foreach (string param in selectedBinaryDecodeParams)
                {
                    int paramBits = (int)SelectedParameters[param];
                    bool isCombinedParam = VRCFTValues.CombinedMapping.ContainsKey(param);
                    string paramPositiveName = isCombinedParam ? VRCFTValues.CombinedMapping[param][0] : param;

                    var paramPositive = fx.FloatParameter(SystemPrefix + paramPositiveName);

                    AacFlClip[] zeroClips = new AacFlClip[paramBits];
                    AacFlClip[] oneClipsParam1 = new AacFlClip[paramBits];
                    AacFlClip[] oneClipsParam2 = new AacFlClip[paramBits];

                    BlendTree[] binarySumChildBlendTreesPositive = new BlendTree[paramBits];
                    BlendTree[] binarySumChildBlendTreesNegative = new BlendTree[paramBits];
                    AacFlFloatParameter[] binarySumChildBlendTreesBlendParams = new AacFlFloatParameter[paramBits];

                    for (int j = 0; j < paramBits; j++)
                    {
                        int binarySuffix = (int)Mathf.Pow(2, j);
                        float binaryContribution = 0.5f * Mathf.Pow(2, j+1) * 1f/(Mathf.Pow(2, (float)paramBits) - 1f);
                        string p = param + binarySuffix.ToString();

                        foreach(string keyframeParam in decodeParams)
                        {
                            var paramToAnimate = fx.FloatParameter(SystemPrefix + keyframeParam);

                            if(generator.writeDefaults && keyframeParam != param)
                                continue;

                            float oneClipVal = keyframeParam == param ? binaryContribution : 0f;
                            oneClipVal *= (generator.writeDefaults ? 1f : 1f*(float)decodeBlendtreeChildren);

                            bool keyframeParamIsCombined = VRCFTValues.CombinedMapping.ContainsKey(keyframeParam);
                            var keyframePositiveName = keyframeParamIsCombined ? VRCFTValues.CombinedMapping[keyframeParam][0] : keyframeParam;
                            var keyframeNegativeName = keyframeParamIsCombined ? VRCFTValues.CombinedMapping[keyframeParam][1] : keyframeParam;

                            var keyframePositive = fx.FloatParameter(SystemPrefix + keyframePositiveName);
                            var keyframeNegative = fx.FloatParameter(SystemPrefix + keyframeNegativeName);

                            var zeroClip = aac.NewClip(param + "_0_b" + binarySuffix);
                            var plusClip  = aac.NewClip(param + "_1_b" + binarySuffix);
                            var minusClip = aac.NewClip(param + "_-1_b" + binarySuffix);

                            // TongueSteps neutral is -1, not 0
                            if(keyframeParam == "TongueSteps")
                            {
                                zeroClip.Animating(clip => clip.AnimatesAnimator(keyframePositive).WithOneFrame(oneClipVal));
                                plusClip.Animating(clip => clip.AnimatesAnimator(keyframePositive).WithOneFrame(oneClipVal));
                                minusClip.Animating(clip => clip.AnimatesAnimator(keyframePositive).WithOneFrame(0f));

                                plusClip.Animating(clip => clip.AnimatesAnimator(keyframeNegative).WithOneFrame(oneClipVal));
                                zeroClip.Animating(clip => clip.AnimatesAnimator(keyframeNegative).WithOneFrame(0f));
                                minusClip.Animating(clip => clip.AnimatesAnimator(keyframeNegative).WithOneFrame(0f));

                                decodeDisabledClip.Animating(clip => clip.AnimatesAnimator(keyframePositive).WithOneFrame(0f));
                                combinedDisabledClip.Animating(clip => clip.AnimatesAnimator(keyframePositive).WithOneFrame(0f));
                                decodeDisabledClip.Animating(clip => clip.AnimatesAnimator(keyframeNegative).WithOneFrame(0f));
                                combinedDisabledClip.Animating(clip => clip.AnimatesAnimator(keyframeNegative).WithOneFrame(0f));
                            }
                            else
                            {
                                zeroClip.Animating(clip => clip.AnimatesAnimator(keyframePositive).WithOneFrame(0f));
                                plusClip.Animating(clip => clip.AnimatesAnimator(keyframePositive).WithOneFrame(oneClipVal));
                                minusClip.Animating(clip => clip.AnimatesAnimator(keyframePositive).WithOneFrame(0f));

                                decodeDisabledClip.Animating(clip => clip.AnimatesAnimator(keyframePositive).WithOneFrame(0f));
                                combinedDisabledClip.Animating(clip => clip.AnimatesAnimator(keyframePositive).WithOneFrame(0f));

                                if(keyframeParamIsCombined)
                                {
                                    zeroClip.Animating(clip => clip.AnimatesAnimator(keyframeNegative).WithOneFrame(0f));
                                    plusClip.Animating(clip => clip.AnimatesAnimator(keyframeNegative).WithOneFrame(0f));
                                    minusClip.Animating(clip => clip.AnimatesAnimator(keyframeNegative).WithOneFrame(oneClipVal));

                                    decodeDisabledClip.Animating(clip => clip.AnimatesAnimator(keyframeNegative).WithOneFrame(0f));
                                    combinedDisabledClip.Animating(clip => clip.AnimatesAnimator(keyframeNegative).WithOneFrame(0f));
                                }
                            }

                            zeroClips[j] = zeroClip;
                            oneClipsParam1[j] = plusClip;
                            oneClipsParam2[j] = minusClip;
                        }

                        string boolParamAsFloatName = generator.useImplicitParameterCasting ? param + binarySuffix.ToString() : SystemPrefix + param + binarySuffix.ToString() + "_Float";
                        var boolParamAsFloat = fx.FloatParameter(boolParamAsFloatName);

                        if(!generator.useImplicitParameterCasting)
                        {
                            var boolParam = fx.BoolParameter(param + binarySuffix.ToString());
                            binaryCastState.DrivingCasts(boolParam, 0f, 1f, boolParamAsFloat, 0f, 1f);
                        }

                        var positiveTree = CreateProxyTree(boolParamAsFloat, zeroClips[j], oneClipsParam1[j], param + $"_b{binarySuffix.ToString()}_Positive");
                        var negativeTree = CreateProxyTree(boolParamAsFloat, zeroClips[j], oneClipsParam2[j], param + $"_b{binarySuffix.ToString()}_Negative");

                        binarySumChildBlendTreesPositive[j] = positiveTree;
                        binarySumChildBlendTreesNegative[j] = negativeTree;
                        binarySumChildBlendTreesBlendParams[j] = parameterConstant1;
                    }
                    
                    BlendTree childMotion;
                    BlendTree parameterDirectBlendTreePositive = CreateDirectTree(decodeLayer, parameterConstant1, binarySumChildBlendTreesBlendParams, binarySumChildBlendTreesPositive, param + "_Binary_Positive");
                    BlendTree parameterDirectBlendTreeNegative = CreateDirectTree(decodeLayer, parameterConstant1, binarySumChildBlendTreesBlendParams, binarySumChildBlendTreesNegative, param + "_Binary_Negative");

                    if (isCombinedParam)
                    {
                        string boolParamNegativeAsFloatName = generator.useImplicitParameterCasting ? param : SystemPrefix + param + "_Float";
                        var boolParamNegativeAsFloat = fx.FloatParameter(boolParamNegativeAsFloatName + "Negative");

                        if(!generator.useImplicitParameterCasting)
                        {
                            var boolParamNegative = fx.BoolParameter(param + "Negative");
                            binaryCastState.DrivingCasts(boolParamNegative, 0f, 1f, boolParamNegativeAsFloat, 0f, 1f);
                        }

                        var combinedTree = CreateFactorTree(boolParamNegativeAsFloat, parameterDirectBlendTreePositive, parameterDirectBlendTreeNegative, param + "_Combined");
                        childMotion = combinedTree;
                    } else {
                        childMotion = parameterDirectBlendTreePositive;
                    }

                    binarySumTopLevelChildMotions.Add(new ChildMotion {motion = childMotion, directBlendParameter = binarySumTopLevelNormalizerParam.Name, timeScale = 1.0f, threshold = 0.0f});
                }

                foreach (string param in selectedFloatDecodeParams)
                {
                    var zeroClip  = aac.NewClip(param + "_0");
                    var oneClip   = aac.NewClip(param + "_1");
                    var minusClip = aac.NewClip(param + "_-1");

                    foreach(string keyframeParam in decodeParams)
                    {
                        var paramToAnimate = fx.FloatParameter(SystemPrefix + keyframeParam);

                        if(generator.writeDefaults && keyframeParam != param)
                            continue;

                        float oneClipVal = keyframeParam == param ? (generator.writeDefaults ? 1f : 1f*(float)decodeBlendtreeChildren) : 0f;

                        bool keyframeParamIsCombined = VRCFTValues.CombinedMapping.ContainsKey(keyframeParam);
                        var keyframePositiveName = keyframeParamIsCombined ? VRCFTValues.CombinedMapping[keyframeParam][0] : keyframeParam;
                        var keyframeNegativeName = keyframeParamIsCombined ? VRCFTValues.CombinedMapping[keyframeParam][1] : keyframeParam;

                        var keyframePositive = fx.FloatParameter(SystemPrefix + keyframePositiveName);
                        var keyframeNegative = fx.FloatParameter(SystemPrefix + keyframeNegativeName);

                        // TongueSteps neutral is -1, not 0
                        if(keyframeParam == "TongueSteps")
                        {
                            zeroClip.Animating(clip => clip.AnimatesAnimator(keyframePositive).WithOneFrame(oneClipVal));
                            oneClip.Animating(clip => clip.AnimatesAnimator(keyframePositive).WithOneFrame(oneClipVal));
                            minusClip.Animating(clip => clip.AnimatesAnimator(keyframePositive).WithOneFrame(0f));

                            oneClip.Animating(clip => clip.AnimatesAnimator(keyframeNegative).WithOneFrame(oneClipVal));
                            zeroClip.Animating(clip => clip.AnimatesAnimator(keyframeNegative).WithOneFrame(0f));
                            minusClip.Animating(clip => clip.AnimatesAnimator(keyframeNegative).WithOneFrame(0f));

                            decodeDisabledClip.Animating(clip => clip.AnimatesAnimator(keyframePositive).WithOneFrame(0f));
                            combinedDisabledClip.Animating(clip => clip.AnimatesAnimator(keyframePositive).WithOneFrame(0f));
                            decodeDisabledClip.Animating(clip => clip.AnimatesAnimator(keyframeNegative).WithOneFrame(0f));
                            combinedDisabledClip.Animating(clip => clip.AnimatesAnimator(keyframeNegative).WithOneFrame(0f));
                        }
                        else
                        {
                            zeroClip.Animating(clip  => clip.AnimatesAnimator(keyframePositive).WithOneFrame(0f));
                            oneClip.Animating(clip   => clip.AnimatesAnimator(keyframePositive).WithOneFrame(oneClipVal));
                            minusClip.Animating(clip => clip.AnimatesAnimator(keyframeNegative).WithOneFrame(oneClipVal));

                            decodeDisabledClip.Animating(clip => clip.AnimatesAnimator(keyframePositive).WithOneFrame(0f));
                            decodeDisabledClip.Animating(clip => clip.AnimatesAnimator(keyframeNegative).WithOneFrame(0f));
                            combinedDisabledClip.Animating(clip => clip.AnimatesAnimator(keyframePositive).WithOneFrame(0f));
                            combinedDisabledClip.Animating(clip => clip.AnimatesAnimator(keyframeNegative).WithOneFrame(0f));
                        }
                    }

                    BlendTree childMotion = CreateCombinedTree(fx.FloatParameter(param), minusClip, zeroClip, oneClip, param + "_Combined");
                    binarySumTopLevelChildMotions.Add(new ChildMotion {motion = childMotion, directBlendParameter = binarySumTopLevelNormalizerParam.Name, timeScale = 1.0f, threshold = 0.0f});
                }

                var binarySumTopLevelDirectBlendTree = CreateDirectTree(decodeLayer, binarySumTopLevelNormalizerParam, binarySumTopLevelChildMotions.ToArray(), "DecodeBlendTree");
                decodeState.WithAnimation(binarySumTopLevelDirectBlendTree);

                decodeBlendTreeToCombine = binarySumTopLevelDirectBlendTree;

                if(generator.useImplicitParameterCasting)
                {
                    aac.RemoveAllSupportingLayers("BinaryCast");
                }
            }
            

            // Smoothing/Driving Layer
            if(smoothingParams.Count > 0)
            {
                var smoothingLayer = aac.CreateSupportingFxLayer("SmoothAndDrive"); // Adds or Overwrites

                var smoothingFactorParameter = fx.FloatParameter(SystemPrefix + "SmoothingAlpha");
                fx.OverrideValue(smoothingFactorParameter, generator.remoteSmoothingTimeConstant);

                fx.OverrideValue(faceTrackingToggle, true);

                int numDirectStates = smoothingParams.Count;

                AacFlFloatParameter directNormalizer;

                if(generator.writeDefaults)
                {
                    directNormalizer = parameterConstant1;
                }
                else
                {
                    directNormalizer = fx.FloatParameter(SystemPrefix + "SmoothingDrivingTreeNormalizer");
                    float directNormalizerValue = generator.writeDefaults ? 1f : 1f/(float)numDirectStates;
                    fx.OverrideValue(directNormalizer, directNormalizerValue);
                }

                List<ChildMotion> smoothingChildMotions = new List<ChildMotion>();

                foreach (string parameterToSmooth in smoothingParams)
                {
                    string parameterNameToSmooth = parameterToSmooth;
                    if(!directFloatParamsNoPrefix.Contains(parameterToSmooth))
                    {
                        parameterNameToSmooth = SystemPrefix + parameterNameToSmooth;
                    }
                    var parameter = fx.FloatParameter(parameterNameToSmooth);
                    var smoothedParameter = fx.FloatParameter(SystemPrefix + parameterToSmooth + "_Smoothed");

                    var zeroClip = aac.NewClip(parameterToSmooth + "param_0_scaled");
                    var oneClip  = aac.NewClip(parameterToSmooth + "param_1_scaled");

                    foreach(string paramName in smoothingParams)
                    {
                        var paramNameSmoothed = fx.FloatParameter(SystemPrefix + paramName + "_Smoothed");

                        if(generator.writeDefaults && paramName != parameterToSmooth)
                            continue;

                        float driveVal = paramName == parameterToSmooth ? (generator.writeDefaults ? 1 : 1f*(float)numDirectStates) : 0f;
                        zeroClip.Animating(clip => clip.AnimatesAnimator(paramNameSmoothed).WithOneFrame(0f));
                        oneClip.Animating(clip => clip.AnimatesAnimator(paramNameSmoothed).WithOneFrame(driveVal));
                        
                        foreach (var target in generator.blendshapeTargetMeshRenderers)
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

                    var factorTree = CreateFactorTree(smoothingFactorParameter, CreateProxyTree(parameter, zeroClip, oneClip, parameter.Name + "_SmoothingA"), CreateSmoothingTree(smoothedParameter, zeroClip, oneClip, parameter.Name + "_SmoothingB"), parameter.Name + "_Smoothing");
                    smoothingChildMotions.Add(new ChildMotion {motion = factorTree, directBlendParameter = directNormalizer.Name, timeScale = 1.0f, threshold = 0.0f});
                }

                var smoothingDirectBlendTree = CreateDirectTree(smoothingLayer, directNormalizer, smoothingChildMotions.ToArray(), "Smoothing");

                var offState = smoothingLayer.NewState("FaceTracking_Off").WithAnimation(smoothingDisabledClip);
                var smoothingState = smoothingLayer.NewState("Smoothing").WithAnimation(smoothingDirectBlendTree);

                offState.TransitionsTo(smoothingState).When(faceTrackingToggle.IsTrue());
                smoothingState.TransitionsTo(offState).When(faceTrackingToggle.IsFalse());

                foreach (string paramName in smoothingParams)
                {
                    var smoothedParameter = fx.FloatParameter(SystemPrefix + paramName + "_Smoothed");
                    smoothingDisabledClip.Animating(clip => clip.AnimatesAnimator(smoothedParameter).WithOneFrame(0f));

                    foreach (var target in generator.blendshapeTargetMeshRenderers)
                    {
                        if(VRCFTValues.AveragedMapping.ContainsKey(paramName))
                        {
                            string blendshapeName1 = VRCFTValues.BlendshapeMapping[VRCFTValues.AveragedMapping[paramName][0]];
                            string blendshapeName2 = VRCFTValues.BlendshapeMapping[VRCFTValues.AveragedMapping[paramName][1]];
                            
                            smoothingDisabledClip.Animating(clip => {clip.Animates(target, "blendShape." + blendshapeName1).WithOneFrame(0f);});
                            smoothingDisabledClip.Animating(clip => {clip.Animates(target, "blendShape." + blendshapeName2).WithOneFrame(0f);});
                            combinedDisabledClip.Animating(clip => {clip.Animates(target, "blendShape." + blendshapeName1).WithOneFrame(0f);});
                            combinedDisabledClip.Animating(clip => {clip.Animates(target, "blendShape." + blendshapeName2).WithOneFrame(0f);});
                        } else {
                            smoothingDisabledClip.Animating(clip => {clip.Animates(target, "blendShape." + VRCFTValues.BlendshapeMapping[paramName]).WithOneFrame(0f);});
                            combinedDisabledClip.Animating(clip => {clip.Animates(target, "blendShape." + VRCFTValues.BlendshapeMapping[paramName]).WithOneFrame(0f);});
                        }
                    }
                }

                smoothingBlendTreeToCombine = smoothingDirectBlendTree;
            }

            if(generator.writeDefaults 
                && generator.singleLayer 
                && decodeBlendTreeToCombine != (BlendTree)null 
                && smoothingBlendTreeToCombine != (BlendTree)null)
            {
                var ftCombinedLayer = aac.CreateSupportingFxLayer("CombinedFaceTracking");

                AacFlFloatParameter[] blendParameters = {parameterConstant1, parameterConstant1};
                BlendTree[] combinedChildren = {decodeBlendTreeToCombine, smoothingBlendTreeToCombine};

                combinedBlendTree = CreateDirectTree(ftCombinedLayer, parameterConstant1, blendParameters, combinedChildren, "CombinedFaceTracking");

                var disabledState = ftCombinedLayer.NewState("Disabled").WithAnimation(combinedDisabledClip);
                var enabledState = ftCombinedLayer.NewState("Enabled").WithAnimation(combinedBlendTree);

                disabledState.TransitionsTo(enabledState).When(faceTrackingToggle.IsTrue());
                enabledState.TransitionsTo(disabledState).When(faceTrackingToggle.IsFalse());

                aac.RemoveAllSupportingLayers("Decode");
                aac.RemoveAllSupportingLayers("SmoothAndDrive");
            }

            aac.RemoveAllMainLayers();
            if (generator.manageExpressionParameters)
            {
                AddParametersToAvatar(generator.avatar, SelectedParameters);
            }
            
        }

        // // // // // // // // // // // // // // // // // // // // // // // // // // // 

        private void AddParametersToAvatar(VRCAvatarDescriptor avatar, Dictionary<string, VRCFTValues.ParamMode> parameters)
        {
            VRCFTGenerator_ParameterUtility.AddParameter(FaceTrackingToggleParameter, VRCExpressionParameters.ValueType.Bool, true, true, avatar);

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
            smoothingFactorParameterLayer.OverrideValue(smoothingFactorParameter, generator.remoteSmoothingTimeConstant); // Forces the value to init at remote value

            var smoothingParameterNameRemote = "FT_SmoothingAlpha_Remote";
            var smoothingParameterNameLocal = "FT_SmoothingAlpha_Local";

            var smoothingFactorParameterRemote = smoothingFactorParameterLayer.FloatParameter(smoothingParameterNameRemote);
            var smoothingFactorParameterLocal  = smoothingFactorParameterLayer.FloatParameter(smoothingParameterNameLocal);

            float tauRemote = generator.remoteSmoothingTimeConstant;
            float tauLocal  = generator.remoteSmoothingTimeConstant;// my.localSmoothingTimeConstant;

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

        string AssetName(string name)
        {
            return $"zAutogenerated__{generator.assetKey}__{name}";
        }

        private BlendTree CreateCombinedTree(AacFlFloatParameter manualControlParameter, AacFlClip minusClip, AacFlClip zeroClip, AacFlClip oneClip, string name = "")
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

            if(name != "")
                proxyTree.name = AssetName(name);

            return proxyTree;
        }

        private BlendTree CreateProxyTree(AacFlFloatParameter manualControlParameter, AacFlClip zeroClip, AacFlClip oneClip, string name = "")
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

            if(name != "")
                proxyTree.name = AssetName(name);

            return proxyTree;
        }

        private BlendTree CreateFactorTree(AacFlFloatParameter smoothingFactorParameter, BlendTree proxyTree, BlendTree smoothingTree, string name = "")
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

                if(name != "")
                    proxyTree.name = AssetName(name);

            }
            return factorTree;
        }

        private BlendTree CreateSmoothingTree(AacFlFloatParameter smoothedParameter, AacFlClip zeroClip, AacFlClip oneClip, string name = "")
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

            if(name != "")
                smoothingTree.name = AssetName(name);

            return smoothingTree;
        }

        private BlendTree CreateDirectTree(AacFlLayer layer, AacFlFloatParameter normalizingParameter, AacFlFloatParameter[] blendParameters, BlendTree[] children, string name = "", float minThreshold = 0f, float maxThreshold = 1f, bool useAutomaticThresholds = false)
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

            if(name != "")
                directTree.name = AssetName(name);

            for(int ch = 0; ch < children.Length; ch++)
            {
                childMotions[ch] = new ChildMotion {motion = children[ch], directBlendParameter = blendParameters[ch].Name, timeScale = 1.0f, threshold = 0.0f};
            }

            directTree.children = childMotions;

            return directTree;
        }

        private BlendTree CreateDirectTree(AacFlLayer layer, AacFlFloatParameter normalizingParameter, ChildMotion[] childMotions, string name = "", float minThreshold = 0f, float maxThreshold = 1f, bool useAutomaticThresholds = false)
        {
            var directTree = aac.NewBlendTreeAsRaw();
            directTree.blendType = BlendTreeType.Direct;
            directTree.blendParameter = normalizingParameter.Name;
            directTree.minThreshold = minThreshold;
            directTree.maxThreshold = maxThreshold;
            directTree.useAutomaticThresholds = useAutomaticThresholds;

            if(name != "")
                directTree.name = AssetName(name);
            
            directTree.children = childMotions;

            return directTree;
        }
    }
}
#endif
