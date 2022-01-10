using UnityEngine;
using UnityEngine.Animations;

namespace net.fushizen.avrc
{
    internal class AvrcAnimations
    {
        
        internal delegate AnimationClip GetClipDelegate();

        internal static AnimationClip Named(string name, GetClipDelegate d)
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
        
        internal static AnimationClip LinearClip(string path, Vector3 baseOffset)
        {
            AnimationClip clip = new AnimationClip();
            clip.SetCurve(path, typeof(Transform), "m_LocalPosition.x", 
                AnimationCurve.Constant(0, 1, 0));
            clip.SetCurve(path, typeof(Transform), "m_LocalPosition.y", 
                AnimationCurve.Constant(0, 1, 0));
            clip.SetCurve(path, typeof(Transform), "m_LocalPosition.z", 
                AnimationCurve.Linear(
                    0, AvrcObjects.RadiusScale * (baseOffset.z + 1), 
                    1, AvrcObjects.RadiusScale * (baseOffset.z + 0.5f)
            ));

            return clip;
        }

        internal static AnimationClip ConstantClip(string path, Vector3 baseOffset, float value)
        {
            AnimationClip clip = new AnimationClip();
            clip.SetCurve(path, typeof(Transform), "m_LocalPosition.x", 
                AnimationCurve.Constant(0, 1, 0));
            clip.SetCurve(path, typeof(Transform), "m_LocalPosition.y", 
                AnimationCurve.Constant(0, 1, 0));
            clip.SetCurve(path, typeof(Transform), "m_LocalPosition.z",
                AnimationCurve.Constant(0, 1, AvrcObjects.RadiusScale * (baseOffset.z + Mathf.LerpUnclamped(1, 0.5f, value))));

            return clip;
        }

        internal static AnimationClip EnableConstraintClip(string path)
        {
            AnimationClip clip = new AnimationClip();
            clip.SetCurve(path, typeof(ParentConstraint), "m_Active", AnimationCurve.Constant(0, 1, 1));

            return clip;
        }

        
        // TODO: Actually needs the constraint enabled at all times
        
        /// <summary>
        /// Creates a clip which runs on the transmitter and enables certain objects when the receiver is the local
        /// player's avatar. Specifically, we enable the constraint and each transmitted parameter's trigger.
        /// </summary>
        /// <param name="parameters"></param>
        /// <returns></returns>
        internal static AnimationClip ReceiverPresentClip(AvrcParameters parameters)
        {
            var clip = EnableConstraintClip(parameters.Names.ObjectPath);

            foreach (var parameter in parameters.avrcParams)
            {
                var gameObjectName = parameters.Names.ParameterPath(parameter);
                clip.SetCurve(gameObjectName, typeof(GameObject), "m_IsActive",
                    AnimationCurve.Constant(0, 1, AvrcObjects.RadiusScale));
            }

            return clip;
        }

        /// <summary>
        /// Transmit each IsLocal parameter to the receiver.
        /// </summary>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public static AnimationClip TransmitterLocalClip(AvrcParameters parameters)
        {
            var clip = new AnimationClip();

            foreach (var parameter in parameters.avrcParams)
            {
                if (parameter.type == AvrcParameters.AvrcParameterType.AvrcIsLocal)
                {
                    var gameObjectName = parameters.Names.ParameterPath(parameter);
                    clip.SetCurve(gameObjectName, typeof(Transform), "m_LocalPosition.x",
                        AnimationCurve.Constant(0, 1, 0));
                    clip.SetCurve(gameObjectName, typeof(Transform), "m_LocalPosition.y",
                        AnimationCurve.Constant(0, 1, 0));
                    clip.SetCurve(gameObjectName, typeof(Transform), "m_LocalPosition.z",
                        AnimationCurve.Constant(0, 1, 0));
                }
            }

            return clip;
        }
    }
}