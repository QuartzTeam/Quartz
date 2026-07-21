using System;
using System.Linq;
using System.Reflection;
namespace Quartz.Compat.Game;
internal static class Refl {
    internal const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic
        | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy;
    internal static Type Type(string name) {
        if(string.IsNullOrEmpty(name)) return null;
        try {
            return typeof(ADOBase).Assembly.GetType(name)
                ?? typeof(ADOBase).Assembly.GetType("ADOFAI." + name);
        } catch {
            return null;
        }
    }
    internal sealed class Member {
        private readonly PropertyInfo prop;
        private readonly FieldInfo fieldInfo;
        internal Member(Type owner, params string[] names) {
            if(owner == null) return;
            foreach(string n in names) {
                try {
                    prop = owner.GetProperty(n, Any);
                } catch(AmbiguousMatchException) {
                    prop = Walk(owner, t => t.GetProperty(n, Declared));
                }
                if(prop != null && prop.GetIndexParameters().Length == 0) return;
                prop = null;
                try {
                    fieldInfo = owner.GetField(n, Any);
                } catch(AmbiguousMatchException) {
                    fieldInfo = Walk(owner, t => t.GetField(n, Declared));
                }
                if(fieldInfo != null) return;
            }
        }
        private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic
            | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        private static T Walk<T>(Type owner, Func<Type, T> pick) where T : class {
            for(Type t = owner; t != null; t = t.BaseType) {
                T found = null;
                try { found = pick(t); } catch { }
                if(found != null) return found;
            }
            return null;
        }
        internal bool Exists => prop != null || fieldInfo != null;
        internal object Get(object instance) {
            try {
                if(prop != null) return prop.CanRead ? prop.GetValue(instance, null) : null;
                return fieldInfo?.GetValue(instance);
            } catch {
                return null;
            }
        }
        internal void Set(object instance, object value) {
            try {
                if(prop != null) {
                    if(prop.CanWrite) prop.SetValue(instance, value, null);
                    return;
                }
                fieldInfo?.SetValue(instance, value);
            } catch {
            }
        }
        internal T Get<T>(object instance, T fallback = default) =>
            Get(instance) is T t ? t : fallback;
    }
    internal static MethodInfo Method(Type owner, string name, int argCount = -1) {
        if(owner == null || string.IsNullOrEmpty(name)) return null;
        try {
            MethodInfo[] all = owner.GetMethods(Any).Where(m => m.Name == name).ToArray();
            if(all.Length == 0) return null;
            if(argCount < 0) return all[0];
            return all.FirstOrDefault(m => m.GetParameters().Length == argCount)
                ?? all.FirstOrDefault(m => m.GetParameters().Length >= argCount
                    && m.GetParameters().Skip(argCount).All(p => p.IsOptional))
                ?? all[0];
        } catch {
            return null;
        }
    }
    internal static object Invoke(MethodInfo m, object instance, params object[] args) {
        if(m == null) return null;
        try {
            ParameterInfo[] ps = m.GetParameters();
            if(args.Length < ps.Length) {
                object[] padded = new object[ps.Length];
                Array.Copy(args, padded, args.Length);
                for(int i = args.Length; i < ps.Length; i++)
                    padded[i] = ps[i].HasDefaultValue ? ps[i].DefaultValue : null;
                args = padded;
            }
            return m.Invoke(instance, args);
        } catch {
            return null;
        }
    }
}
