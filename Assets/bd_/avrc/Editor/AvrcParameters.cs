﻿using System;
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

            public string RxParameterName => !string.IsNullOrEmpty(rxName) ? rxName : name;

            internal string TxParameterFlag(AvrcNames names)
            {
                return $"_AVRC_{names.Prefix}_tx_{name}";
            }
        }

        internal struct AvrcNames
        {
            internal readonly string Prefix;

            internal AvrcNames(string prefix)
            {
                this.Prefix = prefix;
            }

            internal string ObjectPath => $"AVRC/{Prefix}";
            private string LayerPrefix => $"_AVRC_{Prefix}";
            internal string LayerSetup => $"{LayerPrefix}_Setup";
            internal string LayerRxConstraint => $"{LayerPrefix}_RXConstraint";
            internal string LayerTxEnable => $"{LayerPrefix}_TXEnable";
            
            internal string ParamRxPresent => $"_AVRC_{Prefix}_RxPresent";
            internal string ParamTxProximity => $"_AVRC_{Prefix}_TxProximity";
            internal string ParamTxActive => $"_AVRC_{Prefix}_TxActive";
            internal string ParamRxLocal => $"_AVRC_{Prefix}_RxLocal";
            internal string ParamTxLocal => $"_AVRC_{Prefix}_TxLocal";

            internal string ObjTxPresent => ObjectPath + "/$TXPresent";
            internal string ObjTxLocal => ObjectPath + "/$TXLocal";
            internal string ObjRxPresent => ObjectPath + "/$RXPresent";
            internal string ObjRxLocal => ObjectPath + "/$RXLocal";

            internal string ParameterPath(AvrcParameter parameter)
            {
                return $"{ObjectPath}/{parameter.name}";
            }

            public string ParameterLayerName(AvrcParameter parameter)
            {
                return $"_AVRC_{Prefix}_{parameter.name}";
            }
        }

        internal AvrcNames Names => new AvrcNames(prefix);
    }
}