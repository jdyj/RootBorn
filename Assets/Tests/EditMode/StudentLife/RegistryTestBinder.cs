using System;
using System.Reflection;

namespace Rootborn.Tests.EditMode.StudentLife
{
    internal static class RegistryTestBinder
    {
        public static void Set(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) throw new MissingFieldException(target.GetType().FullName, fieldName);
            field.SetValue(target, value);
        }
    }
}
