using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace NewLife.Video.Flv.Extensions;
    public static class InteropExtensions
    {
        [RequiresDynamicCode("基于 Marshal 反射封送的结构体转换需运行时生成编组代码，NativeAOT 下不可用")]
        public static T BytesToStruct<T>(Byte[] bytes, Int32 startIndex, Int32 length)
        {
            T local;
            T local2;
            if (bytes == null)
            {
                local2 = default;
                local = local2;
            }
            else if (bytes.Length <= 0)
            {
                local2 = default;
                local = local2;
            }
            else
            {
                var destination = Marshal.AllocHGlobal(length);
                try
                {
                    Marshal.Copy(bytes, startIndex, destination, length);
                    local = (T)Marshal.PtrToStructure(destination, typeof(T));
                }
                catch (Exception exception)
                {
                    throw new InvalidOperationException(L.T("Error in BytesToStruct. {0}", exception.Message));
                }
                finally
                {
                    Marshal.FreeHGlobal(destination);
                }
            }
            return local;
        }

        [RequiresDynamicCode("基于 Marshal 反射封送的结构体转换需运行时生成编组代码，NativeAOT 下不可用")]
        public static void IntPtrSetValue(IntPtr intptr, Object structObj) => IntPtrSetValue(intptr, StructToBytes(structObj));

        public static void IntPtrSetValue(IntPtr intptr, Byte[] bytes) => Marshal.Copy(bytes, 0, intptr, bytes.Length);

        [RequiresDynamicCode("基于 Marshal 反射封送的结构体转换需运行时生成编组代码，NativeAOT 下不可用")]
        public static T IntPtrToStruct<T>(IntPtr intptr)
        {
            var index = 0;
            return IntPtrToStruct<T>(intptr, index, Marshal.SizeOf(typeof(T)));
        }

        [RequiresDynamicCode("基于 Marshal 反射封送的结构体转换需运行时生成编组代码，NativeAOT 下不可用")]
        public static T IntPtrToStruct<T>(IntPtr intptr, Int32 index, Int32 length)
        {
            var destination = new Byte[length];
            Marshal.Copy(intptr, destination, index, length);
            return BytesToStruct<T>(destination, 0, destination.Length);
        }

        [RequiresDynamicCode("基于 Marshal 反射封送的结构体转换需运行时生成编组代码，NativeAOT 下不可用")]
        public static Byte[] StructToBytes(Object structObj)
        {
            var size = Marshal.SizeOf(structObj);
            return StructToBytes(structObj, size);
        }

        [RequiresDynamicCode("基于 Marshal 反射封送的结构体转换需运行时生成编组代码，NativeAOT 下不可用")]
        public static Byte[] StructToBytes(Object structObj, Int32 size)
        {
            Byte[] buffer2;
            var ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(structObj, ptr, false);
                var destination = new Byte[size];
                Marshal.Copy(ptr, destination, 0, size);
                buffer2 = destination;
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(L.T("Error in StructToBytes. {0}", exception.Message));
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
            return buffer2;
        }
    }
