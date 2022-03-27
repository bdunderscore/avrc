using UnityEngine;
using UnityEngine.Animations;
using VRC.SDK3.Dynamics.Contact.Components;

namespace net.fushizen.avrc
{
    internal class AvrcAnimations
    {
        private const float BOUNDS_SIZE = 1000f;
        private readonly AvrcNames _names;

        private readonly AvrcParameters _parameters;

        public AvrcAnimations(AvrcParameters parameters, AvrcNames names)
        {
            this._parameters = parameters;
            this._names = names;
        }

        internal AnimationClip Named(string name, GetClipDelegate d)
        {
            var clip = d();
            clip.name = name;
            return clip;
            /*
            string assetPath = AvrcAssets.GetGeneratedAssetsFolder() + "/" + name + ".anim";
            var clip = d();
            
            AssetDatabase.CreateAsset(clip, assetPath);

            return clip;
            */
        }

        internal AnimationClip LinearClip(string path, Vector3 baseOffset)
        {
            AnimationClip clip = new AnimationClip();
            clip.SetCurve(path, typeof(Transform), "m_LocalPosition.x",
                AnimationCurve.Constant(0, 1, 0));
            clip.SetCurve(path, typeof(Transform), "m_LocalPosition.y",
                AnimationCurve.Constant(0, 1, 0));
            clip.SetCurve(path, typeof(Transform), "m_LocalPosition.z",
                AnimationCurve.Linear(
                    0, AvrcObjects.Diameter * 1,
                    1, AvrcObjects.Diameter * 0.5f
                ));

            return clip;
        }

        internal AnimationClip GlobalDefaultsClip()
        {
            var clip = new AnimationClip();
            clip.SetCurve("AVRC/AVRC_Bounds", typeof(Transform), "m_LocalScale.x",
                AnimationCurve.Constant(0, 1, 0.01f)
            );
            clip.SetCurve("AVRC/AVRC_Bounds", typeof(Transform), "m_LocalScale.y",
                AnimationCurve.Constant(0, 1, 0.01f)
            );
            clip.SetCurve("AVRC/AVRC_Bounds", typeof(Transform), "m_LocalScale.z",
                AnimationCurve.Constant(0, 1, 0.01f)
            );

            return clip;
        }

        internal AnimationClip ConstantClip(string path, Vector3 baseOffset, float value)
        {
            AnimationClip clip = new AnimationClip();
            clip.SetCurve(path, typeof(Transform), "m_LocalPosition.x",
                AnimationCurve.Constant(0, 1, 0));
            clip.SetCurve(path, typeof(Transform), "m_LocalPosition.y",
                AnimationCurve.Constant(0, 1, 0));
            clip.SetCurve(path, typeof(Transform), "m_LocalPosition.z",
                AnimationCurve.Constant(0, 1,
                    AvrcObjects.Diameter * Mathf.LerpUnclamped(1, 0.5f, value)));

            return clip;
        }

        internal AnimationClip SignalClip(AvrcParameters.AvrcParameter param, bool isAck, int index)
        {
            var clip = new AnimationClip();

            foreach (var bit in _names.SignalParam(param, isAck))
            {
                clip.SetCurve($"{_names.ObjectPath}/{bit.ObjectName}", typeof(GameObject), "m_IsActive",
                    AnimationCurve.Constant(0, 1, index & 1));
                index >>= 1;
            }

            return clip;
        }

        internal AnimationClip PresenceClip(LocalState local, Role role)
        {
            var path = _names.ObjectPath;
            AnimationClip clip = new AnimationClip();
            clip.SetCurve(path, typeof(ParentConstraint), "m_Active", AnimationCurve.Constant(0, 1, 1));

            foreach (var pilotSignal in _names.SignalPilots(role))
                clip.SetCurve($"{_names.ObjectPath}/{pilotSignal.ObjectName}", typeof(VRCContactSender),
                    "m_Enabled", AnimationCurve.Constant(0, 1, 1)
                );

            clip.SetCurve($"{_names.ObjectPath}/{_names.SignalLocal(role).ObjectName}", typeof(GameObject),
                "m_IsActive",
                AnimationCurve.Constant(0, 1, local == LocalState.OwnerLocal ? 1.0f : 0.0f));

            // TODO: Control parameter contact activation?
            return clip;
        }

        internal delegate AnimationClip GetClipDelegate();

        internal enum LocalState
        {
            Unknown,
            OwnerLocal,
            PeerLocal
        }
    }
}