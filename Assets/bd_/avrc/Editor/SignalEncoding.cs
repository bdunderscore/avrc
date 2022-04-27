using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.Contact.Components;
using VRC.SDKBase;

namespace net.fushizen.avrc
{
    internal class ContactSpec
    {
        /// <summary>
        ///     Base position written to the Contact position field
        /// </summary>
        internal Vector3 BasePosition { get; set; }

        /// <summary>
        ///     Name of the GameObject that this contact is attached to
        /// </summary>
        internal string Name { get; set; }

        /// <summary>
        ///     Collider collision tags
        /// </summary>
        internal List<string> CollisionTags { get; set; }

        /// <summary>
        ///     Do we hold the sending contact?
        /// </summary>
        internal bool IsSender { get; set; }

        /// <summary>
        ///     Parameter name to write to
        /// </summary>
        internal string Parameter { get; set; }

        internal Type ComponentType => IsSender ? typeof(VRCContactSender) : typeof(VRCContactReceiver);

        internal string SenseParameter => Parameter + "$SENSE_INDEX";

        internal void AddPresenceCondition(AnimatorStateTransition transition)
        {
            //transition.AddCondition(AnimatorConditionMode.If, 0, Parameter);
            transition.AddCondition(AnimatorConditionMode.Greater, 0.5f, Parameter);
        }
    }

    /// <summary>
    ///     Specifies where to drive a collider to to signal a value
    /// </summary>
    internal class ContactDriver
    {
        internal ContactDriver(ContactSpec target, Vector3 position, int senseIndex)
        {
            Target = target;
            Position = position;
            SenseIndex = senseIndex;
        }

        internal ContactSpec Target { get; }

        internal Vector3 Position { get; }

        internal int SenseIndex { get; }

        internal (string, int) DelayDriver => (Target.SenseParameter, -1);
        internal (string, int) ProbeDriver => (Target.SenseParameter, SenseIndex);

        internal void AddClip(AvrcNames names, AnimationClip clip, float length)
        {
            clip.SetCurve($"{names.ObjectPath}/{Target.Name}", Target.ComponentType,
                "m_Enabled", AnimationCurve.Constant(0, length, 1));
            clip.SetCurve($"{names.ObjectPath}/{Target.Name}", typeof(Transform),
                "m_LocalPosition.x", AnimationCurve.Constant(0, length, Position.x));
            clip.SetCurve($"{names.ObjectPath}/{Target.Name}", typeof(Transform),
                "m_LocalPosition.y", AnimationCurve.Constant(0, length, Position.y));
            clip.SetCurve($"{names.ObjectPath}/{Target.Name}", typeof(Transform),
                "m_LocalPosition.z", AnimationCurve.Constant(0, length, Position.z));
        }

        internal AnimationClip Clip(AvrcNames names, float length = 1)
        {
            var clip = new AnimationClip();
            AddClip(names, clip, length);
            return clip;
        }

        internal void AddCondition(AnimatorStateTransition transition)
        {
            transition.AddCondition(AnimatorConditionMode.Greater, 0.5f, Target.Parameter);
            transition.AddCondition(AnimatorConditionMode.Equals, SenseIndex, Target.SenseParameter);
        }

        internal void AddOtherValueCondition(AnimatorStateTransition transition)
        {
            transition.AddCondition(AnimatorConditionMode.Greater, 0.5f, Target.Parameter);
            transition.AddCondition(AnimatorConditionMode.NotEqual, SenseIndex, Target.SenseParameter);
            transition.AddCondition(AnimatorConditionMode.Less, 255, Target.SenseParameter);
        }
    }

    internal class ProbePhase
    {
        public ProbePhase(ImmutableList<ContactDriver> drivers, ImmutableDictionary<string, int> signalStates)
        {
            Drivers = drivers;
            SignalStates = signalStates;
        }

        internal ImmutableList<ContactDriver> Drivers { get; }
        internal ImmutableDictionary<string, int> SignalStates { get; }

