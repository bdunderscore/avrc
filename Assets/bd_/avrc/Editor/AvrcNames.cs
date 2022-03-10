namespace net.fushizen.avrc
{
    internal class AvrcNames
    {
        internal readonly string Prefix;

        internal AvrcNames(AvrcParameters parameters)
        {
            this.Prefix = parameters.prefix;
        }

        internal string ObjectPath => $"AVRC/{Prefix}";
        internal string LayerPrefix => $"_AVRC_{Prefix}";
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