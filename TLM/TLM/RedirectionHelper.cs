/*
The MIT License (MIT)
Copyright (c) 2015 Sebastian Schöner
Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
*/

using System;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;

namespace TrafficManager
{
    public static class RedirectionHelper
    {
        // Note: These two DllImports are really only used in the alternative methods
        // for detouring.
        [DllImport("mono.dll", CallingConvention = CallingConvention.FastCall, EntryPoint = "mono_domain_get")]
        private static extern IntPtr mono_domain_get();

        [DllImport("mono.dll", CallingConvention = CallingConvention.FastCall, EntryPoint = "mono_method_get_header")]
        private static extern IntPtr mono_method_get_header(IntPtr method);

        /// <summary>
        /// Redirects all calls from method 'from' to method 'to'.
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        public static void RedirectCalls(MethodInfo from, MethodInfo to)
        {
            // GetFunctionPointer enforces compilation of the method.
            var fptr1 = from.MethodHandle.GetFunctionPointer();
            var fptr2 = to.MethodHandle.GetFunctionPointer();
            Debug.Log("Patching from " + fptr1 + " to " + fptr2);
            PatchJumpTo(fptr1, fptr2);
            // We could also use:
            //RedirectCall(from, to);
        }

        /// <summary>
        /// Redirects all calls from method 'from' to method 'to'. This version works
        /// only if the 'from' method has already been compiled (use GetFunctionPointer to force
        /// this) but no function calling 'from' have already been compiled. In fact, this
        /// function forces compilation for 'to' and 'from'. 'to' and 'from' are assumed
        /// to be normal methods.
        /// After compilation, this method looks up the MonoJitInfo structs for both methods
        /// from domain->jit_info_hash and patches sets the pointer to native code to the code
        /// obtained from compiling 'to'.
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        private static void RedirectCall(MethodInfo from, MethodInfo to)
        {
            /* We assume that we are only dealing with 'normal' functions (in Mono lingua).
             * This excludes in particular:
             *  - generic functions
             *  - PInvokes
             *  - methods built at runtime
             */
            IntPtr methodPtr1 = from.MethodHandle.Value;
            IntPtr methodPtr2 = to.MethodHandle.Value;
            // ensure that both methods are compiled
            from.MethodHandle.GetFunctionPointer();
            to.MethodHandle.GetFunctionPointer();

            // get domain->jit_code_hash
            IntPtr domain = mono_domain_get();
            unsafe
            {
                byte* jitCodeHash = ((byte*)domain.ToPointer() + 0xE8);
                long** jitCodeHashTable = *(long***)(jitCodeHash + 0x20);
                uint tableSize = *(uint*)(jitCodeHash + 0x18);

                void* jitInfoFrom = null, jitInfoTo = null;

                // imitate the behavior of mono_internal_hash_table_lookup to get both MonoJitInfo ptrs
                long mptr1 = methodPtr1.ToInt64();
                uint index1 = ((uint)mptr1) >> 3;
                for (long* value = jitCodeHashTable[index1 % tableSize];
                    value != null;
                    value = *(long**)(value + 1))
                {
                    if (mptr1 == *value)
                    {
                        jitInfoFrom = value;
                        break;
                    }
                }

                long mptr2 = methodPtr2.ToInt64();
                uint index2 = ((uint)mptr2) >> 3;
                for (long* value = jitCodeHashTable[index2 % tableSize];
                    value != null;
                    value = *(long**)(value + 1))
                {
                    if (mptr2 == *value)
                    {
                        jitInfoTo = value;
                        break;
                    }
                }
                if (jitInfoFrom == null || jitInfoTo == null)
                {
                    Log.Warning("Could not find methods");
                    return;
                }


                // copy over code_start, used_regs, code_size and ignore the rest for now.
                // code_start beings at +0x10, code_size goes til +0x20
                ulong* fromPtr, toPtr;
                fromPtr = (ulong*)jitInfoFrom;
                toPtr = (ulong*)jitInfoTo;
                *(fromPtr + 2) = *(toPtr + 2);
                *(fromPtr + 3) = *(toPtr + 3);
            }
        }

        /// <summary>
        /// Primitive patching. Inserts a jump to 'target' at 'site'. Works even if both methods'
        /// callers have already been compiled.
        /// </summary>
        /// <param name="site"></param>
        /// <param name="target"></param>
        private static void PatchJumpTo(IntPtr site, IntPtr target)
        {
            // R11 is volatile.
            unsafe
            {
                byte* sitePtr = (byte*)site.ToPointer();
                *sitePtr = 0x49; // mov r11, target
                *(sitePtr + 1) = 0xBB;
                *((ulong*)(sitePtr + 2)) = (ulong)target.ToInt64();
                *(sitePtr + 10) = 0x41; // jmp r11
                *(sitePtr + 11) = 0xFF;
                *(sitePtr + 12) = 0xE3;
            }
        }

        /// <summary>
        /// Redirects calls on MSIL level. Note that this does not work over assembly
        /// boundaries, severly limiting the uses of this approach.
        /// The main problem is that IL uses tokens to identify methods. These tokens
        /// are bound to methods in the assembly metadata. In order to add calls to
        /// completely new methods (i.e. from external assemblies), one must thus
        /// edit the metadata. This method does not do this and only works for
        /// normal methods.
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        private static void RedirectCallIL(MethodInfo from, MethodInfo to)
        {
            /* We assume that we are only dealing with 'normal' functions (in Mono lingo).
             * This excludes in particular:
             *  - generic functions
             *  - PInvokes
             *  - methods built at runtime
             */
            IntPtr methodPtr1 = from.MethodHandle.Value;
            IntPtr methodPtr2 = to.MethodHandle.Value;

            // this ensures that the header of our 'to' method is loaded
            mono_method_get_header(methodPtr2);

            unsafe
            {
                byte* monoMethod1 = (byte*)methodPtr1.ToPointer();
                byte* monoMethod2 = (byte*)methodPtr2.ToPointer();
                /*
                 * Normal methods have this setup:
                 * struct _MonoMethodNormal {
                    MonoMethod method;
                    MonoMethodHeader *header;
                   };
                 * For the x64 version of Unity, this has a size of 56 bytes. We will simply
                 * change the method header pointer, which lives at +40.
                 */
                *((IntPtr*)(monoMethod1 + 40)) = *((IntPtr*)(monoMethod2 + 40)); // replace header ptr
            }
        }
    }
}
