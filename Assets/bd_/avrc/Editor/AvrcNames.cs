using System.Collections.Generic;

namespace net.fushizen.avrc
{
    internal class AvrcNames
    {
        internal readonly string Prefix;

        internal readonly Dictionary<string, string> ParameterMap;

        internal AvrcNames(AvrcBindingConfiguration binding) : this(binding.parameters, binding.role)
        {
            foreach (var nameOverride in binding.parameterMappings)
            {
                ParameterMap[nameOverride.avrcParameterName] = nameOverride.remappedParameterName;
            }
        }

        internal AvrcNames(AvrcParameters parameters,
            AvrcBindingConfiguration.Role role = AvrcBindingConfiguration.Role.TX)
        {
            this.Prefix = parameters.prefix;

            ParameterMap = new Dictionary<string, string>();
            foreach (var p in parameters.avrcParams)
            {
                ParameterMap.Add(p.name, role == AvrcBindingConfiguration.Role.TX ? $"AVRC_{Prefix}_{p.name}" : p.name);
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
        
        internal string ParamRxPresent => $"_AVRCI_{Prefix}_RxPresent";
        internal string ParamTxProximity => $"_AVRCI_{Prefix}_TxProximity";
        internal string ParamTxActive => $"_AVRCI_{Prefix}_TxActive";
        internal string ParamRxLocal => $"_AVRCI_{Prefix}_RxLocal";
        internal string ParamTxLocal => $"_AVRCI_{Prefix}_TxLocal";

        internal string ObjTxPresent => ObjectPath + "/$TXPresent";
        internal string ObjTxLocal => ObjectPath + "/$TXLocal";
        internal string ObjRxPresent => ObjectPath + "/$RXPresent";
        internal string ObjRxLocal => ObjectPath + "/$RXLocal";

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
    }
}