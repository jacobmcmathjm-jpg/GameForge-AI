using System.Buffers.Binary;
using System.Text.Json;

namespace ForgeCore.Tools;

// Deliberately narrower than general glTF: only this bundled recipe's dense, uncompressed skin.
// Structural container validation is performed first by StagedAssetInspector.
internal static class SkinnedGlbInspector
{
    internal static readonly string[] RequiredBones = ["Hips","Spine","Chest","Neck","Head",
        "Clavicle.L","UpperArm.L","LowerArm.L","Hand.L","UpperLeg.L","LowerLeg.L","Foot.L","Toe.L",
        "Clavicle.R","UpperArm.R","LowerArm.R","Hand.R","UpperLeg.R","LowerLeg.R","Foot.R","Toe.R"];

    public static async Task ValidateAsync(string path,string manifestPath,CancellationToken token)
    {
        try { await ValidateCoreAsync(path,manifestPath,token); }
        catch(Exception ex) when(ex is KeyNotFoundException or IndexOutOfRangeException or OverflowException or InvalidOperationException or ArgumentException)
        { throw new InvalidDataException("Character skin data is missing, malformed or out of range.",ex); }
    }
    private static async Task ValidateCoreAsync(string path,string manifestPath,CancellationToken token)
    {
        var bytes=await File.ReadAllBytesAsync(path,token);
        var jsonLength=checked((int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(12,4)));
        using var doc=JsonDocument.Parse(bytes.AsMemory(20,jsonLength)); var root=doc.RootElement;
        var binHeader=20+jsonLength;
        if(binHeader+8>bytes.Length || BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(binHeader+4,4))!=0x004e4942)
            throw new InvalidDataException("Character needs embedded binary skin data.");
        var binLength=checked((int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(binHeader,4)));
        if(binHeader+8L+binLength!=bytes.Length) throw new InvalidDataException("Character binary chunk bounds do not match.");
        var binary=bytes.AsMemory(binHeader+8,binLength);
        var skins=root.GetProperty("skins"); var nodes=root.GetProperty("nodes");
        if(skins.GetArrayLength()!=1) throw new InvalidDataException("Character requires one explicit skin.");
        var skin=skins[0]; var joints=skin.GetProperty("joints").EnumerateArray().Select(x=>x.GetInt32()).ToArray();
        if(joints.Length is < 21 or > 64 || joints.Distinct().Count()!=joints.Length || joints.Any(x=>x<0 || x>=nodes.GetArrayLength()))
            throw new InvalidDataException("Invalid character joint indices.");
        var names=joints.Select(i=>nodes[i].GetProperty("name").GetString()!).ToArray();
        if(names.Distinct(StringComparer.Ordinal).Count()!=names.Length || RequiredBones.Except(names,StringComparer.Ordinal).Any())
            throw new InvalidDataException("Character is missing its named humanoid bone contract.");
        // Every non-root bone must have exactly one parent; traversal must terminate, never cycle.
        var parents=new Dictionary<int,int>();
        for(var n=0;n<nodes.GetArrayLength();n++)
            if(nodes[n].TryGetProperty("children",out var children)) foreach(var child in children.EnumerateArray())
            {var i=child.GetInt32();if(i<0 || i>=nodes.GetArrayLength() || !parents.TryAdd(i,n)) throw new InvalidDataException("Invalid skeleton hierarchy.");}
        foreach(var joint in joints)
        {var seen=new HashSet<int>();var n=joint;while(parents.TryGetValue(n,out var parent)){if(!seen.Add(n))throw new InvalidDataException("Cyclic skeleton.");n=parent;}}
        var accessors=root.GetProperty("accessors"); var views=root.GetProperty("bufferViews");
        (int Count,int Offset,int Stride,int Size) Accessor(int index,string type,int component,int components)
        {
            if(index<0 || index>=accessors.GetArrayLength()) throw new InvalidDataException("Invalid skin accessor.");
            var a=accessors[index];
            if(a.TryGetProperty("sparse",out _) || a.GetProperty("componentType").GetInt32()!=component || a.GetProperty("type").GetString()!=type ||
                a.TryGetProperty("normalized",out var norm) && norm.GetBoolean()) throw new InvalidDataException("Unsupported skin accessor encoding.");
            var vi=a.GetProperty("bufferView").GetInt32(); if(vi<0 || vi>=views.GetArrayLength())throw new InvalidDataException("Invalid skin buffer view.");
            var v=views[vi];if(v.GetProperty("buffer").GetInt32()!=0)throw new InvalidDataException("External skin buffer.");
            var size=components*(component==5121?1:component==5123?2:4);
            var viewOffset=v.TryGetProperty("byteOffset",out var vo)?vo.GetInt32():0;
            var accessorOffset=a.TryGetProperty("byteOffset",out var ao)?ao.GetInt32():0;
            var stride=v.TryGetProperty("byteStride",out var st)?st.GetInt32():size;
            var count=a.GetProperty("count").GetInt32();var length=v.GetProperty("byteLength").GetInt32();
            if(count is < 1 or > 200000 || viewOffset<0 || accessorOffset<0 || stride<size || stride>256 || length<0 ||
                viewOffset+(long)length>binary.Length || accessorOffset+(long)(count-1)*stride+size>length)
                throw new InvalidDataException("Skin accessor exceeds bounded binary data.");
            return(count,checked(viewOffset+accessorOffset),stride,size);
        }
        double FloatAt(int offset) => BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(binary.Span.Slice(offset,4)));
        var bind=Accessor(skin.GetProperty("inverseBindMatrices").GetInt32(),"MAT4",5126,16);
        if(bind.Count!=joints.Length)throw new InvalidDataException("Bind-pose/joint counts disagree.");
        for(var i=0;i<bind.Count;i++) for(var c=0;c<16;c++)
            if(!double.IsFinite(FloatAt(bind.Offset+i*bind.Stride+c*4)))throw new InvalidDataException("Nonfinite bind matrix.");
        var meshNodes=nodes.EnumerateArray().Where(n=>n.TryGetProperty("mesh",out _)).ToArray();
        if(meshNodes.Length!=1 || meshNodes[0].GetProperty("skin").GetInt32()!=0)throw new InvalidDataException("Character mesh is not bound to the declared skin.");
        var meshes=root.GetProperty("meshes"); var meshIndex=meshNodes[0].GetProperty("mesh").GetInt32();
        if(meshIndex<0 || meshIndex>=meshes.GetArrayLength())throw new InvalidDataException("Invalid skinned mesh index.");
        var vertices=0;
        foreach(var primitive in meshes[meshIndex].GetProperty("primitives").EnumerateArray())
        {
            var attrs=primitive.GetProperty("attributes");
            var positions=Accessor(attrs.GetProperty("POSITION").GetInt32(),"VEC3",5126,3);
            var weights=Accessor(attrs.GetProperty("WEIGHTS_0").GetInt32(),"VEC4",5126,4);
            var jointIndex=attrs.GetProperty("JOINTS_0").GetInt32();var component=accessors[jointIndex].GetProperty("componentType").GetInt32();
            if(component is not (5121 or 5123) || attrs.TryGetProperty("WEIGHTS_1",out _))throw new InvalidDataException("Only four joint influences are supported.");
            var ji=Accessor(jointIndex,"VEC4",component,4);
            if(positions.Count!=weights.Count || ji.Count!=weights.Count)throw new InvalidDataException("Unweighted character vertices.");
            vertices=checked(vertices+positions.Count);
            for(var i=0;i<weights.Count;i++)
            {
                for(var coordinate=0;coordinate<3;coordinate++)
                {
                    var value=FloatAt(positions.Offset+i*positions.Stride+coordinate*4);
                    if(!double.IsFinite(value) || Math.Abs(value)>5)throw new InvalidDataException("Invalid character position.");
                }
                double sum=0;
                for(var c=0;c<4;c++)
                {
                    var weight=FloatAt(weights.Offset+i*weights.Stride+c*4);
                    var offset=ji.Offset+i*ji.Stride+c*(component==5121?1:2);
                    var joint=component==5121?binary.Span[offset]:BinaryPrimitives.ReadUInt16LittleEndian(binary.Span.Slice(offset,2));
                    if(!double.IsFinite(weight) || weight<0 || weight>1 || joint>=joints.Length)throw new InvalidDataException("Invalid character vertex weights/joint index.");
                    sum+=weight;
                }
                if(Math.Abs(sum-1)>.002)throw new InvalidDataException("Character weights are not normalized.");
            }
        }
        if(vertices is < 100 or > 150000)throw new InvalidDataException("Character vertex budget failed.");
        using var manifest=JsonDocument.Parse(await File.ReadAllBytesAsync(manifestPath,token));var m=manifest.RootElement;
        if(m.GetProperty("schemaVersion").GetInt32()!=1 || m.GetProperty("rigConvention").GetString()!="generic_named_bones_v1" ||
            m.GetProperty("restPose").GetString()!="T" || m.GetProperty("sourceForward").GetString()!="-Y" || m.GetProperty("sourceUp").GetString()!="+Z" ||
            m.GetProperty("unityForward").GetString()!="+Z" || m.GetProperty("rootMotion").GetString()!="in_place_no_motion_clips" ||
            m.GetProperty("maximumInfluences").GetInt32() is < 1 or > 4 || m.GetProperty("unweightedVertices").GetInt32()!=0 ||
            !m.GetProperty("boneNames").EnumerateArray().Select(x=>x.GetString()).Order().SequenceEqual(names.Order()))
            throw new InvalidDataException("Character source rig manifest disagrees with the exported skeleton.");
    }
}
