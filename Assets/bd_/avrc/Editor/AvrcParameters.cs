using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
using Random = UnityEngine.Random;

namespace net.fushizen.avrc
{
    [Serializable]
    [CreateAssetMenu(fileName="AVRCParams", menuName = "bd_/AVRC Parameters", order=51)]
    public class AvrcParameters : ScriptableObject
    {
        public List<AvrcParameter> avrcParams;
        public Vector3 baseOffset;
        public string prefix;

        /// <summary>
        /// Reference to an expressions menu asset that will be cloned into this AvrcParameters asset.
        /// </summary>
        public VRCExpressionsMenu sourceExpressionMenu;
        /// <summary>
        /// Reference to the expressions menu asset embedded in this AvrcParameters asset.
        /// </summary>
        public VRCExpressionsMenu embeddedExpressionsMenu;

        [SuppressMessage("ReSharper", "HeapView.ObjectAllocation.Evident")]
        public AvrcParameters()
        {
            avrcParams = new List<AvrcParameter>();
        }
        
        [Serializable]
        public enum AvrcParameterType
        {
            Bool,
            Int,
            Float,
            IsLocal,
            BidiInt
        }
        
        [Serializable]
        public struct AvrcParameter
        {
            public string name;
            public AvrcParameterType type;
            public float timeoutSeconds;
            [CanBeNull] public string rxName;
            public int minVal, maxVal;

            internal string TxParameterFlag(AvrcNames names)
            {
                return $"_AVRC_internal_{names.Prefix}_tx_{name}";
            }
        }
    }
}