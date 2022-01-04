using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using VRC.SDK3.Avatars.Components;

namespace net.fushizen.avrc
{
    internal class AvrcObjects
    {
        internal const float RadiusScale = 10f;

        internal static GameObject buildConstraintBase(GameObject parent, string name, AvrcParameters parameters)
        {
            Transform existingObject = parent.transform.Find(name);
            if (existingObject != null)
            {
                Undo.DestroyObjectImmediate(existingObject.gameObject);
            }
            
            GameObject obj = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(obj, "AVRC setup");

            // The constraint locks the colliders to a well-known location that's out of the way
            ParentConstraint constraint = Undo.AddComponent<ParentConstraint>(obj);
            constraint.constraintActive = false;
            constraint.AddSource(new ConstraintSource
            {
                weight = 1,
                sourceTransform = AvrcAssets.Origin().transform
            });
            constraint.translationOffsets = new[]
            {
                parameters.baseOffset
            };
            constraint.locked = true;

            obj.transform.parent = parent.transform;
            obj.transform.localPosition = Vector3.zero;
            obj.transform.localRotation = Quaternion.identity;

            return obj;
        }

        internal static GameObject createTrigger(GameObject parent, AvrcParameters parameters, string name, Vector3 offset)
        {
            var obj = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(obj, "AVRC setup");

            obj.transform.parent = parent.transform;
            obj.transform.localPosition = offset * 2;
            obj.transform.localRotation = Quaternion.identity;
            
            var trigger = Undo.AddComponent<VRCAvatarTrigger>(obj);
            trigger.radius = 0.5f * RadiusScale;
            trigger.shapeType = VRCAvatarTrigger.ShapeType.Sphere;
            trigger.allowSelf = false;
            trigger.collisionMask = new List<string>(new[] {parameters.prefix + "_" + name});
            trigger.isReceiver = false;

            return obj;
        }

        internal static void createReceiver(
            GameObject parent,
            AvrcParameters parameters,
            AvrcParameters.AvrcParameter parameter
        )
        {
            var triggerObj = createTrigger(parent, parameters, parameter.name, Vector3.zero);

            var trigger = triggerObj.GetComponent<VRCAvatarTrigger>();
            trigger.isReceiver = true;
            triggerObj.SetActive(true);

            switch (parameter.type)
            {
                case AvrcParameters.AvrcParameterType.Float:
                    trigger.parameter = parameter.RxParameterName;
                    trigger.receiverType = VRCAvatarTrigger.ReceiverType.Proximity;
                    break;
                case AvrcParameters.AvrcParameterType.AvrcIsLocal:
                    trigger.parameter = parameter.name + "_F";
                    trigger.receiverType = VRCAvatarTrigger.ReceiverType.Constant;
                    trigger.parameterValue = 1;
                    break;
                case AvrcParameters.AvrcParameterType.AvrcLock:
                case AvrcParameters.AvrcParameterType.Bool:
                    trigger.parameter = parameter.name + "_F";
                    trigger.receiverType = VRCAvatarTrigger.ReceiverType.Constant;
                    trigger.parameterValue = 1;
                    break;
                case AvrcParameters.AvrcParameterType.Int:
                    trigger.parameter = parameter.name + "_F";
                    trigger.receiverType = VRCAvatarTrigger.ReceiverType.Proximity;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        internal static GameObject buildReceiverBase(GameObject parent, string name, AvrcParameters parameters)
        {
            var obj = buildConstraintBase(parent, name, parameters);
            
            // This trigger acts as a signal to the transmitter that we are listening locally (and that the anti-culling
            // object should be activated).
            createTrigger(obj, parameters, "$RXPresent", Vector3.zero);
            
            // This trigger is used as a sanity check to verify that we are properly aligned with the transmitter.
            var rxPresent = createTrigger(obj, parameters, "$TXPresent", Vector3.up);
            var trigger = rxPresent.GetComponent<VRCAvatarTrigger>();
            trigger.isReceiver = true;
            trigger.receiverType = VRCAvatarTrigger.ReceiverType.Proximity;
            trigger.parameter = parameters.Names.ParamTxProximity;

            foreach (var param in parameters.avrcParams)
            {
                createReceiver(obj, parameters, param);
            }

            return obj;
        }

        internal static GameObject buildTransmitterBase(
            GameObject parent,
            string name,
            AvrcParameters parameters
        ) {
            var obj = buildConstraintBase(parent, name, parameters);

            var rxPresent = createTrigger(obj, parameters, "$RXPresent", Vector3.zero);
            var trigger = rxPresent.GetComponent<VRCAvatarTrigger>();
            trigger.isReceiver = true;
            trigger.receiverType = VRCAvatarTrigger.ReceiverType.Constant;
            trigger.parameter = parameters.Names.ParamRxPresent;
            trigger.parameterValue = 1;

            var txPresent = createTrigger(obj, parameters, "$TXPresent", Vector3.up);
            trigger = txPresent.GetComponent<VRCAvatarTrigger>();
            trigger.isReceiver = false;

            foreach (var param in parameters.avrcParams)
            {
                createTrigger(obj, parameters, param.name, Vector3.zero);
            }

            return obj;
        }
    }
}