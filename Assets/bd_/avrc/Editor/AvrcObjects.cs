/*
 * MIT License
 * 
 * Copyright (c) 2021-2022 bd_
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE. 
 */

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
        private readonly SignalEncoding _signalEncoding;
        private GameObject _baseObject;
        private Vector3 _pos;

        public AvrcObjects(AvrcLinkSpec linkSpec, AvrcNames names, Role role)
        {
            _linkSpec = linkSpec;
            _names = names;
            _signalEncoding = new SignalEncoding(linkSpec, role, names.LayerPrefix);
            _role = role;
            _pos = new Vector3();
        }

        private ContactBase CreateContactPair(
            ContactSpec contactSpec)
        {
            var obj = new GameObject();
            obj.name = contactSpec.Name;
            obj.transform.parent = _baseObject.transform;
            obj.transform.localPosition = Vector3.zero;

            Undo.RegisterCreatedObjectUndo(obj, "AVRC setup");

            ContactBase contact;
            if (contactSpec.IsSender)
            {
                contact = obj.AddComponent<VRCContactSender>();
            }
            else
            {
                var receiver = obj.AddComponent<VRCContactReceiver>();
                contact = receiver;
                receiver.parameter = contactSpec.Parameter;
                receiver.receiverType = ContactReceiver.ReceiverType.Constant;
            }

            contact.shapeType = ContactBase.ShapeType.Sphere;
            contact.radius = Diameter * 0.5f;
            contact.position = contactSpec.BasePosition;
            contact.collisionTags = contactSpec.CollisionTags;

            contact.enabled = false;

            return contact;
        }

        internal void CreateContacts(GameObject avatar)
        {
            _baseObject = BuildConstraintBase(avatar, _names.Prefix);

            foreach (var contact in _signalEncoding.AllContacts)
            {
                CreateContactPair(contact);
            }
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