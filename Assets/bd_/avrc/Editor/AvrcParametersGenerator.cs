using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEditor;
using VRC.SDK3.Avatars.ScriptableObjects;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace net.fushizen.avrc
{
    using AvrcParameter = AvrcParameters.AvrcParameter;
    
    /**
     * Generates parameters from a AV3 menu
     */
    public class AvrcParametersGenerator
    {
        private AvrcParameters _parameters;
        private VRCAvatarDescriptor _avatar;
        private bool _foldout;
        
        private Localizations L => Localizations.Inst;

        internal AvrcParametersGenerator(
            AvrcParameters parameters
        )
        {
            _parameters = parameters;
        }

        internal void GenerateParametersUI()
        {
            if (_parameters.sourceExpressionMenu == null) return;

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
                var paramDict = GenerateParameters(errors, _parameters.sourceExpressionMenu,
                    _avatar.expressionParameters);

                if (errors.Count > 0)
                {
                    foreach (var error in errors)
                    {
                        EditorGUILayout.HelpBox(error, MessageType.Error);
                    }

                    return;
                }

                var alreadyAdded = _parameters.avrcParams.Select(p => p.name).ToImmutableHashSet();
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
                            _parameters.avrcParams.Add(avrcParameter);
                        }
                    }
                }
            }
        }
        
        internal Dictionary<string, AvrcParameter> GenerateParameters(
            List<string> errors,
            VRCExpressionsMenu rootMenu, 
            VRCExpressionParameters expressionParameters
            )
        {
            if (rootMenu == null || expressionParameters == null) return new Dictionary<string, AvrcParameter>();

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

            Dictionary<string, AvrcParameter> avrcParams = new Dictionary<string, AvrcParameter>();
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

        private void RecordFloatParam(
            List<string> errors,
            Dictionary<string, AvrcParameter> avrcParams,
            Dictionary<string, VRCExpressionParameters.Parameter> knownParameters, 
            VRCExpressionsMenu.Control.Parameter subParameter
            )
        {
            if (string.IsNullOrEmpty(subParameter.name))
            {
                return;
            }
            
            if (!knownParameters.ContainsKey(subParameter.name))
            {
                errors.Add(string.Format(L.GP_NOTDEF, subParameter.name));
                return;
            }
            
            var ty = knownParameters[subParameter.name].valueType;
            if (ty != VRCExpressionParameters.ValueType.Float)
            {
                errors.Add(string.Format(L.GP_ERR_PUPPET_TYPE, subParameter.name));
                return;
            }
            
            if (avrcParams.ContainsKey(subParameter.name))
            {
                return;
            }

            avrcParams[subParameter.name] = new AvrcParameter
            {
                name = subParameter.name,
                type = AvrcParameters.AvrcParameterType.Float,
            };
        }

        private void RecordIntParam(
            List<string> errors, 
            Dictionary<string, AvrcParameter> avrcParams,
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
                ? AvrcParameters.AvrcParameterType.Bool
                : AvrcParameters.AvrcParameterType.Int;

            int intVal = Mathf.RoundToInt(controlValue);
            
            if (!avrcParams.ContainsKey(controlParameter.name))
            {
                avrcParams[controlParameter.name] = new AvrcParameter
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