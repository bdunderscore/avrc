using UnityEngine;
using UnityEngine.Animations;

namespace net.fushizen.avrc
{
    internal class AvrcAnimations
    {
        private const float BOUNDS_SIZE = 1000f;

        private readonly AvrcLinkSpec _linkSpec;
        private readonly AvrcNames _names;

        public AvrcAnimations(AvrcLinkSpec linkSpec, AvrcNames names)
        {
            _linkSpec = linkSpec;
            this._names = names;
        }

        internal AnimationClip Named(string name, GetClipDelegate d)
        {
            var clip = d();
            clip.name = name;
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

        internal AnimationClip PresenceClip(LocalState local, SignalEncoding signalEncoding)
        {
            var path = _names.ObjectPath;
            AnimationClip clip = new AnimationClip();
            clip.SetCurve(path, typeof(ParentConstraint), "m_Active", AnimationCurve.Constant(0, 1, 1));

            signalEncoding.AddDisableAll(_names, clip);

            if (local == LocalState.OwnerLocal)
                signalEncoding.MyPilotLocal.AddClip(_names, clip, 1);
            else
                signalEncoding.MyPilotNotLocal.AddClip(_names, clip, 1);

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