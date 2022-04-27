/*
 * MIT License
 * 
 * Copyright (c) 2021-2022 bd_
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE. 
 */

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
        internal string ParamPrefix => $"_AVRC_{Prefix}_";
        internal string ParamSecretActive => $"_AVRC_{Prefix}_SecretActive";

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
            return ParamPrefix + signal.name + suffix;
        }

        public string ParameterLayerName(AvrcSignal signal)
        {
            return $"{LayerPrefix}_{signal.name}";
        }
    }
}