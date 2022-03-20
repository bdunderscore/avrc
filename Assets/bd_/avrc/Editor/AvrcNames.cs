using System.Collections.Generic;

namespace net.fushizen.avrc
{
    internal class AvrcNames
    {
        internal readonly Dictionary<string, string> ParameterMap;

        internal readonly string Prefix;

        internal AvrcNames(AvrcBindingConfiguration binding) : this(binding.parameters, binding.role)
        {
            foreach (var nameOverride in binding.parameterMappings)
            {
                if (!string.IsNullOrWhiteSpace(nameOverride.remappedParameterName))
                    ParameterMap[nameOverride.avrcParameterName] = nameOverride.remappedParameterName;
            }
        }

        internal AvrcNames(AvrcParameters parameters,
            Role role = Role.TX)
        {
            this.Prefix = parameters.prefix;

            ParameterMap = new Dictionary<string, string>();
            foreach (var p in parameters.avrcParams)
            {
                ParameterMap.Add(p.name, role == Role.TX ? $"AVRC_{Prefix}_{p.name}" : p.name);
            }
        }

        internal string ObjectPath => $"AVRC/{Prefix}";
        internal string LayerPrefix => $"_AVRC_{Prefix}";
        internal string LayerSetup => $"{LayerPrefix}_Setup";
        internal string LayerRxConstraint => $"{LayerPrefix}_RXConstraint";
        internal string LayerTxEnable => $"{LayerPrefix}_Base";

        internal string PubParamPrefix => $"AVRC_{Prefix}_";
        internal string ParamPrefix => $"_AVRCI_{Prefix}_";

        internal string PubParamEitherLocal => $"AVRC_{Prefix}_EitherLocal";
        internal string PubParamPeerPresent => $"AVRC_{Prefix}_PeerPresent";
        internal string PubParamPeerLocal => $"AVRC_{Prefix}_PeerLocal";

        internal string ParameterPath(AvrcParameters.AvrcParameter parameter)
        {
            return $"{ObjectPath}/{parameter.name}";
        }

        internal string UserParameter(AvrcParameters.AvrcParameter parameter)
        {
            return ParameterMap[parameter.name];
        }

        internal string InternalParameter(AvrcParameters.AvrcParameter parameter, string suffix = "")
        {
            return $"_AVRCI_{Prefix}_{parameter.name}$" + suffix;
        }

        public string ParameterLayerName(AvrcParameters.AvrcParameter parameter)
        {
            return $"_AVRC_{Prefix}_{parameter.name}";
        }

        public string[] SignalPilots(Role role)
        {
            var prefix = role == Role.RX ? "RX" : "TX";
            return new[]
            {
                $"_AVRCI_{Prefix}_{prefix}Pilot1",
                $"_AVRCI_{Prefix}_{prefix}Pilot2"
            };
        }

        public string SignalLocal(Role role)
        {
            return $"_AVRCI_{Prefix}_{role.ToString()}Local";
        }

        public string[] SignalParam(AvrcParameters.AvrcParameter parameter, bool ack)
        {
            var suffix = ack ? "$ACK" : "";
            var values = parameter.type == AvrcParameters.AvrcParameterType.Bool
                ? 2
                : parameter.maxVal - parameter.minVal + 1;

            var signals = new List<string>();
            var bits = 0;
            while (values > 0)
            {
                signals.Add($"_AVRCI_{Prefix}_B{bits}_{parameter.name}{suffix}");
                bits++;
                values = values >> 1;
            }

            return signals.ToArray();
        }
    }
}