using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace net.fushizen.avrc
{
    /**
     * Generates parameters from a AV3 menu
     */
    public class AvrcLinkSpecGenerator
    {
        private readonly AvrcLinkSpec _linkSpec;
        private VRCAvatarDescriptor _avatar;
        private bool _foldout;

        internal AvrcLinkSpecGenerator(
            AvrcLinkSpec linkSpec
        )
        {
            _linkSpec = linkSpec;
        }

        private Localizations L => Localizations.Inst;

        internal void GenerateParametersUI()
        {
            if (_linkSpec.sourceExpressionMenu == null) return;

            _foldout = EditorGUILayout.Foldout(_foldout, L.GP_FOLDOUT);
            if (!_foldout)
            {
                return;
            }

            _avatar = EditorGUILayout.ObjectField(L.GP_REF_AVATAR, _avatar, typeof(VRCAvatarDescriptor), true)
                as VRCAvatarDescriptor;

            if (_avatar != null)
            {
                if (_avatar.expressionParameters == null)
                {
                    EditorGUILayout.HelpBox(L.GP_ERR_NO_PARAMS, MessageType.Error);
                    return;
                }

                List<string> errors = new List<string>();
                var paramDict = GenerateParameters(errors, _linkSpec.sourceExpressionMenu,
                    _avatar.expressionParameters);

                if (errors.Count > 0)
                {
                    foreach (var error in errors)
                    {
                        EditorGUILayout.HelpBox(error, MessageType.Error);
                    }

                    return;
                }

                var alreadyAdded = _linkSpec.signals.Select(p => p.name).ToImmutableHashSet();
                var paramList = paramDict.Values.Where(p => !alreadyAdded.Contains(p.name)).ToList();

                if (paramList.Count == 0)
                {
                    EditorGUILayout.HelpBox(L.GP_ERR_NO_NEW_PARAMS, MessageType.Info);
                }
                else
                {
                    string msg = L.GP_FOUND_PARAMS + string.Join("\n", paramList.Select(p => p.name));
                    EditorGUILayout.HelpBox(msg, MessageType.Info);

                    if (GUILayout.Button(L.GP_ADD_PARAMS))
                    {
                        foreach (var avrcParameter in paramList)
                        {
                            _linkSpec.signals.Add(avrcParameter);
                        }

                        EditorUtility.SetDirty(_linkSpec);
                        AssetDatabase.SaveAssets();
                    }
                }
            }
        }

        private Dictionary<string, AvrcSignal> GenerateParameters(
            List<string> errors,
            VRCExpressionsMenu rootMenu,
            VRCExpressionParameters expressionParameters
        )
        {
            if (rootMenu == null || expressionParameters == null)
                return new Dictionary<string, AvrcSignal>();

            Dictionary<string, VRCExpressionParameters.Parameter> knownParameters =
                new Dictionary<string, VRCExpressionParameters.Parameter>();

            foreach (var param in expressionParameters.parameters)
            {
                if (knownParameters.ContainsKey(param.name))
                {
                    errors.Add(string.Format(L.GP_ERR_DUPLICATE, param.name));
                }
                else
                {
                    knownParameters[param.name] = param;
                }
            }

            var avrcParams =
                new Dictionary<string, AvrcSignal>();
            HashSet<VRCExpressionsMenu> enqueued = new HashSet<VRCExpressionsMenu>();
            Queue<VRCExpressionsMenu> toVisit = new Queue<VRCExpressionsMenu>();

            toVisit.Enqueue(rootMenu);
            enqueued.Add(rootMenu);

            while (toVisit.Count > 0)
            {
                var next = toVisit.Dequeue();

                foreach (var control in next.controls)
                {
                    switch (control.type)
                    {
                        case VRCExpressionsMenu.Control.ControlType.Button:
                        case VRCExpressionsMenu.Control.ControlType.Toggle:
                            RecordIntParam(errors, avrcParams, knownParameters, control.parameter, control.value);
                            break;
                        case VRCExpressionsMenu.Control.ControlType.RadialPuppet:
                        case VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet:
                        case VRCExpressionsMenu.Control.ControlType.FourAxisPuppet:
                            RecordIntParam(errors, avrcParams, knownParameters, control.parameter, control.value);
                            foreach (var subParameter in control.subParameters)
                            {
                                RecordFloatParam(errors, avrcParams, knownParameters, subParameter);
                            }

                            break;
                        case VRCExpressionsMenu.Control.ControlType.SubMenu:
                            RecordIntParam(errors, avrcParams, knownParameters, control.parameter, control.value);
                            if (control.subMenu != null && !enqueued.Contains(control.subMenu))
                            {
                                toVisit.Enqueue(control.subMenu);
                                enqueued.Add(control.subMenu);
                            }

                            break;
                    }
                }
            }

            return avrcParams;
        }

        [SuppressMessage("ReSharper", "UnusedParameter.Local")]
        private void RecordFloatParam(
            List<string> errors,
            Dictionary<string, AvrcSignal> avrcParams,
            Dictionary<string, VRCExpressionParameters.Parameter> knownParameters,
            VRCExpressionsMenu.Control.Parameter subParameter
        )
        {
            // unsupported
        }

        private void RecordIntParam(
            List<string> errors,
            Dictionary<string, AvrcSignal> avrcParams,
            Dictionary<string, VRCExpressionParameters.Parameter> knownParameters,
            VRCExpressionsMenu.Control.Parameter controlParameter,
            float controlValue
        )
        {
            if (string.IsNullOrEmpty(controlParameter.name)) return;

            if (!knownParameters.ContainsKey(controlParameter.name))
            {
                errors.Add(string.Format(L.GP_NOTDEF, controlParameter.name));
                return;
            }

            var ty = knownParameters[controlParameter.name].valueType;
            if (ty == VRCExpressionParameters.ValueType.Float)
            {
                errors.Add(string.Format(L.GP_ERR_PRIMARY_TYPE, controlParameter.name));
                return;
            }

            var avrcType = ty == VRCExpressionParameters.ValueType.Bool
                ? AvrcSignalType.Bool
                : AvrcSignalType.Int;

            int intVal = Mathf.RoundToInt(controlValue);

            if (!avrcParams.ContainsKey(controlParameter.name))
            {
                avrcParams[controlParameter.name] = new AvrcSignal
                {
                    name = controlParameter.name,
                    type = avrcType,
                    minVal = 0,
                    maxVal = intVal,
                };
            }
            else
            {
                var curParam = avrcParams[controlParameter.name];

                if (curParam.maxVal < intVal)
                {
                    curParam.maxVal = intVal;
                }

                avrcParams[controlParameter.name] = curParam;
            }
        }
    }
}