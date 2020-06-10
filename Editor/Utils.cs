// #define PERFORMANCE_TRACKING_DEBUG
using JetBrains.Annotations;
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Unity.PerformanceTracking
{
    internal static class Utils
    {
        public static string packageName = "com.unity.performance-tracking";
        public static string packageFolderName = $"Packages/{packageName}";

        private static string[] _ignoredAssemblies =
        {
            "^UnityScript$", "^System$", "^mscorlib$", "^netstandard$",
            "^System\\..*", "^nunit\\..*", "^Microsoft\\..*", "^Mono\\..*", "^SyntaxTree\\..*"
        };

        public static string ColorToHexCode(Color color)
        {
            var r = (int)(color.r * 255);
            var g = (int)(color.g * 255);
            var b = (int)(color.b * 255);
            return $"#{r:X2}{g:X2}{b:X2}";
        }

        public static void DelayCall(float seconds, System.Action callback)
        {
            DelayCall(EditorApplication.timeSinceStartup, seconds, callback);
        }

        public static void DelayCall(double timeStart, float seconds, System.Action callback)
        {
            var dt = EditorApplication.timeSinceStartup - timeStart;
            if (dt >= seconds)
                callback();
            else
                EditorApplication.delayCall += () => DelayCall(timeStart, seconds, callback);
        }


#if UNITY_EDITOR
        [InitializeOnLoad]
        #endif
        internal static class UnityVersion
        {
            enum Candidate
            {
                Dev = 0,
                Alpha = 1 << 8,
                Beta = 1 << 16,
                Final = 1 << 24
            }

            static UnityVersion()
            {
                var version = Application.unityVersion.Split('.');

                if (version.Length < 2)
                {
                    Console.WriteLine("Could not parse current Unity version '" + Application.unityVersion + "'; not enough version elements.");
                    return;
                }

                if (int.TryParse(version[0], out Major) == false)
                {
                    Console.WriteLine("Could not parse major part '" + version[0] + "' of Unity version '" + Application.unityVersion + "'.");
                }

                if (int.TryParse(version[1], out Minor) == false)
                {
                    Console.WriteLine("Could not parse minor part '" + version[1] + "' of Unity version '" + Application.unityVersion + "'.");
                }

                if (version.Length >= 3)
                {
                    try
                    {
                        Build = ParseBuild(version[2]);
                    }
                    catch
                    {
                        Console.WriteLine("Could not parse minor part '" + version[1] + "' of Unity version '" + Application.unityVersion + "'.");
                    }
                }

                #if PERFORMANCE_TRACKING_DEBUG
                Debug.Log($"Unity {Major}.{Minor}.{Build}");
                #endif
            }

            public static int ParseBuild(string build)
            {
                var rev = 0;
                if (build.Contains("a"))
                    rev = (int)Candidate.Alpha;
                else if (build.Contains("b"))
                    rev = (int)Candidate.Beta;
                if (build.Contains("f"))
                    rev = (int)Candidate.Final;
                var tags = build.Split('a', 'b', 'f', 'p', 'x');
                if (tags.Length == 2)
                {
                    rev += Convert.ToInt32(tags[0], 10) << 4;
                    rev += Convert.ToInt32(tags[1], 10);
                }
                return rev;
            }

            public static bool IsVersionGreaterOrEqual(int major, int minor)
            {
                if (Major > major)
                    return true;
                if (Major == major)
                {
                    if (Minor >= minor)
                        return true;
                }

                return false;
            }

            public static bool IsVersionGreaterOrEqual(int major, int minor, int build)
            {
                if (Major > major)
                    return true;
                if (Major == major)
                {
                    if (Minor > minor)
                        return true;

                    if (Minor == minor)
                    {
                        if (Build >= build)
                            return true;
                    }
                }

                return false;
            }

            public static readonly int Major;
            public static readonly int Minor;
            public static readonly int Build;
        }

        internal static string JsonSerialize(object obj)
        {
            var assembly = typeof(Selection).Assembly;
            var managerType = assembly.GetTypes().First(t => t.Name == "Json");
            var method = managerType.GetMethod("Serialize", BindingFlags.Public | BindingFlags.Static);
            var jsonString = "";
            if (UnityVersion.IsVersionGreaterOrEqual(2019, 1, UnityVersion.ParseBuild("0a10")))
            {
                var arguments = new object[] { obj, false, "  " };
                jsonString = method.Invoke(null, arguments) as string;
            }
            else
            {
                var arguments = new object[] { obj };
                jsonString = method.Invoke(null, arguments) as string;
            }
            return jsonString;
        }

        internal static object JsonDeserialize(object obj)
        {
            var assembly = typeof(Selection).Assembly;
            var managerType = assembly.GetTypes().First(t => t.Name == "Json");
            var method = managerType.GetMethod("Deserialize", BindingFlags.Public | BindingFlags.Static);
            var arguments = new object[] { obj };
            return method.Invoke(null, arguments);
        }

        internal static bool IsDeveloperMode()
        {
            #if PERFORMANCE_TRACKING_DEBUG
            return true;
            #else
            return Directory.Exists($"{packageFolderName}/.git");
            #endif
        }

        internal static string GetPerformanceTrackingVersion()
        {
            string version = null;
            try
            {
                var filePath = File.ReadAllText($"{packageFolderName}/package.json");
                if (JsonDeserialize(filePath) is Dictionary<string, object> manifest && manifest.ContainsKey("version"))
                {
                    version = manifest["version"] as string;
                }
            }
            catch (Exception)
            {
                // ignored
            }

            return version ?? "unknown";
        }

        private static bool IsIgnoredAssembly(AssemblyName assemblyName)
        {
            var name = assemblyName.Name;
            return _ignoredAssemblies.Any(candidate => Regex.IsMatch(name, candidate));
        }

        public static IEnumerable<Type> GetLoadableTypes(this Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                return e.Types.Where(t => t != null);
            }
        }

        internal static MethodInfo[] GetAllStaticMethods(this AppDomain aAppDomain, bool showInternalAPIs)
        {
            var result = new List<MethodInfo>();
            var assemblies = aAppDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                if (IsIgnoredAssembly(assembly.GetName()))
                    continue;
#if QUICKSEARCH_DEBUG
                var countBefore = result.Count;
#endif
                var types = assembly.GetLoadableTypes();
                foreach (var type in types)
                {
                    var methods = type.GetMethods(BindingFlags.Static | (showInternalAPIs ? BindingFlags.Public | BindingFlags.NonPublic : BindingFlags.Public) | BindingFlags.DeclaredOnly);
                    foreach (var m in methods)
                    {
                        if (m.IsPrivate)
                            continue;

                        if (m.IsGenericMethod)
                            continue;

                        if (m.Name.Contains("Begin") || m.Name.Contains("End"))
                            continue;

                        if (m.GetParameters().Length == 0)
                            result.Add(m);
                    }
                }
#if QUICKSEARCH_DEBUG
                Debug.Log($"{result.Count - countBefore} - {assembly.GetName()}");
#endif
            }
            return result.ToArray();
        }

        internal static IEnumerable<MethodInfo> GetAllMethodsWithAttribute<T>(BindingFlags bindingFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) where T : System.Attribute
        {
#if UNITY_2019_2_OR_NEWER
            return TypeCache.GetMethodsWithAttribute<T>();
#else
            Assembly assembly = typeof(Selection).Assembly;
            var managerType = assembly.GetTypes().First(t => t.Name == "EditorAssemblies");
            var method = managerType.GetMethod("Internal_GetAllMethodsWithAttribute", BindingFlags.NonPublic | BindingFlags.Static);
            var arguments = new object[] { typeof(T), bindingFlags };
            return ((method.Invoke(null, arguments) as object[]) ?? throw new InvalidOperationException()).Cast<MethodInfo>();
#endif
        }
    }
}
