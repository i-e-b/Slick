using System;
using System.Reflection;

namespace SlickUWP.Adaptors
{
    public static class EventExtensions
    {
        public static void ClearEventInvocations(this object obj, string eventName)
        {
            if (obj == null || string.IsNullOrWhiteSpace(eventName)) return;
            var fi = obj.GetType().GetEventField(eventName);
            if (fi == null) return;
            fi.SetValue(obj, null);
        }

        private static FieldInfo GetEventField(this Type type, string eventName)
        {
            if (type == null || string.IsNullOrWhiteSpace(eventName)) return null;
            FieldInfo field = null;
            while (type != null)
            {
                /* Find events defined as field */
                field = type.GetField(eventName, BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null && (field.FieldType == typeof(MulticastDelegate) || field.FieldType.IsSubclassOf(typeof(MulticastDelegate))))
                    break;
                
                /* Find events defined as public field */
                field = type.GetField(eventName, BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public);
                if (field != null && (field.FieldType == typeof(MulticastDelegate) || field.FieldType.IsSubclassOf(typeof(MulticastDelegate))))
                    break;

                /* Find events defined as property { add; remove; } */
                field = type.GetField("EVENT_" + eventName.ToUpper(), BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null)
                    break;
                type = type.BaseType;
            }
            return field;
        }
    }
}