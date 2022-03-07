using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Crypto.Parameters;
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

        internal AvrcParametersGenerator(
            AvrcParameters parameters
        )
        {
            _parameters = parameters;
        }

        internal void GenerateParametersUI()
        {
            if (_parameters.sourceExpressionMenu == null) return;

            _foldout = EditorGUILayout.Foldout(_foldout, "Generate from expressions menu");
            if (!_foldout)
            {
                return;
            }

            _avatar = EditorGUILayout.ObjectField("Reference Avatar", _avatar, typeof(VRCAvatarDescriptor), true)
                    as VRCAvatarDescriptor;

            if (_avatar != null)
            {
                if (_avatar.expressionParameters == null)
                {
                    EditorGUILayout.HelpBox("No expression parameters found", MessageType.Error);
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
                    EditorGUILayout.HelpBox("No new parameters found", MessageType.Info);
                }
                else
                {
                    string msg = "Found parameters:\n" + string.Join("\n", paramList.Select(p => p.name));
                    EditorGUILayout.HelpBox(msg, MessageType.Info);

                    if (GUILayout.Button("Add parameters"))
                    {
                        foreach (var avrcParameter in paramList)
                        {
                            _parameters.avrcParams.Add(avrcParameter);
                        }
                    }
                }
            }
        }
        
        internal static Dictionary<string, AvrcParameter> GenerateParameters(
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
                    errors.Add($"Duplicate parameter: {param.name}");
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

        private static void RecordFloatParam(
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
                errors.Add($"Parameter not defined in expressions parameters: {subParameter.name}");
                return;
            }
            
            var ty = knownParameters[subParameter.name].valueType;
            if (ty != VRCExpressionParameters.ValueType.Float)
            {
                errors.Add("Puppet menu subparameter is not a float: " + subParameter.name);
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

        private static void RecordIntParam(
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
                errors.Add(
                           $"Parameter {controlParameter.name} is not defined in the expression parameters."
                           );
                return;
            }

            var ty = knownParameters[controlParameter.name].valueType;
            if (ty == VRCExpressionParameters.ValueType.Float)
            {
                errors.Add($"Primary parameter {controlParameter.name} is a float, not an int.");
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