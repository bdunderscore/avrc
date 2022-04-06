using System.Collections.Generic;

namespace net.fushizen.avrc
{
    internal class AvrcNames
    {
        internal readonly string LinkGUID;

        internal readonly string Prefix;
        internal readonly Dictionary<string, string> SignalMap;

        internal AvrcNames(AvrcBindingConfiguration binding) : this(binding.linkSpec, binding.role)
        {
            if (!string.IsNullOrEmpty(binding.layerName)) Prefix = binding.layerName;

            foreach (var nameOverride in binding.signalMappings)
            {
                if (!string.IsNullOrWhiteSpace(nameOverride.remappedParameterName))
                    SignalMap[nameOverride.avrcSignalName] = nameOverride.remappedParameterName;
            }
        }

        internal AvrcNames(AvrcLinkSpec linkSpec, Role role = Role.TX)
        {
            Prefix = linkSpec.name;
            LinkGUID = linkSpec.guid;

            SignalMap = new Dictionary<string, string>();
            foreach (var p in linkSpec.signals)
            {
                SignalMap.Add(p.name, role == Role.TX ? $"AVRC_{Prefix}_{p.name}" : p.name);
            }
        }

        internal string ObjectPath => $"AVRC/{Prefix}";
        internal string LayerPrefix => $"_AVRC_{Prefix}";
        internal string LayerSetup => $"{LayerPrefix}_Setup";
        internal string LayerProbe => $"{LayerPrefix}_Probe";

        internal string PubParamPrefix => $"AVRC_{Prefix}_";
        internal string ParamPrefix => $"_AVRCI_{Prefix}_";

        internal string PubParamEitherLocal => $"AVRC_{Prefix}_EitherLocal";
        internal string PubParamPeerPresent => $"AVRC_{Prefix}_PeerPresent";
        internal string PubParamPeerLocal => $"AVRC_{Prefix}_PeerLocal";
        internal string PubParamForceTransmit => $"AVRC_{Prefix}_ForceTx";

        internal string SignalToParam(AvrcSignal signal)
        {
            return SignalMap[signal.name];
        }

        internal string InternalParameter(AvrcSignal signal, string suffix = "")
        {
            return $"_AVRCI_{Prefix}_{signal.name}$" + suffix;
        }

        public string ParameterLayerName(AvrcSignal signal)
        {
            return $"_AVRC_{Prefix}_{signal.name}";
        }
    }
}