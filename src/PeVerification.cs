using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

internal static partial class PatcherCore {
 static string VerifyPE(byte[] bytes) {
  Require(bytes.Length>=64&&bytes[0]==0x4d&&bytes[1]==0x5a,"Missing MZ");
  int pe=BitConverter.ToInt32(bytes,0x3c);Require(pe>=0&&pe+24<=bytes.Length&&BitConverter.ToUInt32(bytes,pe)==0x4550,"Missing PE");
  int optional=pe+24,size=BitConverter.ToUInt16(bytes,pe+20),count=BitConverter.ToUInt16(bytes,pe+6);
  Require(optional+size<=bytes.Length,"Invalid PE header");
  ushort magic=BitConverter.ToUInt16(bytes,optional);Require(magic==0x10b||magic==0x20b,"Invalid PE format");
  int directories=optional+(magic==0x10b?96:112);
  Require(directories+15*8<=optional+size,"Missing PE directories");
  int sections=optional+size;Require(sections+count*40<=bytes.Length,"Invalid sections");
  int Offset(uint rva,int length) {
   Require(length>=0,"Invalid resource length");
   for(int i=0;i<count;i++) {
    int s=sections+i*40;uint address=BitConverter.ToUInt32(bytes,s+12),rawSize=BitConverter.ToUInt32(bytes,s+16),raw=BitConverter.ToUInt32(bytes,s+20);
    if(rva>=address&&(ulong)(rva-address)+(uint)length<=rawSize) {
     long offset=(long)raw+rva-address;Require(offset>=0&&offset+length<=bytes.Length,"Truncated section");return checked((int)offset);
    }
   }
   throw new InvalidOperationException("Unmapped PE directory");
  }
  uint clr=BitConverter.ToUInt32(bytes,directories+14*8);int clrOffset=Offset(clr,16);
  Offset(BitConverter.ToUInt32(bytes,clrOffset+8),checked((int)BitConverter.ToUInt32(bytes,clrOffset+12)));
  uint resourceRva=BitConverter.ToUInt32(bytes,directories+2*8);int resourceSize=checked((int)BitConverter.ToUInt32(bytes,directories+2*8+4));
  Require(resourceSize>=16,"Missing native resources");int root=Offset(resourceRva,resourceSize);var hashes=new List<string>();
  var visited=new HashSet<int>();
  void Check(int relative,int length) {Require(relative>=0&&length>=0&&(long)relative+length<=resourceSize,"Bad resource offset");}
  void Walk(int relative,int depth,string path) {
   Check(relative,16);Require(depth<8&&visited.Add(relative),"Cyclic resource directory");
   int p=root+relative,n=BitConverter.ToUInt16(bytes,p+12)+BitConverter.ToUInt16(bytes,p+14);Check(relative+16,n*8);
   for(int i=0;i<n;i++) {
    int entry=p+16+i*8;uint name=BitConverter.ToUInt32(bytes,entry),target=BitConverter.ToUInt32(bytes,entry+4);
    string key;
    if((name&0x80000000)!=0) {
     int off=checked((int)(name&0x7fffffff));Check(off,2);int chars=BitConverter.ToUInt16(bytes,root+off);Check(off+2,chars*2);
     key=System.Text.Encoding.Unicode.GetString(bytes,root+off+2,chars*2);
    } else key=name.ToString();
    string next=path+"/"+key;
    int destination=checked((int)(target&0x7fffffff));
    if((target&0x80000000)!=0)Walk(destination,depth+1,next);
    else {
     Check(destination,16);int leaf=root+destination;int length=checked((int)BitConverter.ToUInt32(bytes,leaf+4));
     int data=Offset(BitConverter.ToUInt32(bytes,leaf),length);
     hashes.Add(next+":"+BitConverter.ToUInt32(bytes,leaf+8)+":"+Hash(bytes.Skip(data).Take(length).ToArray()));
    }
   }
  }
  Walk(0,0,"");Console.WriteLine("PE PASS; native resource leaves="+hashes.Count);return string.Join("|",hashes);
 }
}
