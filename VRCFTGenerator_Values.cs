#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using VRCFTGenerator.AnimatorAsCode.V0;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using ValueType = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.ValueType;
using UnityEditor;
using UnityEditor.Animations;

namespace Raz.VRCFTGenerator
{

    public class VRCFTValues : MonoBehaviour
    {
        public enum ParamMode
        {
            binary1 = 1,
            binary2 = 2,
            binary3 = 3,
            binary4 = 4,
            binary5 = 5,
            binary6 = 6,
            binary7 = 7,
            floatParam = 8,
        }

        public enum ParamTypes
        {
            FloatParam = 0,
            BoolParam = 1,
            IntParam = 2,
        }

        public static readonly Dictionary<string, string[]> CombinedMapping = new Dictionary<string, string[]> {
            {"JawX",                            new string[] {"JawRight", "JawLeft"}},
            {"MouthUpper",                      new string[] {"MouthUpperRight", "MouthUpperLeft"}},
            {"MouthLower",                      new string[] {"MouthLowerRight", "MouthLowerLeft"}},
            {"SmileSadRight",                   new string[] {"MouthSmileRight", "MouthSadRight"}},
            {"SmileSadLeft",                    new string[] {"MouthSmileLeft", "MouthSadLeft"}},
            {"TongueY",                         new string[] {"TongueUp", "TongueDown"}},
            {"TongueX",                         new string[] {"TongueRight", "TongueLeft"}},
            {"PuffSuckRight",                   new string[] {"CheekPuffRight", "CheekSuck"}},
            {"PuffSuckLeft",                    new string[] {"CheekPuffLeft", "CheekSuck"}},
            {"JawOpenApe",                      new string[] {"JawOpen", "MouthApeShape"}},
            {"JawOpenPuffRight",                new string[] {"JawOpen", "CheekPuffRight"}},
            {"JawOpenPuffLeft",                 new string[] {"JawOpen", "CheekPuffLeft"}},
            {"JawOpenSuck",                     new string[] {"JawOpen", "CheekSuck"}},
            {"JawOpenForward",                  new string[] {"JawOpen", "JawForward"}},
            {"MouthUpperUpRightUpperInside",    new string[] {"MouthUpperUpRight", "MouthUpperInside"}},
            {"MouthUpperUpRightPuffRight",      new string[] {"MouthUpperUpRight", "CheekPuffRight"}},
            {"MouthUpperUpRightApe",            new string[] {"MouthUpperUpRight", "MouthApeShape"}},
            {"MouthUpperUpRightPout",           new string[] {"MouthUpperUpRight", "MouthPout"}},
            {"MouthUpperUpRightOverlay",        new string[] {"MouthUpperUpRight", "MouthLowerOverlay"}},
            {"MouthUpperUpLeftUpperInside",     new string[] {"MouthUpperUpLeft", "MouthUpperInside"}},
            {"MouthUpperUpLeftPuffLeft",        new string[] {"MouthUpperUpLeft", "CheekPuffLeft"}},
            {"MouthUpperUpLeftApe",             new string[] {"MouthUpperUpLeft", "MouthApeShape"}},
            {"MouthUpperUpLeftPout",            new string[] {"MouthUpperUpLeft", "MouthPout"}},
            {"MouthUpperUpLeftOverlay",         new string[] {"MouthUpperUpLeft", "MouthLowerOverlay"}},
            {"MouthLowerDownRightLowerInside",  new string[] {"MouthLowerDownRight", "MouthLowerInside"}},
            {"MouthLowerDownRightPuffRight",    new string[] {"MouthLowerDownRight", "CheekPuffRight"}},
            {"MouthLowerDownRightApe",          new string[] {"MouthLowerDownRight", "MouthApeShape"}},
            {"MouthLowerDownRightPout",         new string[] {"MouthLowerDownRight", "MouthPout"}},
            {"MouthLowerDownRightOverlay",      new string[] {"MouthLowerDownRight", "MouthLowerOverlay"}},
            {"MouthLowerDownLeftLowerInside",   new string[] {"MouthLowerDownLeft", "MouthLowerInside"}},
            {"MouthLowerDownLeftPuffLeft",      new string[] {"MouthLowerDownLeft", "CheekPuffLeft"}},
            {"MouthLowerDownLeftApe",           new string[] {"MouthLowerDownLeft", "MouthApeShape"}},
            {"MouthLowerDownLeftPout",          new string[] {"MouthLowerDownLeft", "MouthPout"}},
            {"MouthLowerDownLeftOverlay",       new string[] {"MouthLowerDownLeft", "MouthLowerOverlay"}},
            {"SmileRightUpperOverturn",         new string[] {"MouthSmileRight", "MouthUpperOverturn"}},
            {"SmileRightLowerOverturn",         new string[] {"MouthSmileRight", "MouthLowerOverturn"}},
            {"SmileRightApe",                   new string[] {"MouthSmileRight", "MouthApeShape"}},
            {"SmileRightOverlay",               new string[] {"MouthSmileRight", "MouthLowerOverlay"}},
            {"SmileRightPout",                  new string[] {"MouthSmileRight", "MouthPout"}},
            {"SmileLeftUpperOverturn",          new string[] {"MouthSmileLeft", "MouthUpperOverturn"}},
            {"SmileLeftLowerOverturn",          new string[] {"MouthSmileLeft", "MouthLowerOverturn"}},
            {"SmileLeftApe",                    new string[] {"MouthSmileLeft", "MouthApeShape"}},
            {"SmileLeftOverlay",                new string[] {"MouthSmileLeft", "MouthLowerOverlay"}},
            {"SmileLeftPout",                   new string[] {"MouthSmileLeft", "MouthPout"}},
            {"PuffRightUpperOverturn",          new string[] {"CheekPuffRight", "MouthUpperOverturn"}},
            {"PuffRightLowerOverturn",          new string[] {"CheekPuffRight", "MouthLowerOverturn"}},
            {"PuffLeftUpperOverturn",           new string[] {"CheekPuffLeft", "MouthUpperOverturn"}},
            {"PuffLeftLowerOverturn",           new string[] {"CheekPuffLeft", "MouthLowerOverturn"}},
            {"TongueSteps",                     new string[] {"TongueLongStep1", "TongueLongStep2"}},

            {"MouthX",                          new string[] {"MouthRight", "MouthLeft"}},
            {"SmileSad",                        new string[] {"MouthSmile", "MouthSad"}},
            {"PuffSuck",                        new string[] {"CheekPuff", "CheekSuck"}},
            {"JawOpenPuff",                     new string[] {"JawOpen", "CheekPuff"}},
            {"MouthUpperUpUpperInside",         new string[] {"MouthUpperUp", "MouthUpperInside"}},
            {"MouthUpperUpInside",              new string[] {"MouthUpperUp", "MouthInside"}},
            {"MouthUpperUpPuff",                new string[] {"MouthUpperUp", "CheekPuff"}},
            {"MouthUpperUpPuffLeft",            new string[] {"MouthUpperUp", "CheekPuffLeft"}},
            {"MouthUpperUpPuffRight",           new string[] {"MouthUpperUp", "CheekPuffRight"}},
            {"MouthUpperUpApe",                 new string[] {"MouthUpperUp", "MouthApeShape"}},
            {"MouthUpperUpPout",                new string[] {"MouthUpperUp", "MouthPout"}},
            {"MouthUpperUpOverlay",             new string[] {"MouthUpperUp", "MouthLowerOverlay"}},
            {"MouthLowerDownLowerInside",       new string[] {"MouthLowerDown", "MouthLowerInside"}},
            {"MouthLowerDownInside",            new string[] {"MouthLowerDown", "MouthInside"}},
            {"MouthLowerDownPuff",              new string[] {"MouthLowerDown", "CheekPuff"}},
            {"MouthLowerDownPuffLeft",          new string[] {"MouthLowerDown", "CheekPuffLeft"}},
            {"MouthLowerDownPuffRight",         new string[] {"MouthLowerDown", "CheekPuffRight"}},
            {"MouthLowerDownApe",               new string[] {"MouthLowerDown", "MouthApeShape"}},
            {"MouthLowerDownPout",              new string[] {"MouthLowerDown", "MouthPout"}},
            {"MouthLowerDownOverlay",           new string[] {"MouthLowerDown", "MouthLowerOverlay"}},
            {"SmileRightOverturn",              new string[] {"MouthSmileRight", "MouthOverturn"}},
            {"SmileLeftOverturn",               new string[] {"MouthSmileLeft", "MouthOverturn"}},
            {"SmileUpperOverturn",              new string[] {"MouthSmile", "MouthUpperOverturn"}},
            {"SmileLowerOverturn",              new string[] {"MouthSmile", "MouthLowerOverturn"}},
            // {"SmileOverturn",                   new string[] {"MouthSmile", "MouthOverturn"}},
            {"SmileApe",                        new string[] {"MouthSmile", "MouthApeShape"}},
            {"SmileOverlay",                    new string[] {"MouthSmile", "MouthLowerOverlay"}},
            {"SmilePout",                       new string[] {"MouthSmile", "MouthPout"}},
            {"PuffRightOverturn",               new string[] {"CheekPuffRight", "MouthOverturn"}},
            {"PuffLeftOverturn",                new string[] {"CheekPuffLeft", "MouthOverturn"}},
            {"PuffUpperOverturn",               new string[] {"CheekPuff", "MouthUpperOverturn"}},
            {"PuffLowerOverturn",               new string[] {"CheekPuff", "MouthLowerOverturn"}},
            {"PuffOverturn",                    new string[] {"CheekPuff", "MouthOverturn"}},
        };

