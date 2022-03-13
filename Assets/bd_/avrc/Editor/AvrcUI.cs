using System;
using UnityEngine;

namespace net.fushizen.avrc
{
    internal sealed class AvrcUI
    {
        private AvrcUI()
        {
        }


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