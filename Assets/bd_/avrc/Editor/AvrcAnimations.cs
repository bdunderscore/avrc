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

using UnityEngine;
using UnityEngine.Animations;

namespace net.fushizen.avrc
{
    internal class AvrcAnimations
    {
        private const float BOUNDS_SIZE = 1000f;

        private readonly AvrcLinkSpec _linkSpec;
        private readonly AvrcNames _names;

        public AvrcAnimations(AvrcLinkSpec linkSpec, AvrcNames names)
        {
            _linkSpec = linkSpec;
            this._names = names;
        }

        internal AnimationClip Named(string name, GetClipDelegate d)
        {
            var clip = d();
            clip.name = name;
            return clip;
        }

        internal AnimationClip GlobalDefaultsClip()
        {
            var clip = new AnimationClip();
            clip.SetCurve("AVRC/AVRC_Bounds", typeof(Transform), "m_LocalScale.x",
                AnimationCurve.Constant(0, 1, 0.01f)
            );
            clip.SetCurve("AVRC/AVRC_Bounds", typeof(Transform), "m_LocalScale.y",
                AnimationCurve.Constant(0, 1, 0.01f)
            );
            clip.SetCurve("AVRC/AVRC_Bounds", typeof(Transform), "m_LocalScale.z",
                AnimationCurve.Constant(0, 1, 0.01f)
            );

            return clip;
        }

        internal AnimationClip PresenceClip(LocalState local, SignalEncoding signalEncoding)
        {
            var path = _names.ObjectPath;
            AnimationClip clip = new AnimationClip();
            clip.SetCurve(path, typeof(ParentConstraint), "m_Active", AnimationCurve.Constant(0, 1, 1));

            signalEncoding.AddDisableAll(_names, clip);

            if (local == LocalState.OwnerLocal)
                signalEncoding.MyPilotLocal.AddClip(_names, clip, 1);
            else
                signalEncoding.MyPilotNotLocal.AddClip(_names, clip, 1);

            // TODO: Control parameter contact activation?
            return clip;
        }

        internal delegate AnimationClip GetClipDelegate();

        internal enum LocalState
        {
            Unknown,
            OwnerLocal,
            PeerLocal
        }
    }
}