        public static readonly Dictionary<string, string> BlendshapeMapping = new Dictionary<string, string> {
            {"CheekPuffLeft",       "Cheek_Puff_Left"},
            {"CheekPuffRight",      "Cheek_Puff_Right"},
            {"CheekSuck",           "Cheek_Suck"},
            {"JawForward",          "Jaw_Forward"},
            {"JawLeft",             "Jaw_Left"},
            {"JawOpen",             "Jaw_Open"},
            {"JawRight",            "Jaw_Right"},
            {"MouthApeShape",       "Mouth_Ape_Shape"},
            {"MouthLowerDownLeft",  "Mouth_Lower_DownLeft"},
            {"MouthLowerDownRight", "Mouth_Lower_DownRight"},
            {"MouthLowerInside",    "Mouth_Lower_Inside"},
            {"MouthLowerLeft",      "Mouth_Lower_Left"},
            {"MouthLowerOverlay",   "Mouth_Lower_Overlay"},
            {"MouthLowerOverturn",  "Mouth_Lower_Overturn"},
            {"MouthLowerRight",     "Mouth_Lower_Right"},
            {"MouthPout",           "Mouth_Pout"},
            {"MouthSadLeft",        "Mouth_Sad_Left"},
            {"MouthSadRight",       "Mouth_Sad_Right"},
            {"MouthSmileLeft",      "Mouth_Smile_Left"},
            {"MouthSmileRight",     "Mouth_Smile_Right"},
            {"MouthUpperInside",    "Mouth_Upper_Inside"},
            {"MouthUpperLeft",      "Mouth_Upper_Left"},
            {"MouthUpperOverturn",  "Mouth_Upper_Overturn"},
            {"MouthUpperRight",     "Mouth_Upper_Right"},
            {"MouthUpperUpLeft",    "Mouth_Upper_UpLeft"},
            {"MouthUpperUpRight",   "Mouth_Upper_UpRight"},
            {"TongueDown",          "Tongue_Down"},
            {"TongueDownLeftMorph", "Tongue_DownLeft_Morph"},
            {"TongueDownRightMorph","Tongue_DownRight_Morph"},
            {"TongueLeft",          "Tongue_Left"},
            {"TongueLongStep1",     "Tongue_LongStep1"},
            {"TongueLongStep2",     "Tongue_LongStep2"},
            {"TongueRight",         "Tongue_Right"},
            {"TongueRoll",          "Tongue_Roll"},
            {"TongueUp",            "Tongue_Up"},
            {"TongueUpLeftMorph",   "Tongue_UpLeft_Morph"},
            {"TongueUpRightMorph",  "Tongue_UpRight_Morph"},
        };
        
