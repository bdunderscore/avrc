using System;
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
        private readonly AvrcLinkSpec _linkSpec;

        private readonly AvrcNames _names;
        private readonly Role _role;
        private GameObject _baseObject;
        private Vector3 _pos;

        public AvrcObjects(AvrcLinkSpec linkSpec, AvrcNames names, Role role)
        {
            _linkSpec = linkSpec;
            _names = names;
            _role = role;
            _pos = new Vector3();
        }

        private ContactBase CreateContactPair(
            ContactSpec contactSpec,
            bool defaultActive = true,
            Role sender = Role.TX
        )
        {
            var obj = new GameObject();
            obj.name = contactSpec.ObjectName;
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
                receiver.parameter = contactSpec.ParamName;
                receiver.receiverType = ContactReceiver.ReceiverType.Constant;
            }

            contact.shapeType = ContactBase.ShapeType.Sphere;
            contact.radius = Diameter * 0.5f;
            contact.position = _pos;
            contact.collisionTags = AvrcLicenseManager.MungeContactTag(contactSpec.TagName, sender == _role);

            contact.enabled = sender != _role || defaultActive;

            return contact;
        }

        internal void CreateContacts(GameObject avatar)
        {
            _baseObject = BuildConstraintBase(avatar, _names.Prefix);

            var rxPilots = _names.PilotContacts(Role.RX);
            var txPilots = _names.PilotContacts(Role.TX);

            CreateContactPair(rxPilots[0], false, Role.RX);
            CreateContactPair(txPilots[0], false);

            foreach (var param in _linkSpec.signals)
            {
                var signals = _names.SignalContacts(param, false);
                foreach (var signal in signals) CreateContactPair(signal);

                if (param.syncDirection == SyncDirection.TwoWay)
                {
                    var acks = _names.SignalContacts(param, true);
                    foreach (var signal in acks) CreateContactPair(signal, sender: Role.RX);
                }
            }

            CreateContactPair(_names.LocalContacts(Role.RX), false, Role.RX);
            CreateContactPair(_names.LocalContacts(Role.TX), false);

            CreateContactPair(rxPilots[1], false, Role.RX);
            CreateContactPair(txPilots[1], false);
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
                _linkSpec.baseOffset
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