// Small Helper Class for VRC Parameters

// SPDX-License-Identifier: MIT
// Copyright 2022 Razgriz/Cam

// Cam — 08/02/2022
// it took me like 5 minutes
// i dont even want credit its w/e
// just yeet it wherever you want
#if UNITY_EDITOR
#if VRC_SDK_VRCSDK3

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
using VRCFTGenerator.AnimatorAsCode.V0;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;


namespace Raz.VRCFTGenerator
{
    public static class VRCFTGenerator_ParameterUtility
    {
        public static bool ParameterExists(VRCExpressionParameters.Parameter parameter, VRCAvatarDescriptor descriptor, bool matchType = true)
        {
            VRCExpressionParameters.Parameter found = descriptor.expressionParameters.FindParameter(parameter.name);
            if (found == null)
                return false;

            return matchType ? found.valueType == parameter.valueType : true;
        }

        public static bool ParameterExists(string parameterName, VRCAvatarDescriptor descriptor)
        {
            return descriptor.expressionParameters.FindParameter(parameterName) != null;
        }

        public static VRCExpressionParameters.Parameter GetParameterIfExists(VRCExpressionParameters.Parameter parameter, VRCAvatarDescriptor descriptor, bool matchType = true)
        {
            VRCExpressionParameters.Parameter found = descriptor.expressionParameters.FindParameter(parameter.name);
            if (found == null)
                return null;

            if (matchType && found.valueType == parameter.valueType)
                return found;

            return null;
        }

        public static VRCExpressionParameters.Parameter GetParameterIfExists(string parameterName, VRCAvatarDescriptor descriptor)
        {
            return descriptor.expressionParameters.FindParameter(parameterName);
        }

        public static void SetDefaultParameterValue(string parameterName, float defaultFloat, VRCAvatarDescriptor descriptor)
        {
            if (descriptor.expressionParameters.FindParameter(parameterName) == null)
                return;

            VRCExpressionParameters.Parameter[] parameters = descriptor.expressionParameters.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].name.Equals(parameterName))
                {
                    parameters[i].defaultValue = Mathf.Clamp(defaultFloat, -1f, 1f);
                    break;
                }
            }

            EditorUtility.SetDirty(descriptor.expressionParameters);
            descriptor.expressionParameters.parameters = parameters;
        }

        public static void SetDefaultParameterValue(string parameterName, int defaultInt, VRCAvatarDescriptor descriptor)
        {
            if (descriptor.expressionParameters.FindParameter(parameterName) == null)
                return;

            VRCExpressionParameters.Parameter[] parameters = descriptor.expressionParameters.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].name.Equals(parameterName)) {
                    parameters[i].defaultValue = Mathf.Clamp(defaultInt, 0, 255);
                    break;
                }
            }

            EditorUtility.SetDirty(descriptor.expressionParameters);
            descriptor.expressionParameters.parameters = parameters;
        }

        public static void SetDefaultParameterValue(string parameterName, bool defaultBool, VRCAvatarDescriptor descriptor)
        {
            if (descriptor.expressionParameters.FindParameter(parameterName) == null)
                return;

            VRCExpressionParameters.Parameter[] parameters = descriptor.expressionParameters.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].name.Equals(parameterName))
                {
                    parameters[i].defaultValue = defaultBool ? 1f : 0f;
                    break;
                }
            }

            EditorUtility.SetDirty(descriptor.expressionParameters);
            descriptor.expressionParameters.parameters = parameters;
        }

        public static void ChangeParameterType(string parameterName, VRCExpressionParameters.ValueType newType, VRCAvatarDescriptor descriptor)
        {
            VRCExpressionParameters.Parameter parameter = descriptor.expressionParameters.FindParameter(parameterName);
            if (parameter == null)
                return;

            VRCExpressionParameters.Parameter[] parameters = descriptor.expressionParameters.parameters;
            for (int i = 0; i < parameters.Length; i++) {
                if (parameters[i].name.Equals(parameterName)) {
                    parameters[i].valueType = newType;
                    break;
                }
            }

            EditorUtility.SetDirty(descriptor.expressionParameters);
            descriptor.expressionParameters.parameters = parameters;
        }

        public static void SetSaved(string parameterName, bool saved, VRCAvatarDescriptor descriptor)
        {
            if (descriptor.expressionParameters.FindParameter(parameterName) == null)
                return;

            VRCExpressionParameters.Parameter[] parameters = descriptor.expressionParameters.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].name.Equals(parameterName))
                {
                    parameters[i].saved = saved;
                    break;
                }
            }

            EditorUtility.SetDirty(descriptor.expressionParameters);
            descriptor.expressionParameters.parameters = parameters;
        }

        public static void RemoveParameter(string parameterName, VRCAvatarDescriptor descriptor)
        {
            if (descriptor.expressionParameters.FindParameter(parameterName) == null)
                return;

            EditorUtility.SetDirty(descriptor.expressionParameters);
            descriptor.expressionParameters.parameters = descriptor.expressionParameters.parameters
                .Where(p => !p.name.Equals(parameterName))
                .ToArray();
        }

        public static void AddParameter(string parameterName, VRCExpressionParameters.ValueType valueType, int defaultInt, bool saved, VRCAvatarDescriptor descriptor)
        {
            VRCExpressionParameters.Parameter newParameter = new VRCExpressionParameters.Parameter() {
                name = parameterName,
                defaultValue = Mathf.Clamp(defaultInt, 0, 255),
                saved = saved,
                valueType = valueType
            };

            EditorUtility.SetDirty(descriptor.expressionParameters);
            descriptor.expressionParameters.parameters = descriptor.expressionParameters.parameters
                .Append(newParameter)
                .ToArray();
        }

        public static void AddParameter(string parameterName, VRCExpressionParameters.ValueType valueType, float defaultFloat, bool saved, VRCAvatarDescriptor descriptor)
        {
            VRCExpressionParameters.Parameter newParameter = new VRCExpressionParameters.Parameter()
            {
                name = parameterName,
                defaultValue = Mathf.Clamp(defaultFloat, -1f, 1f),
                saved = saved,
                valueType = valueType
            };

            EditorUtility.SetDirty(descriptor.expressionParameters);
            descriptor.expressionParameters.parameters = descriptor.expressionParameters.parameters
                .Append(newParameter)
                .ToArray();
        }

        public static void AddParameter(string parameterName, VRCExpressionParameters.ValueType valueType, bool defaultBool, bool saved, VRCAvatarDescriptor descriptor)
        {
            VRCExpressionParameters.Parameter newParameter = new VRCExpressionParameters.Parameter()
            {
                name = parameterName,
                defaultValue = defaultBool ? 1f : 0f,
                saved = saved,
                valueType = valueType
            };

            EditorUtility.SetDirty(descriptor.expressionParameters);
            descriptor.expressionParameters.parameters = descriptor.expressionParameters.parameters
                .Append(newParameter)
                .ToArray();
        }
    }
}
#endif
#endif
