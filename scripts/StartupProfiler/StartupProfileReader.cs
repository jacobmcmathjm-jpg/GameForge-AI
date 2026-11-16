using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEditor.Profiling;
using UnityEngine;
public static class StartupProfileReader
{
    [Serializable]private sealed class Frame {public int index,count;public string thread;public double start,milliseconds;}
    [Serializable]private sealed class Sample {public int frame,index;public string name;public double start,milliseconds;}
    [Serializable]private sealed class Region {public string name;public int count;public double total,first,last,maximum;}
    [Serializable]private sealed class Report {public int first,last;public bool truncated;public string source;public List<Frame> frames=new List<Frame>();public List<Sample> samples=new List<Sample>();public List<Region> startupTail=new List<Region>();public int droppedRegionNames;}
    public static void Export(){
        try{
            var args=Environment.GetCommandLineArgs();var source=Value(args,"-forgecore-profile-source");var target=Value(args,"-forgecore-profile-json");
            if(!Path.IsPathRooted(source)||!Path.IsPathRooted(target)||!File.Exists(source)||File.Exists(target))throw new IOException("Existing absolute raw source and fresh JSON target required.");
            ProfilerDriver.enabled=false;ProfilerDriver.ClearAllFrames();
            if(!ProfilerDriver.LoadProfile(source,false))throw new IOException("Profiler raw load failed.");
            var report=new Report{source=source,first=ProfilerDriver.firstFrameIndex,last=ProfilerDriver.lastFrameIndex};
            if(report.first<0||report.last<report.first||report.last-report.first>1000)throw new IOException("Missing/bounded frame range.");
            for(var f=report.first;f<=report.last;f++){
                using(var data=ProfilerDriver.GetRawFrameDataView(f,0)){
                    if(!data.valid)throw new IOException("Missing main-thread data.");
                    report.frames.Add(new Frame{index=f,count=data.sampleCount,thread=data.threadName,start=data.frameStartTimeMs,milliseconds=data.frameTimeMs});
                    var tail=new Dictionary<string,Region>();
                    var tailStart=data.frameStartTimeMs+data.frameTimeMs-650;
                    for(var i=0;i<data.sampleCount;i++){
                        var ms=data.GetSampleTimeMs(i);
                        if(f==report.first){
                            var start=data.GetSampleStartTimeMs(i);
                            if(start>=tailStart){
                                var name=data.GetSampleName(i);
                                if(!tail.TryGetValue(name,out var region)){
                                    if(tail.Count>=512){report.droppedRegionNames++;continue;}
                                    region=new Region{name=name,first=start};tail.Add(name,region);
                                }
                                region.count++;region.total+=ms;region.maximum=Math.Max(region.maximum,ms);region.last=start+ms;
                            }
                        }
                        if(ms<1)continue;
                        if(report.samples.Count==20000){report.truncated=true;continue;}
                        report.samples.Add(new Sample{frame=f,index=i,name=data.GetSampleName(i),start=data.GetSampleStartTimeMs(i),milliseconds=ms});
                    }
                    if(f==report.first)report.startupTail=tail.Values.OrderByDescending(r=>r.total).Take(80).ToList();
                }
            }
            using(var writer=new StreamWriter(new FileStream(target,FileMode.CreateNew)))writer.Write(JsonUtility.ToJson(report,true));
            Debug.Log("STARTUP_PROFILE_EXPORTED "+report.frames.Count+" frames / "+report.samples.Count+" long samples");
            EditorApplication.Exit(0);
        }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
    private static string Value(string[] args,string key){var i=Array.IndexOf(args,key);if(i<0||i+1>=args.Length)throw new ArgumentException(key);return args[i+1];}
}
