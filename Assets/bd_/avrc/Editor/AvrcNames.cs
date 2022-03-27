using System.Collections.Generic;

namespace net.fushizen.avrc
{
    internal class Signal
    {
        private readonly AvrcNames _names;
        private readonly string _suffix;

        public Signal(AvrcNames names, string suffix)
        {
            _names = names;
            _suffix = suffix;
        }

        internal string ObjectName => $"_AVRCI_{_names.Prefix}_{_suffix}";
        internal string ParamName => ObjectName;
        internal string TagName => $"_AVRCI_{_names.ParamsGUID}_{_suffix}";
    }

    internal class AvrcNames
    {
        internal readonly Dictionary<string, string> ParameterMap;
        internal readonly string ParamsGUID;

        internal readonly string Prefix;

        internal AvrcNames(AvrcBindingConfiguration binding) : this(binding.parameters, binding.role)
        {
            if (!string.IsNullOrEmpty(binding.layerName)) Prefix = binding.layerName;

            foreach (var nameOverride in binding.parameterMappings)
            {
                if (!string.IsNullOrWhiteSpace(nameOverride.remappedParameterName))
                    ParameterMap[nameOverride.avrcParameterName] = nameOverride.remappedParameterName;
            }
        }

        internal AvrcNames(AvrcParameters parameters, Role role = Role.TX)
        {
            Prefix = parameters.name;
            ParamsGUID = parameters.guid;

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

        public Signal[] SignalPilots(Role role)
        {
            var rolePrefix = role == Role.RX ? "RX" : "TX";
            return new[]
            {
                new Signal(this, role + "Pilot1"),
                new Signal(this, role + "Pilot2")
            };
        }

        public Signal SignalLocal(Role role)
        {
            return new Signal(this, role + "Local");
        }

        public string SignalLocalTag(Role role)
        {
            return $"_AVRCI_{ParamsGUID}_{role.ToString()}Local";
        }

        public Signal[] SignalParam(AvrcParameters.AvrcParameter parameter, bool ack)
        {
            var suffix = ack ? "$ACK" : "";
            var values = parameter.type == AvrcParameters.AvrcParameterType.Bool
                ? 2
                : parameter.maxVal - parameter.minVal + 1;

            var signals = new List<Signal>();
            var bits = 0;
            while (values > 0)
            {
                signals.Add(new Signal(this, $"B{bits}_{parameter.name}{suffix}"));
                bits++;
                values = values >> 1;
            }

            return signals.ToArray();
        }
    }
}