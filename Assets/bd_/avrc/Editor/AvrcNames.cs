using System.Collections.Generic;

namespace net.fushizen.avrc
{
    internal class ContactSpec
    {
        private readonly AvrcNames _names;
        private readonly string _suffix;

        public ContactSpec(AvrcNames names, string suffix)
        {
            _names = names;
            _suffix = suffix;
        }

        internal string ObjectName => $"_AVRCI_{_names.Prefix}_{_suffix}";
        internal string ParamName => ObjectName;
        internal string TagName => $"_AVRCI_{_names.LinkGUID}_{_suffix}";
    }

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

        internal string PubParamPrefix => $"AVRC_{Prefix}_";
        internal string ParamPrefix => $"_AVRCI_{Prefix}_";

        internal string PubParamEitherLocal => $"AVRC_{Prefix}_EitherLocal";
        internal string PubParamPeerPresent => $"AVRC_{Prefix}_PeerPresent";
        internal string PubParamPeerLocal => $"AVRC_{Prefix}_PeerLocal";

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

        public ContactSpec[] PilotContacts(Role role)
        {
            var rolePrefix = role == Role.RX ? "RX" : "TX";
            return new[]
            {
                new ContactSpec(this, role + "Pilot1"),
                new ContactSpec(this, role + "Pilot2")
            };
        }

        public ContactSpec LocalContacts(Role role)
        {
            return new ContactSpec(this, role + "Local");
        }

        public ContactSpec[] SignalContacts(AvrcSignal signal, bool ack)
        {
            var suffix = ack ? "$ACK" : "";
            var values = signal.type == AvrcSignalType.Bool
                ? 2
                : signal.maxVal - signal.minVal + 1;

            var signals = new List<ContactSpec>();
            var bits = 0;
            while (values > 0)
            {
                signals.Add(new ContactSpec(this, $"B{bits}_{signal.name}{suffix}"));
                bits++;
                values = values >> 1;
            }

            return signals.ToArray();
        }
    }
}