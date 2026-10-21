using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

internal static class FileIdentity {
 [StructLayout(LayoutKind.Sequential)]struct Info {
  internal uint Attributes;
  internal System.Runtime.InteropServices.ComTypes.FILETIME Creation,Access,Write;
  internal uint Volume,SizeHigh,SizeLow,Links,IndexHigh,IndexLow;
 }
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool GetFileInformationByHandle(SafeFileHandle handle,out Info info);
 internal static void RequireDifferent(FileStream input,string output) {
  using(var other=new FileStream(output,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)) {
   if(!GetFileInformationByHandle(input.SafeFileHandle,out var a)||!GetFileInformationByHandle(other.SafeFileHandle,out var b))
    throw new IOException("Cannot establish file identity; refusing to write");
   if(a.Volume==b.Volume&&a.IndexHigh==b.IndexHigh&&a.IndexLow==b.IndexLow)
    throw new IOException("Output resolves to the original file; links and aliases are protected");
  }
 }
}
