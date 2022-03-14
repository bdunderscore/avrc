using UnityEngine;
using UnityEngine.Animations;

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
                    0, AvrcObjects.RadiusScale * 1,
                    1, AvrcObjects.RadiusScale * 0.5f
                ));

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
                    AvrcObjects.RadiusScale * Mathf.LerpUnclamped(1, 0.5f, value)));

            return clip;
        }

        internal AnimationClip EnableConstraintClip(LocalState local, string sendingSelfPresent)
        {
            var path = _names.ObjectPath;
            AnimationClip clip = new AnimationClip();
            clip.SetCurve(path, typeof(ParentConstraint), "m_Active", AnimationCurve.Constant(0, 1, 1));

            if (local == LocalState.OwnerLocal)
            {
                // When local we offset to the opposite side of the 
                Vector3 offset = -AvrcObjects.PresencePositionOffset;

                clip.SetCurve(sendingSelfPresent, typeof(Transform), "m_LocalPosition.x",
                    AnimationCurve.Constant(0, 1, offset.x));
                clip.SetCurve(sendingSelfPresent, typeof(Transform), "m_LocalPosition.y",
                    AnimationCurve.Constant(0, 1, offset.y));
                clip.SetCurve(sendingSelfPresent, typeof(Transform), "m_LocalPosition.z",
                    AnimationCurve.Constant(0, 1, offset.z));
            }
            else if (local == LocalState.PeerLocal)
            {
                clip.SetCurve("AVRC/AVRC_Bounds", typeof(Transform), "m_LocalScale.x",
                    AnimationCurve.Constant(0, 1, BOUNDS_SIZE));
                clip.SetCurve("AVRC/AVRC_Bounds", typeof(Transform), "m_LocalScale.y",
                    AnimationCurve.Constant(0, 1, BOUNDS_SIZE));
                clip.SetCurve("AVRC/AVRC_Bounds", typeof(Transform), "m_LocalScale.z",
                    AnimationCurve.Constant(0, 1, BOUNDS_SIZE));
            }

            foreach (var param in _parameters.avrcParams)
            {
                int isActive = local != LocalState.Unknown ? 1 : 0;

                clip.SetCurve(
                    _names.ParameterPath(param), typeof(GameObject), "m_IsActive",
                    AnimationCurve.Constant(0, 1, isActive));

                if (param.syncDirection == AvrcParameters.SyncDirection.TwoWay)
                {
                    clip.SetCurve(
                        _names.ParameterPath(param) + "_ACK", typeof(GameObject), "m_IsActive",
                        AnimationCurve.Constant(0, 1, isActive));
                }
            }

            return clip;
        }


        // TODO: Actually needs the constraint enabled at all times

        /// <summary>
        /// Creates a clip which runs on the transmitter and enables certain objects when the receiver is the local
        /// player's avatar. Specifically, we enable the constraint and each transmitted parameter's trigger.
        /// </summary>
        /// <param name="parameters"></param>
        /// <returns></returns>
        internal AnimationClip ReceiverPresentClip(LocalState local)
        {
            return EnableConstraintClip(local, _names.ObjRxPresent);
        }

        internal AnimationClip TransmitterPresentClip(LocalState local)
        {
            return EnableConstraintClip(local, _names.ObjTxPresent);
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