        public AnimationClip ProbeClip(AvrcNames names, float length = SignalEncoding.ContactResponseTime)
        {
            var clip = new AnimationClip();

            clip.frameRate = SignalEncoding.AnimationFrameRate;

            foreach (var driver in Drivers) driver.AddClip(names, clip, length);

            return clip;
        }

        public VRCAvatarParameterDriver BuildProbeDriver(bool sample)
        {
            var paramDriver = ScriptableObject.CreateInstance<VRCAvatarParameterDriver>();
            paramDriver.localOnly = false;
            paramDriver.parameters = Drivers.Select(contactDriver =>
            {
                return new VRC_AvatarParameterDriver.Parameter
                {
                    name = contactDriver.Target.SenseParameter,
                    type = VRC_AvatarParameterDriver.ChangeType.Set,
                    value = sample ? SignalStates[contactDriver.Target.Name] : 255
                };
            }).ToList();

            return paramDriver;
        }
    }

    /// <summary>
    ///     Converts from the user-provided signal configuration to the specific object offsets and names that we'll use
    ///     to transmit the signals.
    /// </summary>
    internal class SignalEncoding
    {
        private const string PILOT_RX = "RX_PILOT";
        private const string PILOT_TX = "TX_PILOT";

        /// <summary>
        ///     Time we wait for contacts to update before reading their values. VRC internally uses a 60fps contact update
        ///     rate, so we hold the contact in position for at least 1/60fps before starting to read values (there's an
        ///     additional frame of delay built in here as well)
        /// </summary>
        internal const float ContactResponseTime = 0.01666666f;

        internal const float AnimationFrameRate = 2.0f / ContactResponseTime;

        // We use a 16m offset to ensure that we have two diameters of gap between signals.
        private static readonly Vector3 SignalOffset = new Vector3(0, 16f, 0);
        private readonly string _layerPrefix;

        private readonly AvrcLinkSpec _linkSpec;
        private readonly Role _myRole;
        private Vector3 _nextPosition;

        internal ImmutableList<ProbePhase> ProbePhases;

        internal SignalEncoding(AvrcLinkSpec linkSpec, Role role, string layerPrefix)
        {
            _linkSpec = linkSpec;
            _myRole = role;
            _nextPosition = linkSpec.baseOffset;
            _layerPrefix = layerPrefix;

            AllContacts = ImmutableList<ContactSpec>.Empty;

            var rxPilot = AllocateCollider(PILOT_RX, Role.RX);
            _nextPosition += SignalOffset;
            var txPilot = AllocateCollider(PILOT_TX, Role.TX);

            var rxPresent = new ContactDriver(rxPilot, Vector3.zero, 0);
            var txPresent = new ContactDriver(txPilot, Vector3.zero, 0);

            MyPilotNotLocal = role == Role.RX ? rxPresent : txPresent;
            TheirPilotNotLocal = role == Role.RX ? txPresent : rxPresent;

            GenerateSignalDrivers(linkSpec);

            // _nextPosition has been updated to point above the signal stack.
            var rxLocal = new ContactDriver(rxPilot, _nextPosition - linkSpec.baseOffset, 2);
            _nextPosition += SignalOffset;
            var txLocal = new ContactDriver(txPilot, _nextPosition - linkSpec.baseOffset, 2);

            // Generate a rest position to ensure that we transition through not-present even when multiple transmitters
            // are present.
            _nextPosition += SignalOffset;
            var rxRest = new ContactDriver(rxPilot, _nextPosition - linkSpec.baseOffset, 1);
            _nextPosition += SignalOffset;
            var txRest = new ContactDriver(txPilot, _nextPosition - linkSpec.baseOffset, 1);

            // Register local/present as probable values
            SignalDrivers = SignalDrivers.Add(PILOT_RX, new[] {rxPresent, rxRest, rxLocal}.ToImmutableArray());
            SignalDrivers = SignalDrivers.Add(PILOT_TX, new[] {txPresent, txRest, txLocal}.ToImmutableArray());

            GenerateProbePhases();

            MyPilotLocal = role == Role.RX ? rxLocal : txLocal;
            TheirPilotLocal = role == Role.RX ? txLocal : rxLocal;
            TheirPilotRest = role == Role.RX ? txRest : rxRest;
        }