        public static readonly Dictionary<string, string[]> AveragedMapping = new Dictionary<string, string[]> 
        {
            {"MouthLeft",       new string[] {"MouthUpperLeft", "MouthLowerLeft"}},
            {"MouthRight",      new string[] {"MouthUpperRight", "MouthLowerRight"}},
            {"CheekPuff",       new string[] {"CheekPuffRight", "CheekPuffLeft"}},
            {"MouthUpperUp",    new string[] {"MouthUpperUpLeft", "MouthUpperUpRight"}},
            {"MouthLowerDown",  new string[] {"MouthLowerDownLeft", "MouthLowerDownRight"}},
            {"MouthInside",     new string[] {"MouthUpperInside", "MouthLowerInside"}},
            {"MouthSmile",      new string[] {"MouthSmileRight", "MouthSmileLeft"}},
            {"MouthSad",        new string[] {"MouthSadLeft", "MouthSadRight"}},
        };

        public enum ParameterName
        {
            [InspectorName("Face/Direct/CheekPuffLeft")]
            CheekPuffLeft,
            [InspectorName("Face/Direct/CheekPuffRight")]
            CheekPuffRight,
            [InspectorName("Face/Direct/CheekSuck")]
            CheekSuck,
            [InspectorName("Face/Direct/JawForward")]
            JawForward,
            [InspectorName("Face/Direct/JawLeft")]
            JawLeft,
            [InspectorName("Face/Direct/JawOpen")]
            JawOpen,
            [InspectorName("Face/Direct/JawRight")]
            JawRight,
            [InspectorName("Face/Direct/MouthApeShape")]
            MouthApeShape,
            [InspectorName("Face/Direct/MouthLowerDownLeft")]
            MouthLowerDownLeft,
            [InspectorName("Face/Direct/MouthLowerDownRight")]
            MouthLowerDownRight,
            [InspectorName("Face/Direct/MouthLowerInside")]
            MouthLowerInside,
            [InspectorName("Face/Direct/MouthLowerLeft")]
            MouthLowerLeft,
            [InspectorName("Face/Direct/MouthLowerOverlay")]
            MouthLowerOverlay,
            [InspectorName("Face/Direct/MouthLowerOverturn")]
            MouthLowerOverturn,
            [InspectorName("Face/Direct/MouthLowerRight")]
            MouthLowerRight,
            [InspectorName("Face/Direct/MouthPout")]
            MouthPout,
            [InspectorName("Face/Direct/MouthSadLeft")]
            MouthSadLeft,
            [InspectorName("Face/Direct/MouthSadRight")]
            MouthSadRight,
            [InspectorName("Face/Direct/MouthSmileLeft")]
            MouthSmileLeft,
            [InspectorName("Face/Direct/MouthSmileRight")]
            MouthSmileRight,
            [InspectorName("Face/Direct/MouthUpperInside")]
            MouthUpperInside,
            [InspectorName("Face/Direct/MouthUpperLeft")]
            MouthUpperLeft,
            [InspectorName("Face/Direct/MouthUpperOverturn")]
            MouthUpperOverturn,
            [InspectorName("Face/Direct/MouthUpperRight")]
            MouthUpperRight,
            [InspectorName("Face/Direct/MouthUpperUpLeft")]
            MouthUpperUpLeft,
            [InspectorName("Face/Direct/MouthUpperUpRight")]
            MouthUpperUpRight,
            [InspectorName("Face/Direct/TongueDown")]
            TongueDown,
            [InspectorName("Face/Direct/TongueDownLeftMorph")]
            TongueDownLeftMorph,
            [InspectorName("Face/Direct/TongueDownRightMorph")]
            TongueDownRightMorph,
            [InspectorName("Face/Direct/TongueLeft")]
            TongueLeft,
            [InspectorName("Face/Direct/TongueLongStep1")]
            TongueLongStep1,
            [InspectorName("Face/Direct/TongueLongStep2")]
            TongueLongStep2,
            [InspectorName("Face/Direct/TongueRight")]
            TongueRight,
            [InspectorName("Face/Direct/TongueRoll")]
            TongueRoll,
            [InspectorName("Face/Direct/TongueUp")]
            TongueUp,
            [InspectorName("Face/Direct/TongueUpLeftMorph")]
            TongueUpLeftMorph,
            [InspectorName("Face/Direct/TongueUpRightMorph")]
            TongueUpRightMorph,
            [InspectorName("Face/Averaged/CheekPuff")]
            CheekPuff,
            [InspectorName("Face/Averaged/MouthInside")]
            MouthInside,
            [InspectorName("Face/Averaged/MouthLeft")]
            MouthLeft,
            [InspectorName("Face/Averaged/MouthLowerDown")]
            MouthLowerDown,
            [InspectorName("Face/Averaged/MouthRight")]
            MouthRight,
            [InspectorName("Face/Averaged/MouthSad")]
            MouthSad,
            [InspectorName("Face/Averaged/MouthSmile")]
            MouthSmile,
            [InspectorName("Face/Averaged/MouthUpperUp")]
            MouthUpperUp,
            [InspectorName("Face/Combined/General/JawX")]
            JawX,
            [InspectorName("Face/Combined/General/MouthUpper")]
            MouthUpper,
            [InspectorName("Face/Combined/General/MouthLower")]
            MouthLower,
            [InspectorName("Face/Combined/General/MouthX")]
            MouthX,
            [InspectorName("Face/Combined/General/SmileSadRight")]
            SmileSadRight,
            [InspectorName("Face/Combined/General/SmileSadLeft")]
            SmileSadLeft,
            [InspectorName("Face/Combined/General/SmileSad")]
            SmileSad,
            [InspectorName("Face/Combined/General/TongueY")]
            TongueY,
            [InspectorName("Face/Combined/General/TongueX")]
            TongueX,
            [InspectorName("Face/Combined/General/TongueSteps")]
            TongueSteps,
            [InspectorName("Face/Combined/General/PuffSuckRight")]
            PuffSuckRight,
            [InspectorName("Face/Combined/General/PuffSuckLeft")]
            PuffSuckLeft,
            [InspectorName("Face/Combined/General/PuffSuck")]
            PuffSuck,
            [InspectorName("Face/Combined/JawOpen/JawOpenApe")]
            JawOpenApe,
            [InspectorName("Face/Combined/JawOpen/JawOpenPuff")]
            JawOpenPuff,
            [InspectorName("Face/Combined/JawOpen/JawOpenPuffRight")]
            JawOpenPuffRight,
            [InspectorName("Face/Combined/JawOpen/JawOpenPuffLeft")]
            JawOpenPuffLeft,
            [InspectorName("Face/Combined/JawOpen/JawOpenSuck")]
            JawOpenSuck,
            [InspectorName("Face/Combined/JawOpen/JawOpenForward")]
            JawOpenForward,
            [InspectorName("Face/Combined/MouthUpper/MouthUpperUpRightUpperInside")]
            MouthUpperUpRightUpperInside,
            [InspectorName("Face/Combined/MouthUpper/MouthUpperUpRightPuffRight")]
            MouthUpperUpRightPuffRight,
            [InspectorName("Face/Combined/MouthUpper/MouthUpperUpRightApe")]
            MouthUpperUpRightApe,
            [InspectorName("Face/Combined/MouthUpper/MouthUpperUpRightPout")]
            MouthUpperUpRightPout,
            [InspectorName("Face/Combined/MouthUpper/MouthUpperUpRightOverlay")]
            MouthUpperUpRightOverlay,
            [InspectorName("Face/Combined/MouthUpper/MouthUpperUpLeftUpperInside")]
            MouthUpperUpLeftUpperInside,
            [InspectorName("Face/Combined/MouthUpper/MouthUpperUpLeftPuffLeft")]
            MouthUpperUpLeftPuffLeft,
            [InspectorName("Face/Combined/MouthUpper/MouthUpperUpLeftApe")]
            MouthUpperUpLeftApe,
            [InspectorName("Face/Combined/MouthUpper/MouthUpperUpLeftPout")]
            MouthUpperUpLeftPout,
            [InspectorName("Face/Combined/MouthUpper/MouthUpperUpLeftOverlay")]
            MouthUpperUpLeftOverlay,
            [InspectorName("Face/Combined/MouthUpper/MouthUpperUpUpperInside")]
            MouthUpperUpUpperInside,
            [InspectorName("Face/Combined/MouthUpper/MouthUpperUpInside")]
            MouthUpperUpInside,
            [InspectorName("Face/Combined/MouthUpper/MouthUpperUpPuff")]
            MouthUpperUpPuff,
            [InspectorName("Face/Combined/MouthUpper/MouthUpperUpPuffLeft")]
            MouthUpperUpPuffLeft,
            [InspectorName("Face/Combined/MouthUpper/MouthUpperUpPuffRight")]
            MouthUpperUpPuffRight,
            [InspectorName("Face/Combined/MouthUpper/MouthUpperUpApe")]
            MouthUpperUpApe,
            [InspectorName("Face/Combined/MouthUpper/MouthUpperUpPout")]
            MouthUpperUpPout,
            [InspectorName("Face/Combined/MouthUpper/MouthUpperUpOverlay")]
            MouthUpperUpOverlay,
            [InspectorName("Face/Combined/MouthLower/MouthLowerDownRightLowerInside")]
            MouthLowerDownRightLowerInside,
            [InspectorName("Face/Combined/MouthLower/MouthLowerDownRightPuffRight")]
            MouthLowerDownRightPuffRight,
            [InspectorName("Face/Combined/MouthLower/MouthLowerDownRightApe")]
            MouthLowerDownRightApe,
            [InspectorName("Face/Combined/MouthLower/MouthLowerDownRightPout")]
            MouthLowerDownRightPout,
            [InspectorName("Face/Combined/MouthLower/MouthLowerDownRightOverlay")]
            MouthLowerDownRightOverlay,
            [InspectorName("Face/Combined/MouthLower/MouthLowerDownLeftLowerInside")]
            MouthLowerDownLeftLowerInside,
            [InspectorName("Face/Combined/MouthLower/MouthLowerDownLeftPuffLeft")]
            MouthLowerDownLeftPuffLeft,
            [InspectorName("Face/Combined/MouthLower/MouthLowerDownLeftApe")]
            MouthLowerDownLeftApe,
            [InspectorName("Face/Combined/MouthLower/MouthLowerDownLeftPout")]
            MouthLowerDownLeftPout,
            [InspectorName("Face/Combined/MouthLower/MouthLowerDownLeftOverlay")]
            MouthLowerDownLeftOverlay,
            [InspectorName("Face/Combined/MouthLower/MouthLowerDownLowerInside")]
            MouthLowerDownLowerInside,
            [InspectorName("Face/Combined/MouthLower/MouthLowerDownInside")]
            MouthLowerDownInside,
            [InspectorName("Face/Combined/MouthLower/MouthLowerDownPuff")]
            MouthLowerDownPuff,
            [InspectorName("Face/Combined/MouthLower/MouthLowerDownPuffLeft")]
            MouthLowerDownPuffLeft,
            [InspectorName("Face/Combined/MouthLower/MouthLowerDownPuffRight")]
            MouthLowerDownPuffRight,
            [InspectorName("Face/Combined/MouthLower/MouthLowerDownApe")]
            MouthLowerDownApe,
            [InspectorName("Face/Combined/MouthLower/MouthLowerDownPout")]
            MouthLowerDownPout,
            [InspectorName("Face/Combined/MouthLower/MouthLowerDownOverlay")]
            MouthLowerDownOverlay,
            [InspectorName("Face/Combined/Smile/SmileRightUpperOverturn")]
            SmileRightUpperOverturn,
            [InspectorName("Face/Combined/Smile/SmileRightLowerOverturn")]
            SmileRightLowerOverturn,
            [InspectorName("Face/Combined/Smile/SmileRightOverturn")]
            SmileRightOverturn,
            [InspectorName("Face/Combined/Smile/SmileRightApe")]
            SmileRightApe,
            [InspectorName("Face/Combined/Smile/SmileRightOverlay")]
            SmileRightOverlay,
            [InspectorName("Face/Combined/Smile/SmileRightPout")]
            SmileRightPout,
            [InspectorName("Face/Combined/Smile/SmileLeftUpperOverturn")]
            SmileLeftUpperOverturn,
            [InspectorName("Face/Combined/Smile/SmileLeftLowerOverturn")]
            SmileLeftLowerOverturn,
            [InspectorName("Face/Combined/Smile/SmileLeftOverturn")]
            SmileLeftOverturn,
            [InspectorName("Face/Combined/Smile/SmileLeftApe")]
            SmileLeftApe,
            [InspectorName("Face/Combined/Smile/SmileLeftOverlay")]
            SmileLeftOverlay,
            [InspectorName("Face/Combined/Smile/SmileLeftPout")]
            SmileLeftPout,
            [InspectorName("Face/Combined/Smile/SmileUpperOverturn")]
            SmileUpperOverturn,
            [InspectorName("Face/Combined/Smile/SmileLowerOverturn")]
            SmileLowerOverturn,
            [InspectorName("Face/Combined/Smile/SmileApe")]
            SmileApe,
            [InspectorName("Face/Combined/Smile/SmileOverlay")]
            SmileOverlay,
            [InspectorName("Face/Combined/Smile/SmilePout")]
            SmilePout,
            [InspectorName("Face/Combined/CheekPuff/PuffRightUpperOverturn")]
            PuffRightUpperOverturn,
            [InspectorName("Face/Combined/CheekPuff/PuffRightLowerOverturn")]
            PuffRightLowerOverturn,
            [InspectorName("Face/Combined/CheekPuff/PuffRightOverturn")]
            PuffRightOverturn,
            [InspectorName("Face/Combined/CheekPuff/PuffLeftUpperOverturn")]
            PuffLeftUpperOverturn,
            [InspectorName("Face/Combined/CheekPuff/PuffLeftLowerOverturn")]
            PuffLeftLowerOverturn,
            [InspectorName("Face/Combined/CheekPuff/PuffLeftOverturn")]
            PuffLeftOverturn,
            [InspectorName("Face/Combined/CheekPuff/PuffUpperOverturn")]
            PuffUpperOverturn,
            [InspectorName("Face/Combined/CheekPuff/PuffLowerOverturn")]
            PuffLowerOverturn,
            [InspectorName("Face/Combined/CheekPuff/PuffOverturn")]
            PuffOverturn,
        };
    }
}

#endif