using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using VRC.Dynamics;
using VRC.SDK3.Avatars.Components;

namespace net.fushizen.avrc
{
    [DefaultExecutionOrder(1)] // run after Av3Emu
    public class AvrcTestHelper : MonoBehaviour
    {
        public VRCAvatarDescriptor avatar1;
        public VRCAvatarDescriptor avatar2;

        private bool av1_init_done;
        private bool av2_init_done;
        private FieldInfo f_IsMirrorClone, f_IsShadowClone, f_AvatarSyncSource;

        private Type lyumaEmulatorType;

        // Update is called once per frame
        private void Update()
        {
            TryInit(ref av1_init_done, avatar1, "av1", "av2");
            TryInit(ref av2_init_done, avatar2, "av2", "av1");
        }

        private void TryInit(ref bool init_done, VRCAvatarDescriptor avatar, string localPrefix, string nonLocalPrefix)
        {
            if (init_done || avatar == null) return;

            // Try to find the LyumaAv3Runtime component reflectively (to avoid a hard dependency)
            Component runtime;
            if (lyumaEmulatorType != null)
            {
                runtime = avatar.GetComponent(lyumaEmulatorType);
            }
            else
            {
                runtime = null;
                foreach (var component in avatar.GetComponents(typeof(Component)))
                {
                    var ty = component.GetType();
                    if (ty.Name == "LyumaAv3Runtime")
                    {
                        runtime = component;
                        lyumaEmulatorType = ty;
                        f_IsMirrorClone = ty.GetField("IsMirrorClone");
                        f_IsShadowClone = ty.GetField("IsShadowClone");
                        f_AvatarSyncSource = ty.GetField("AvatarSyncSource");
                        break;
                    }
                }
            }

            if (runtime == null) return;

            if (f_AvatarSyncSource.GetValue(runtime) != runtime)
            {
                Debug.LogError("Avatar does not self-reference");
                return;
            }

            // Try to find the non-local clone
            Component nonLocal = null;
            foreach (var root in avatar.gameObject.scene.GetRootGameObjects())
            {
                var subRuntime = root.GetComponent(lyumaEmulatorType);
                if (subRuntime == null || subRuntime == runtime) continue;

                if ((bool) f_IsMirrorClone.GetValue(subRuntime) ||
                    (bool) f_IsShadowClone.GetValue(subRuntime)) continue;

                if (f_AvatarSyncSource.GetValue(subRuntime) != runtime) continue;

                nonLocal = subRuntime;
                break;
            }

            if (nonLocal == null) return;

            // Munge the contact tags
            MungeContactTags(runtime, localPrefix);
            MungeContactTags(nonLocal, nonLocalPrefix);

            init_done = true;
        }

        private void MungeContactTags(Component root, string prefix)
        {
            var collisionTagsHash =
                typeof(ContactBase).GetField("collisionTagsHash", BindingFlags.NonPublic | BindingFlags.Instance);
            if (collisionTagsHash == null)
            {
                Debug.LogError("collisionTagsHash not found on ContactBase");
                return;
            }

            foreach (var contact in root.GetComponentsInChildren<ContactBase>(true))
            {
                var tags = contact.collisionTags.Select(tag =>
                {
                    if (tag.StartsWith("_AVRCI_"))
                        return prefix + tag;
                    return tag;
                }).ToList();
                contact.collisionTags = tags;

                var tagsHash = new HashSet<int>();
                foreach (var hash in contact.collisionTags.Select(t => t.GetHashCode())) tagsHash.Add(hash);
                collisionTagsHash.SetValue(contact, tagsHash);
            }
        }
    }
}