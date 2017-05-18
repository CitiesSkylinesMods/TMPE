using CSUtil.Commons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CSUtil.Redirection {
    public abstract class RedirectAttribute : Attribute
    {
        public RedirectAttribute(Type classType, string methodName, ulong bitSetOption = 0)
        {
            ClassType = classType;
            MethodName = methodName;
            BitSetRequiredOption = bitSetOption;
        }

        public RedirectAttribute(Type classType, ulong bitSetOption = 0)
            : this(classType, null, bitSetOption)
        { }

        public Type ClassType { get; set; }
        public string MethodName { get; set; }
        public ulong BitSetRequiredOption { get; set; }
    }

    /// <summary>
    /// Marks a method for redirection. All marked methods are redirected by calling
    /// <see cref="Redirector.PerformRedirections"/> and reverted by <see cref="Redirector.RevertRedirections"/>
    /// <para>NOTE: only the methods belonging to the same assembly that calls Perform/RevertRedirections are redirected.</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class RedirectFromAttribute : RedirectAttribute
    {
        /// <param name="classType">The class of the method that will be redirected</param>
        /// <param name="methodName">The name of the method that will be redirected. If null,
        /// the name of the attribute's target method will be used.</param>
        public RedirectFromAttribute(Type classType, string methodName, ulong bitSetOption = 0)
            : base(classType, methodName, bitSetOption)
        { }

        public RedirectFromAttribute(Type classType, ulong bitSetOption = 0)
            : base(classType, bitSetOption)
        { }
    }

    /// <summary>
    /// Marks a method for redirection. All marked methods are redirected by calling
    /// <see cref="Redirector.PerformRedirections"/> and reverted by <see cref="Redirector.RevertRedirections"/>
    /// <para>NOTE: only the methods belonging to the same assembly that calls Perform/RevertRedirections are redirected.</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class RedirectToAttribute : RedirectAttribute
    {
        /// <param name="classType">The class of the target method</param>
        /// <param name="methodName">The name of the target method. If null,
        /// the name of the attribute's target method will be used.</param>
        public RedirectToAttribute(Type classType, string methodName, ulong bitSetOption = 0)
            : base(classType, methodName, bitSetOption)
        { }

        public RedirectToAttribute(Type classType, ulong bitSetOption = 0)
            : base(classType, bitSetOption)
        { }
    }

    public static class Redirector
    {
        internal class MethodRedirection : IDisposable
        {
            private bool _isDisposed = false;

            private MethodInfo _originalMethod;
            private readonly RedirectCallsState _callsState;
            public Assembly RedirectionSource { get; set; }

            public MethodRedirection(MethodInfo originalMethod, MethodInfo newMethod, Assembly redirectionSource)
            {
                _originalMethod = originalMethod;
                _callsState = RedirectionHelper.RedirectCalls(_originalMethod, newMethod);
                RedirectionSource = redirectionSource;
            }

            public void Dispose()
            {
                if (!_isDisposed)
                {
                    RedirectionHelper.RevertRedirect(_originalMethod, _callsState);
                    _originalMethod = null;
                    _isDisposed = true;
                }
            }

            public MethodInfo OriginalMethod
            {
                get
                {
                    return _originalMethod;
                }
            }
        }

        private static List<MethodRedirection> s_redirections = new List<MethodRedirection>();

        public static void PerformRedirections(ulong bitMask = 0)
        {
            Assembly callingAssembly = Assembly.GetCallingAssembly();

            IEnumerable<MethodInfo> methods = from type in callingAssembly.GetTypes()
                                              from method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                                              where method.GetCustomAttributes(typeof(RedirectAttribute), false).Length > 0
                                              select method;

            foreach (MethodInfo method in methods)
            {
                foreach (RedirectAttribute redirectAttr in method.GetCustomAttributes(typeof(RedirectAttribute), false))
                {
                    if (redirectAttr.BitSetRequiredOption != 0 && (bitMask & redirectAttr.BitSetRequiredOption) == 0)
                        continue;

                    string originalName = String.IsNullOrEmpty(redirectAttr.MethodName) ? method.Name : redirectAttr.MethodName;

                    MethodInfo originalMethod = null;
                    foreach (MethodInfo m in redirectAttr.ClassType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
                    {
                        if (m.Name != originalName)
                            continue;

                        if (method.IsCompatibleWith(m))
                        {
                            originalMethod = m;
                            break;
                        }
                    }

                    if (originalMethod == null)
                    {
                        throw new Exception(string.Format("Redirector: Original method {0} has not been found for redirection", originalName));
                    }

                    if (redirectAttr is RedirectFromAttribute)
                    {
                        if (!s_redirections.Any(r => r.OriginalMethod == originalMethod))
                        {
                            Log.Info(string.Format("Redirector: Detouring method calls from {0}.{1} to {2}.{3} via RedirectFrom",
                                originalMethod.DeclaringType,
                                originalMethod.Name,
                                method.DeclaringType,
                                method.Name));
                            s_redirections.Add(originalMethod.RedirectTo(method, callingAssembly));
                        }
                    }

                    if (redirectAttr is RedirectToAttribute)
                    {
                        if (!s_redirections.Any(r => r.OriginalMethod == method))
                        {
							Log.Info(string.Format("Redirector: Detouring method calls from {0}.{1} to {2}.{3} via RedirectTo",
                                method.DeclaringType,
                                method.Name,
                                originalMethod.DeclaringType,
                                originalMethod.Name));
                            s_redirections.Add(method.RedirectTo(originalMethod, callingAssembly));
                        }
                    }
                }
            }
        }

        public static void RevertRedirections()
        {
            Assembly callingAssembly = Assembly.GetCallingAssembly();

            for (int i = s_redirections.Count - 1; i >= 0; --i)
            {
                var redirection = s_redirections[i];

                if (Equals(redirection.RedirectionSource, callingAssembly))
                {
					Log.Info(string.Format("Redirector: Removing redirection {0}", s_redirections[i].OriginalMethod));
                    s_redirections[i].Dispose();
                    s_redirections.RemoveAt(i);
                }
            }
        }
    }
}
