using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using VRC.Dynamics;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.Contact.Components;

namespace net.fushizen.avrc
{
    internal class AvrcObjects
    {
        private readonly AvrcParameters _parameters;
        private readonly AvrcNames _names;

        public AvrcObjects(AvrcParameters parameters, AvrcNames names)
        {
            this._parameters = parameters;
            this._names = names;
        }
        
        
        internal const float RadiusScale = 1f;
        internal const float PresenceTestValue = 0.66f;
        // One radius (RadiusScale * 0.5) away gives a distance of zero from the edge of the transmitter to the
        // center of the receiver (received value 1). Two radiuses away is a maximum distance (1.0).
        internal static readonly Vector3 PresencePositionOffset 
            = Vector3.forward * 0.5f * RadiusScale * (2 - PresenceTestValue);

        internal GameObject buildConstraintBase(
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

        internal GameObject createTrigger<T>(GameObject parent, string name, bool staticPresence = false)
            where T: ContactBase
        {
            var obj = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(obj, "AVRC setup");

            obj.transform.parent = parent.transform;
            obj.transform.localPosition = Vector3.zero;
            obj.transform.localRotation = Quaternion.identity;
            
            T trigger = Undo.AddComponent<T>(obj);
            trigger.radius = 0.5f * RadiusScale;
            trigger.shapeType = ContactBase.ShapeType.Sphere;
            trigger.collisionTags = new List<string>(new[] {$"{_parameters.prefix}_{name}"});

            if (staticPresence)
            {
                obj.transform.localPosition += PresencePositionOffset;
            }
            
            return obj;
        }

        internal void createReceiver(
            GameObject parent,
            AvrcParameters.AvrcParameter parameter
        )
        {
            // IsLocal parameters are transmitted using the presence test triggers
            if (parameter.type == AvrcParameters.AvrcParameterType.IsLocal) return;
            
            var triggerObj = createTrigger<VRCContactReceiver>(parent, parameter.name);

            var trigger = triggerObj.GetComponent<VRCContactReceiver>();
            trigger.allowSelf = false;
            triggerObj.SetActive(false);

            switch (parameter.type)
            {
                case AvrcParameters.AvrcParameterType.Float:
                    trigger.parameter = parameter.RxParameterName;
                    trigger.receiverType = ContactReceiver.ReceiverType.Proximity;
                    triggerObj.SetActive(true);
                    break;
                case AvrcParameters.AvrcParameterType.IsLocal:
                    trigger.parameter = $"{parameter.name}_F";
                    trigger.receiverType = ContactReceiver.ReceiverType.Constant;
                    trigger.value = 1;
                    break;
                case AvrcParameters.AvrcParameterType.Bool:
                    trigger.parameter = $"{parameter.name}_F";
                    trigger.receiverType = ContactReceiver.ReceiverType.Constant;
                    trigger.value = 1;
                    break;
                case AvrcParameters.AvrcParameterType.Int:
                    trigger.parameter = $"{parameter.name}_F";
                    trigger.receiverType = ContactReceiver.ReceiverType.Proximity;
                    break;
                case AvrcParameters.AvrcParameterType.BidiInt:
                    trigger.parameter = $"{parameter.name}_F";
                    trigger.receiverType = ContactReceiver.ReceiverType.Proximity;
                    trigger.position = new Vector3(0, 0, 0);
                    
                    // Create ACK trigger
                    triggerObj = createTrigger<VRCContactSender>(parent, $"{parameter.name}_ACK");
                    triggerObj.SetActive(false);
                    var sender = triggerObj.GetComponent<VRCContactSender>();
                    sender.position = new Vector3(0, 0, 0);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        internal GameObject buildReceiverBase(GameObject parent, string name)
        {
            var obj = buildConstraintBase(parent, name, _parameters);
            
            // This trigger acts as a signal to the transmitter that we are listening locally (and that the anti-culling
            // object should be activated).
            // Note that RXPresent and TXPresent are offset from the base object, so that rotations affect them more
            // strongly than the actual transmitted value.
            createTrigger<VRCContactSender>(obj, "$RXPresent", staticPresence: true);
            
            // This trigger is used as a sanity check to verify that we are properly aligned with the transmitter.
            var rxPresent = createTrigger<VRCContactReceiver>(obj, "$TXPresent");
            var rxTrigger = rxPresent.GetComponent<VRCContactReceiver>();
            rxTrigger.allowSelf = false;
            rxTrigger.receiverType = VRCContactReceiver.ReceiverType.Proximity;
            rxTrigger.parameter = _names.ParamTxProximity;

            var txLocal = createTrigger<VRCContactReceiver>(obj, "$TXLocal");
            var localTrigger = txLocal.GetComponent<VRCContactReceiver>();
            txLocal.transform.localPosition = -(Vector3.forward * rxTrigger.radius);
            localTrigger.allowSelf = false;
            localTrigger.receiverType = VRCContactReceiver.ReceiverType.Constant;
            localTrigger.parameter = _names.ParamTxLocal;
            localTrigger.collisionTags = new List<string>(rxTrigger.collisionTags);
            
            foreach (var param in _parameters.avrcParams)
            {
                createReceiver(obj, param);
            }

            return obj;
        }

        internal GameObject buildTransmitterBase(
            GameObject parent,
            string name
        ) {
            var obj = buildConstraintBase(parent, name, _parameters);

            var rxPresent = createTrigger<VRCContactReceiver>(obj, "$RXPresent");
            var trigger = rxPresent.GetComponent<VRCContactReceiver>();
            trigger.allowSelf = false;
            trigger.receiverType = VRCContactReceiver.ReceiverType.Proximity;
            trigger.parameter = _names.ParamRxPresent;
            var rxPresentMask = trigger.collisionTags;

            createTrigger<VRCContactSender>(obj, "$TXPresent", staticPresence: true);

            var rxLocal = createTrigger<VRCContactReceiver>(obj, "$RXLocal");
            trigger = rxLocal.GetComponent<VRCContactReceiver>();
            rxLocal.transform.localPosition = -(Vector3.forward * trigger.radius);
            trigger.allowSelf = false;
            trigger.receiverType = VRCContactReceiver.ReceiverType.Constant;
            trigger.parameter = _names.ParamRxLocal;
            trigger.collisionTags = new List<string>(rxPresentMask);

            foreach (var param in _parameters.avrcParams)
            {
                // IsLocal parameters are implicitly transmitted using the presence test trigger
                if (param.type == AvrcParameters.AvrcParameterType.IsLocal) continue;

                var triggerObj = createTrigger<VRCContactSender>(obj, param.name);
                triggerObj.SetActive(param.type == AvrcParameters.AvrcParameterType.Float);

                if (param.type == AvrcParameters.AvrcParameterType.BidiInt)
                {
                    triggerObj.GetComponent<VRCContactSender>().position = new Vector3(0, 0, 0);
                    
                    // Create ACK receiver
                    triggerObj = createTrigger<VRCContactReceiver>(obj, $"{param.name}_ACK");
                    triggerObj.SetActive(false);
                    trigger = triggerObj.GetComponent<VRCContactReceiver>();
                    trigger.allowSelf = false;
                    trigger.parameter = $"{param.name}_ACK";
                    trigger.receiverType = VRCContactReceiver.ReceiverType.Proximity;
                    trigger.position = new Vector3(0, 0, 0);
                    break;
                }
            }

            return obj;
        }
    }
}