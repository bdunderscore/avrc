using System.Collections.Generic;

namespace net.fushizen.avrc
{
    internal class AvrcNames
    {
        internal readonly string Prefix;

        internal readonly Dictionary<string, string> ParameterMap;

        internal AvrcNames(AvrcParameters parameters)
        {
            this.Prefix = parameters.prefix;

            ParameterMap = new Dictionary<string, string>();
            foreach (var p in parameters.avrcParams)
            {
                ParameterMap.Add(p.name, $"AVRC_{Prefix}_{p.name}");
            }
        }

        internal string ObjectPath => $"AVRC/{Prefix}";
        internal string LayerPrefix => $"_AVRC_{Prefix}";
        internal string LayerSetup => $"{LayerPrefix}_Setup";
        internal string LayerRxConstraint => $"{LayerPrefix}_RXConstraint";
        internal string LayerTxEnable => $"{LayerPrefix}_TXEnable";
            
        internal string ParamRxPresent => $"_AVRC_Internal_{Prefix}_RxPresent";
        internal string ParamTxProximity => $"_AVRC_Internal_{Prefix}_TxProximity";
        internal string ParamTxActive => $"_AVRC_Internal_{Prefix}_TxActive";
        internal string ParamRxLocal => $"_AVRC_Internal_{Prefix}_RxLocal";
        internal string ParamTxLocal => $"_AVRC_Internal_{Prefix}_TxLocal";

        internal string ObjTxPresent => ObjectPath + "/$TXPresent";
        internal string ObjTxLocal => ObjectPath + "/$TXLocal";
        internal string ObjRxPresent => ObjectPath + "/$RXPresent";
        internal string ObjRxLocal => ObjectPath + "/$RXLocal";

        internal string ParameterPath(AvrcParameters.AvrcParameter parameter)
        {
            return $"{ObjectPath}/{parameter.name}";
        }

        public string ParameterLayerName(AvrcParameters.AvrcParameter parameter)
        {
            return $"_AVRC_{Prefix}_{parameter.name}";
        }
    }
}