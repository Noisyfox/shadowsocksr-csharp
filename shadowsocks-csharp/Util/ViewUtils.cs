using Shadowsocks.Controller;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace Shadowsocks.Util
{
    public static class ViewUtils
    {
        public static IEnumerable<TControl> GetChildControls<TControl>(this Control control) where TControl : Control
        {
            if (control.Controls.Count == 0)
            {
                return Enumerable.Empty<TControl>();
            }

            var children = control.Controls.OfType<TControl>().ToList();
            return children.SelectMany(GetChildControls<TControl>).Concat(children);
        }


        public static MenuItem CreateMenuItem(string text, EventHandler click)
        {
            return new MenuItem(I18N.GetString(text), click);
        }

        public static MenuItem CreateMenuGroup(string text, MenuItem[] items, EventHandler click = null)
        {
            return new MenuItem(MenuMerge.Add, 0, Shortcut.None, I18N.GetString(text), click, null, null, items);
        }

        // Workaround NotifyIcon's 63 chars limit
        // https://stackoverflow.com/questions/579665/how-can-i-show-a-systray-tooltip-longer-than-63-chars
        public static void SetNotifyIconText(NotifyIcon ni, string text)
        {
            if (text.Length >= 128)
                throw new ArgumentOutOfRangeException("Text limited to 127 characters");
            Type t = typeof(NotifyIcon);
            BindingFlags hidden = BindingFlags.NonPublic | BindingFlags.Instance;
            t.GetField("text", hidden).SetValue(ni, text);
            if ((bool)t.GetField("added", hidden).GetValue(ni))
                t.GetMethod("UpdateIcon", hidden).Invoke(ni, new object[] { true });
        }
    }
}
