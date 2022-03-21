using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.Contact.Components;
using Object = UnityEngine.Object;

namespace net.fushizen.avrc
{
    internal class AvrcObjects
    {
        // Just under 5m - the maximum size for contacts to work reliably
        internal const float Diameter = 4.5f;
        private const float Separation = Diameter + 0.5f;

        private readonly AvrcNames _names;
        private readonly AvrcParameters _parameters;
        private readonly Role _role;
        private GameObject _baseObject;
        private Vector3 _pos;

        public AvrcObjects(AvrcParameters parameters, AvrcNames names, Role role)
        {
            _parameters = parameters;
            _names = names;
            _role = role;
            _pos = new Vector3();
        }

        private ContactBase CreateTriggerPair(
            Signal signal,
            bool defaultActive = true,
            Role sender = Role.TX
        )
        {
            var obj = new GameObject();
            obj.name = signal.ObjectName;
            obj.transform.parent = _baseObject.transform;
            obj.transform.localPosition = _pos;
            _pos += new Vector3(0, Separation, 0);
            Undo.RegisterCreatedObjectUndo(obj, "AVRC setup");

            ContactBase contact;
            if (sender == _role)
            {
                contact = obj.AddComponent<VRCContactSender>();
            }
            else
            {
                var receiver = obj.AddComponent<VRCContactReceiver>();
                contact = receiver;
                receiver.parameter = signal.ParamName;
                receiver.receiverType = ContactReceiver.ReceiverType.Constant;
                receiver.value = 1.0f;
            }

            contact.shapeType = ContactBase.ShapeType.Sphere;
            contact.radius = Diameter * 0.5f;
            contact.position = _pos;
            contact.collisionTags = new List<string>(new[] {signal.TagName});

            contact.enabled = sender != _role || defaultActive;

            return contact;
        }

        internal void CreateTriggers(GameObject avatar)
        {
            _baseObject = BuildConstraintBase(avatar, _names.Prefix);

            var rxPilots = _names.SignalPilots(Role.RX);
            var txPilots = _names.SignalPilots(Role.TX);

            CreateTriggerPair(rxPilots[0], false, Role.RX);
            CreateTriggerPair(txPilots[0], false);

            foreach (var param in _parameters.avrcParams)
            {
                var signals = _names.SignalParam(param, false);
                foreach (var signal in signals) CreateTriggerPair(signal);

                if (param.syncDirection == AvrcParameters.SyncDirection.TwoWay)
                {
                    var acks = _names.SignalParam(param, true);
                    foreach (var signal in acks) CreateTriggerPair(signal, sender: Role.RX);
                }
            }

            CreateTriggerPair(_names.SignalLocal(Role.RX), false, Role.RX);
            CreateTriggerPair(_names.SignalLocal(Role.TX), false);

            CreateTriggerPair(rxPilots[1], false, Role.RX);
            CreateTriggerPair(txPilots[1], false);
        }

        private GameObject BuildConstraintBase(
            GameObject avatar,
            string name
        )
        {
            var parent = CreateRoot(avatar);

            Transform existingObject = parent.transform.Find(name);
            if (existingObject != null)
            {
                Undo.DestroyObjectImmediate(existingObject.gameObject);
            }

            GameObject obj = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(obj, "AVRC setup");

            // The constraint locks the colliders to an offset from the world origin
            ParentConstraint constraint = Undo.AddComponent<ParentConstraint>(obj);
            constraint.constraintActive = false;
            constraint.AddSource(new ConstraintSource
            {
                weight = 1,
                sourceTransform = AvrcAssets.Origin().transform
            });
            constraint.translationOffsets = new[]
            {
                _parameters.baseOffset
            };
            constraint.locked = true;

            obj.transform.parent = parent.transform;
            obj.transform.localPosition = Vector3.zero;
            obj.transform.localRotation = Quaternion.identity;

            // Build a big cube to control avatar bounds when a receiver is present.
            if (parent.transform.Find("AVRC_Bounds") == null)
            {
                GameObject boundsCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Undo.RegisterCreatedObjectUndo(boundsCube, "AVRC setup");

                Object.DestroyImmediate(boundsCube.GetComponent<BoxCollider>());
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


        private static GameObject CreateRoot(GameObject avatar)
        {
            var rootTransform = avatar.transform.Find("AVRC");
            GameObject root;
            if (rootTransform != null)
            {
                root = rootTransform.gameObject;
            }
            else
            {
                root = new GameObject
                {
                    transform =
                    {
                        parent = avatar.transform,
                        localPosition = Vector3.zero,
                        localRotation = Quaternion.identity
                    },
                    name = "AVRC"
                };
                Undo.RegisterCreatedObjectUndo(root, "AVRC setup");
            }

            if (root.GetComponent<ScaleConstraint>() == null)
            {
                var constraint = Undo.AddComponent<ScaleConstraint>(root);
                constraint.AddSource(new ConstraintSource
                {
                    weight = 1,
                    sourceTransform = AvrcAssets.Origin().transform
                });
                constraint.locked = true;
                constraint.constraintActive = true;
            }

            return root;
        }
    }
}