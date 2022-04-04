using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;
using UnityEngine.Serialization;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace net.fushizen.avrc
{
    [Serializable]
    [CreateAssetMenu(fileName = "AVRCLink", menuName = "bd_/Avatar Remote/Link", order = 51)]
    public class AvrcLinkSpec : ScriptableObject
    {
        [FormerlySerializedAs("avrcParams")] public List<AvrcSignal> signals;
        public Vector3 baseOffset;
        public string guid;

        /// <summary>
        /// Reference to an expressions menu asset that will be cloned into this AvrcParameters asset.
        /// </summary>
        public VRCExpressionsMenu sourceExpressionMenu;

        /// <summary>
        /// Reference to the expressions menu asset embedded in this AvrcParameters asset.
        /// </summary>
        public VRCExpressionsMenu embeddedExpressionsMenu;

        [SuppressMessage("ReSharper", "HeapView.ObjectAllocation.Evident")]
        public AvrcLinkSpec()
        {
            signals = new List<AvrcSignal>();
        }
    }

    [Serializable]
    public struct AvrcSignal
    {
        /// <summary>
        ///     The signal name
        /// </summary>
        public string name;

        /// <summary>
        ///     The type of the value transmitted by this signal
        /// </summary>
        public AvrcSignalType type;

        /// <summary>
        ///     The direction in which this signal is transmitted
        /// </summary>
        public SyncDirection syncDirection;

        /// <summary>
        ///     Minimum value for int signals
        /// </summary>
        public int minVal;

        /// <summary>
        ///     Maximum value (inclusive) for int signals
        /// </summary>
        public int maxVal;

        internal string SignalName => $"S${name}";
        internal string AckSignalName => $"A${name}";

        /// <summary>
        ///     Name of the parameter used to trigger the non-local clone of the avatar to transmit on a two-way signal.
        /// </summary>
        /// <param name="names"></param>
        /// <returns></returns>
        internal string TxSignalFlag(AvrcNames names)
        {
            return $"_AVRC_internal_{names.Prefix}_tx_{name}";
        }
    }

    [Serializable]
    public enum SyncDirection
    {
        /// <summary>
        ///     Synced from TX -> RX only
        /// </summary>
        OneWay,

        /// <summary>
        ///     Synced bidirectionally
        /// </summary>
        TwoWay
    }

    [Serializable]
    public enum AvrcSignalType
    {
        Bool,
        Int
    }
}