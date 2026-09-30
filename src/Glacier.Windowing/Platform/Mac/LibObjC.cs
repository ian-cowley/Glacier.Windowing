namespace Glacier.Windowing.Platform.Mac;

using System;
using System.Runtime.InteropServices;

internal static unsafe partial class LibObjC
{
    private const string DllName = "/usr/lib/libobjc.A.dylib";

    [DllImport(DllName, CharSet = CharSet.Ansi)]
    public static extern IntPtr objc_getClass(string name);

    [DllImport(DllName, CharSet = CharSet.Ansi)]
    public static extern IntPtr sel_registerName(string name);

    [DllImport(DllName, EntryPoint = "objc_msgSend")]
    public static extern IntPtr objc_msgSend(IntPtr self, IntPtr op);

    [DllImport(DllName, EntryPoint = "objc_msgSend")]
    public static extern IntPtr objc_msgSend(IntPtr self, IntPtr op, IntPtr arg1);

    [DllImport(DllName, EntryPoint = "objc_msgSend")]
    public static extern IntPtr objc_msgSend(IntPtr self, IntPtr op, IntPtr arg1, IntPtr arg2);

    [DllImport(DllName, EntryPoint = "objc_msgSend")]
    public static extern IntPtr objc_msgSend(IntPtr self, IntPtr op, byte arg1);
}
