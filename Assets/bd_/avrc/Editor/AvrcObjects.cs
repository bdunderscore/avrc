using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using VRC.SDK3.Avatars.Components;

namespace net.fushizen.avrc
{
    internal class AvrcObjects
    {
        internal const float RadiusScale = 1f;
        internal const float PresenceTestValue = 0.66f;
        // One radius (RadiusScale * 0.5) away gives a distance of zero from the edge of the transmitter to the
        // center of the receiver (received value 1). Two radiuses away is a maximum distance (1.0).
        internal static readonly Vector3 PresencePositionOffset 
            = Vector3.forward * 0.5f * RadiusScale * (2 - PresenceTestValue);

        internal static GameObject buildConstraintBase(
            GameObject parent,
            string name, 
            AvrcParameters parameters,
            bool isTx = false)
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
            
            // Build a big cube to control avatar bounds when a receiver is present.
            bool transmitsValue =
                isTx || parameters.avrcParams.Any(p => p.type == AvrcParameters.AvrcParameterType.BidiInt);
            if (transmitsValue && parent.transform.Find("AVRC_Bounds") == null)
            {
                GameObject boundsCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Undo.RegisterCreatedObjectUndo(boundsCube, "AVRC setup");
                
                UnityEngine.Object.DestroyImmediate(boundsCube.GetComponent<BoxCollider>());
                var renderer = boundsCube.GetComponent<MeshRenderer>();
                renderer.sharedMaterials = Array.Empty<Material>();

                boundsCube.name = "AVRC_Bounds";
                boundsCube.transform.localScale = Vector3.one * 0.01f;
                boundsCube.transform.parent = parent.transform;
                boundsCube.transform.localPosition = Vector3.zero;
                boundsCube.transform.localRotation = Quaternion.identity;
            }

            return obj;
        }

        internal static GameObject createTrigger(GameObject parent, AvrcParameters parameters, string name, bool staticPresence = false)
        {
            var obj = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(obj, "AVRC setup");

            obj.transform.parent = parent.transform;
            obj.transform.localPosition = Vector3.zero;
            obj.transform.localRotation = Quaternion.identity;
            
            var trigger = Undo.AddComponent<VRCAvatarTrigger>(obj);
            trigger.radius = 0.5f * RadiusScale;
            trigger.shapeType = VRCAvatarTrigger.ShapeType.Sphere;
            trigger.allowSelf = false;
            trigger.collisionMask = new List<string>(new[] {$"{parameters.prefix}_{name}"});
            trigger.isReceiver = false;
            
            if (staticPresence)
            {
                obj.transform.localPosition += PresencePositionOffset;
            }
            
            return obj;
        }

        internal static void createReceiver(
            GameObject parent,
            AvrcParameters parameters,
            AvrcParameters.AvrcParameter parameter
        )
        {
            var triggerObj = createTrigger(parent, parameters, parameter.name);

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
                    trigger.parameter = $"{parameter.name}_F";
                    trigger.receiverType = VRCAvatarTrigger.ReceiverType.Constant;
                    trigger.parameterValue = 1;
                    break;
                case AvrcParameters.AvrcParameterType.AvrcLock:
                case AvrcParameters.AvrcParameterType.Bool:
                    trigger.parameter = $"{parameter.name}_F";
                    trigger.receiverType = VRCAvatarTrigger.ReceiverType.Constant;
                    trigger.parameterValue = 1;
                    break;
                case AvrcParameters.AvrcParameterType.Int:
                    trigger.parameter = $"{parameter.name}_F";
                    trigger.receiverType = VRCAvatarTrigger.ReceiverType.Proximity;
                    break;
                case AvrcParameters.AvrcParameterType.BidiInt:
                    trigger.parameter = $"{parameter.name}_F";
                    trigger.receiverType = VRCAvatarTrigger.ReceiverType.Proximity;
                    trigger.position = new Vector3(0, 0, 0);
                    
                    // Create ACK trigger
                    triggerObj = createTrigger(parent, parameters, $"{parameter.name}_ACK");
                    trigger = triggerObj.GetComponent<VRCAvatarTrigger>();
                    trigger.position = new Vector3(0, 0, 0);
                    trigger.isReceiver = false;
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
            // Note that RXPresent and TXPresent are offset from the base object, so that rotations affect them more
            // strongly than the actual transmitted value.
            createTrigger(obj, parameters, "$RXPresent", staticPresence: true);
            
            // This trigger is used as a sanity check to verify that we are properly aligned with the transmitter.
            var rxPresent = createTrigger(obj, parameters, "$TXPresent");
            var trigger = rxPresent.GetComponent<VRCAvatarTrigger>();
            trigger.isReceiver = true;
            trigger.receiverType = VRCAvatarTrigger.ReceiverType.Proximity;
            trigger.parameter = parameters.Names.ParamTxProximity;

            var txLocal = createTrigger(obj, parameters, "$TXLocal");
            var localTrigger = txLocal.GetComponent<VRCAvatarTrigger>();
            txLocal.transform.localPosition = -(Vector3.forward * trigger.radius);
            localTrigger.isReceiver = true;
            localTrigger.receiverType = VRCAvatarTrigger.ReceiverType.Constant;
            localTrigger.parameter = parameters.Names.ParamTxLocal;
            localTrigger.collisionMask = new List<string>(trigger.collisionMask);
            
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

            var rxPresent = createTrigger(obj, parameters, "$RXPresent");
            var trigger = rxPresent.GetComponent<VRCAvatarTrigger>();
            trigger.isReceiver = true;
            trigger.receiverType = VRCAvatarTrigger.ReceiverType.Proximity;
            trigger.parameter = parameters.Names.ParamRxPresent;
            var rxPresentMask = trigger.collisionMask;

            var txPresent = createTrigger(obj, parameters, "$TXPresent", staticPresence: true);
            trigger = txPresent.GetComponent<VRCAvatarTrigger>();
            trigger.isReceiver = false;

            var rxLocal = createTrigger(obj, parameters, "$RXLocal");
            trigger = rxLocal.GetComponent<VRCAvatarTrigger>();
            rxLocal.transform.localPosition = -(Vector3.forward * trigger.radius);
            trigger.isReceiver = true;
            trigger.receiverType = VRCAvatarTrigger.ReceiverType.Constant;
            trigger.parameter = parameters.Names.ParamRxLocal;
            trigger.collisionMask = new List<string>(rxPresentMask);

            foreach (var param in parameters.avrcParams)
            {
                var triggerObj = createTrigger(obj, parameters, param.name);

                if (param.type == AvrcParameters.AvrcParameterType.BidiInt)
                {
                    triggerObj.GetComponent<VRCAvatarTrigger>().position = new Vector3(0, 0, 0);
                    
                    // Create ACK receiver
                    triggerObj = createTrigger(obj, parameters, $"{param.name}_ACK");
                    trigger = triggerObj.GetComponent<VRCAvatarTrigger>();
                    trigger.isReceiver = true;
                    trigger.parameter = $"{param.name}_ACK";
                    trigger.receiverType = VRCAvatarTrigger.ReceiverType.Proximity;
                    trigger.position = new Vector3(0, 0, 0);
                    break;
                }
            }

            return obj;
        }
    }
}