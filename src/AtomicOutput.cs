using System;
using System.IO;

internal static class AtomicOutput {
 internal static void Write(byte[] bytes,string output,bool overwrite) {
  output=Path.GetFullPath(output);
  string directory=Path.GetDirectoryName(output);Directory.CreateDirectory(directory);
  string temp=Path.Combine(directory,".pclunlocker-"+Guid.NewGuid().ToString("N")+".tmp");
  try {
   using(var file=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){file.Write(bytes,0,bytes.Length);file.Flush(true);}
   if(File.Exists(output)) {
    if(!overwrite)throw new IOException("Output exists; use --force to replace it");
    // Never delete the old output before replacement. Failure preserves it.
    File.Replace(temp,output,null);
   } else File.Move(temp,output);
  } finally {if(File.Exists(temp))File.Delete(temp);}
 }
}
