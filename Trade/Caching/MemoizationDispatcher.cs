using System;
using System.Linq;
using System.Reflection;

namespace Trade.Caching
{
    /// <summary>
    /// Optional reflection-based dispatcher to call a target method with memoization if it is
    /// decorated with [Memoize]. This lets you adopt caching without any 3rd-party IL weaver.
    /// Usage: MemoizationDispatcher.Invoke(this, MethodBase.GetCurrentMethod(), new object[]{...}, (a)=>{ ... body ... });
    /// For production, you can replace this with compile-time IL weaving.
    /// </summary>
    public static class MemoizationDispatcher
    {
        public static object Invoke(object instance, MethodBase method, object[] args, Func<object[], object> target)
        {
            var memo = method.GetCustomAttributes(typeof(MemoizeAttribute), inherit: false)
                             .OfType<MemoizeAttribute>()
                             .FirstOrDefault();
            if (memo == null)
            {
                return target(args);
            }

            return MemoizationHelper.InvokeWithCache(instance, method, args, target, memo);
        }
    }
}
