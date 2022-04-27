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

using System;
using UnityEngine;

namespace net.fushizen.avrc
{
    internal static class AvrcUI
    {
        public static Rect AdvanceRect(ref Rect rect, Single width, Single padBefore = 0, Single padAfter = 0)
        {
            var r = rect;
            r.width = width;
            r.x += padBefore;
            rect.x += width + padBefore + padAfter;
            rect.width -= width + padBefore + padAfter;

            return r;
        }

        public static void RenderLabel(
            ref Rect rect,
            GUIContent content,
            GUIStyle style = null,
            Single padBefore = 0,
            Single padAfter = 0)
        {
            if (style == null)
            {
                style = GUI.skin.label;
            }

            style = new GUIStyle(style)
            {
                alignment = TextAnchor.MiddleCenter
            };

            Vector2 size = style.CalcSize(content);
            var r = AdvanceRect(ref rect, size.x, padBefore, padAfter);

            r.y -= (r.height - size.y) / 2 - 1;

            GUI.Label(r, content, style);
        }

        public static void RenderLabel(
            ref Rect rect,
            string content,
            GUIStyle style = null,
            Single padBefore = 0,
            Single padAfter = 0
        )
        {
            RenderLabel(ref rect, new GUIContent(content), style, padBefore, padAfter);
        }
    }
}