        internal ImmutableList<ContactSpec> AllContacts { get; private set; }

        internal ContactDriver MyPilotLocal { get; }
        internal ContactDriver MyPilotNotLocal { get; }

        internal ContactDriver TheirPilotLocal { get; }
        internal ContactDriver TheirPilotNotLocal { get; }

        internal ContactDriver TheirPilotRest { get; }

        internal ImmutableDictionary<string, ImmutableArray<ContactDriver>> SignalDrivers { get; private set; }

        /// <summary>
        ///     Adds a clip which will disable all contacts.
        /// </summary>
        /// <param name="names"></param>
        /// <param name="clip"></param>
        internal void AddDisableAll(AvrcNames names, AnimationClip clip)
        {
            foreach (var collider in AllContacts)
            {
                var ty = collider.IsSender ? typeof(VRCContactSender) : typeof(VRCContactReceiver);
                clip.SetCurve($"{names.ObjectPath}/{collider.Name}", ty,
                    "m_Enabled", AnimationCurve.Constant(0, 1, 0));
            }
        }

        private void GenerateProbePhases()
        {
            var phaseCount = SignalDrivers
                .OrderBy(kvp => kvp.Value[0].Target.Name)
                .Where(drivers => !drivers.Value[0].Target.IsSender)
                .Select(drivers => drivers.Value.Length)
                .Max();
            ProbePhases = ImmutableList<ProbePhase>.Empty;

            for (var i = 0; i < phaseCount; i++)
            {
                var probeDrivers = ImmutableList<ContactDriver>.Empty;
                var signalStates = ImmutableDictionary<string, int>.Empty;

                foreach (var kvp in SignalDrivers)
                {
                    var name = kvp.Key;
                    var signalDrivers = kvp.Value;

                    if (signalDrivers[0].Target.IsSender) continue;

                    var state = i % signalDrivers.Length;
                    probeDrivers = probeDrivers.Add(signalDrivers[state]);
                    signalStates = signalStates.Add(name, state);
                }

                ProbePhases = ProbePhases.Add(new ProbePhase(probeDrivers, signalStates));
            }
        }

        private void GenerateSignalDrivers(AvrcLinkSpec linkSpec)
        {
            SignalDrivers = ImmutableDictionary<string, ImmutableArray<ContactDriver>>.Empty;

            foreach (var signal in linkSpec.signals)
            {
                var signalSteps = signal.type == AvrcSignalType.Bool
                    ? 2
                    : Math.Max(2, signal.maxVal - signal.minVal + 1);

                GenerateInnerSignalDriver(signal.SignalName, Role.TX, signalSteps);
                if (signal.syncDirection == SyncDirection.TwoWay)
                    GenerateInnerSignalDriver(signal.AckSignalName, Role.RX, signalSteps);
            }
        }

        private void GenerateInnerSignalDriver(string name, Role senderRole, int signalSteps)
        {
            var contactSpec = AllocateCollider(name, senderRole);

            var drivers = ImmutableArray<ContactDriver>.Empty;

            for (var i = 0; i < signalSteps; i++)
            {
                var driver = new ContactDriver(contactSpec, SignalOffset * i, i);
                drivers = drivers.Add(driver);
            }

            _nextPosition += SignalOffset * signalSteps;
            SignalDrivers = SignalDrivers.Add(name, drivers);
        }

        private ContactSpec AllocateCollider(string name, Role sendingRole)
        {
            var newContact = new ContactSpec();
            newContact.Name = name;
            newContact.BasePosition = _nextPosition;
            newContact.CollisionTags = new List<string>(new[] {_linkSpec.guid + "$" + name});
            newContact.IsSender = sendingRole == _myRole;
            newContact.Parameter = $"{_layerPrefix}${name}";
            AllContacts = AllContacts.Add(newContact);

            return newContact;
        }
    }
}