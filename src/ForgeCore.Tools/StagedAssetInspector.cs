using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ForgeCore.Tools;

internal sealed record StagedAssetManifest(
    [property: JsonPropertyName("file")] string File,
    [property: JsonPropertyName("sourcePage")] string SourcePage,
    [property: JsonPropertyName("downloadUrl")] string DownloadUrl,
    [property: JsonPropertyName("license")] string License,
    [property: JsonPropertyName("sha256")] string Sha256,
    [property: JsonPropertyName("bytes")] long Bytes,
    [property: JsonPropertyName("stagedUtc")] DateTimeOffset StagedUtc);

internal sealed record StagedAssetInspection(
    StagedAssetManifest Manifest, string Sha256, long Bytes, string Format, string Summary);

internal static class StagedAssetInspector
{
    private const int MaxImageDimension = 16_384;
    private const long MaxImagePixels = 100_000_000;
    private const int MaxGlbJsonBytes = 8 * 1024 * 1024;
    private const int MaxModelVertices = 2_000_000;
    private const int MaxModelFaces = 2_000_000;
    private static readonly JsonSerializerOptions ManifestJson = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true
    };

    public static async Task<StagedAssetInspection> InspectAsync(string assetPath, string manifestPath, long maxAssetBytes,
        string? expectedSha256, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!File.Exists(assetPath) || !File.Exists(manifestPath))
            throw new FileNotFoundException("The staged asset and its provenance manifest must both exist.");
        var info = new FileInfo(assetPath);
        if (info.Length is < 1 || info.Length > maxAssetBytes)
            throw new InvalidDataException($"Staged asset size must be between 1 and {maxAssetBytes} bytes.");
        var manifestText = await File.ReadAllTextAsync(manifestPath, Encoding.UTF8, token);
        StagedAssetManifest manifest;
        try { manifest = JsonSerializer.Deserialize<StagedAssetManifest>(manifestText, ManifestJson) ?? throw new JsonException("Manifest is empty."); }
        catch (JsonException ex) { throw new InvalidDataException("Staged asset manifest is malformed or contains unexpected fields.", ex); }
        ValidateManifest(manifest, Path.GetFileName(assetPath), info.Length);

        await using var hashStream = new FileStream(assetPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81_920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var actualHash = Convert.ToHexString(await SHA256.HashDataAsync(hashStream, token)).ToLowerInvariant();
        if (!FixedHashEquals(manifest.Sha256, actualHash))
            throw new InvalidDataException("Staged asset SHA-256 no longer matches its provenance manifest.");
        if (expectedSha256 is not null && !FixedHashEquals(expectedSha256, actualHash))
            throw new InvalidDataException("Staged asset SHA-256 does not match the approved import hash.");

        var extension = Path.GetExtension(assetPath).ToLowerInvariant();
        var (format, summary) = extension switch
        {
            ".png" => await InspectPngAsync(assetPath, token),
            ".jpg" or ".jpeg" => await InspectJpegAsync(assetPath, token),
            ".webp" => await InspectWebpAsync(assetPath, token),
            ".wav" => await InspectWavAsync(assetPath, token),
            ".ogg" => await InspectSignatureAsync(assetPath, "OggS"u8.ToArray(), "Ogg audio container", token),
            ".mp3" => await InspectMp3Async(assetPath, token),
            ".obj" => await InspectObjAsync(assetPath, token),
            ".glb" => await InspectGlbAsync(assetPath, token),
            _ => throw new InvalidDataException("Unsupported staged asset type.")
        };
        return new(manifest, actualHash, info.Length, format, summary);
    }

    private static void ValidateManifest(StagedAssetManifest manifest, string fileName, long actualBytes)
    {
        if (!string.Equals(manifest.File, fileName, StringComparison.Ordinal))
            throw new InvalidDataException("Manifest file name does not match the staged asset.");
        if (manifest.Bytes != actualBytes) throw new InvalidDataException("Manifest byte count does not match the staged asset.");
        if (manifest.Sha256.Length != 64 || manifest.Sha256.Any(c => !Uri.IsHexDigit(c)))
            throw new InvalidDataException("Manifest SHA-256 is invalid.");
        if (string.IsNullOrWhiteSpace(manifest.License) || manifest.License.Length > 200)
            throw new InvalidDataException("Manifest licence is missing or oversized.");
        if (manifest.StagedUtc == default || manifest.StagedUtc > DateTimeOffset.UtcNow.AddMinutes(5))
            throw new InvalidDataException("Manifest stagedUtc is missing or invalid.");
        ValidateHttps(manifest.SourcePage, "sourcePage"); ValidateHttps(manifest.DownloadUrl, "downloadUrl");
    }

    private static void ValidateHttps(string value, string field)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(uri.UserInfo) || (!uri.IsDefaultPort && uri.Port != 443))
            throw new InvalidDataException($"Manifest {field} must be a safe HTTPS URL.");
    }

    private static bool FixedHashEquals(string expected, string actual) =>
        expected.Length == 64 && actual.Length == 64 && CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(expected), Convert.FromHexString(actual));

    internal static async Task<(string, string)> InspectPngAsync(string path, CancellationToken token)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 16_384, FileOptions.Asynchronous);
        var signature = await ReadExactlyAsync(stream, 8, token);
        if (!signature.AsSpan().SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
            throw new InvalidDataException("PNG signature is invalid.");
        var firstHeader = await ReadExactlyAsync(stream, 8, token);
        var firstLength = BinaryPrimitives.ReadUInt32BigEndian(firstHeader.AsSpan(0, 4));
        if (firstLength != 13 || !firstHeader.AsSpan(4, 4).SequenceEqual("IHDR"u8))
            throw new InvalidDataException("PNG IHDR chunk is invalid.");
        var ihdr = await ReadExactlyAsync(stream, 13, token); _ = await ReadExactlyAsync(stream, 4, token);
        var width = BinaryPrimitives.ReadUInt32BigEndian(ihdr.AsSpan(0, 4));
        var height = BinaryPrimitives.ReadUInt32BigEndian(ihdr.AsSpan(4, 4));
        ValidateDimensions(width, height);
        if (ihdr[8] is not (1 or 2 or 4 or 8 or 16) || ihdr[10] != 0 || ihdr[11] != 0 || ihdr[12] > 1)
            throw new InvalidDataException("PNG IHDR encoding fields are invalid.");
        var foundData = false; var foundEnd = false;
        for (var chunks = 0; chunks < 100_000 && stream.Position + 12 <= stream.Length; chunks++)
        {
            var chunkHeader = await ReadExactlyAsync(stream, 8, token);
            var length = BinaryPrimitives.ReadUInt32BigEndian(chunkHeader.AsSpan(0, 4));
            if (length > 64 * 1024 * 1024 || stream.Position + length + 4 > stream.Length)
                throw new InvalidDataException("PNG chunk length is invalid or excessive.");
            var type = Encoding.ASCII.GetString(chunkHeader, 4, 4);
            if (type == "IDAT") foundData = true;
            if (type == "IEND")
            {
                if (length != 0 || !foundData) throw new InvalidDataException("PNG IEND appeared before image data or had content.");
                _ = await ReadExactlyAsync(stream, 4, token); foundEnd = true; break;
            }
            stream.Seek(length + 4, SeekOrigin.Current);
        }
        if (!foundEnd || stream.Position != stream.Length) throw new InvalidDataException("PNG is missing a final IEND chunk or contains trailing data.");
        return ("PNG image", $"{width}x{height} pixels; IHDR/IDAT/IEND envelope validated");
    }

    private static async Task<(string, string)> InspectJpegAsync(string path, CancellationToken token)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 16_384, FileOptions.Asynchronous);
        if (await ReadByteAsync(stream, token) != 0xFF || await ReadByteAsync(stream, token) != 0xD8)
            throw new InvalidDataException("JPEG start marker is invalid.");
        for (var segments = 0; segments < 4096 && stream.Position < stream.Length; segments++)
        {
            int markerStart;
            do { markerStart = await ReadByteAsync(stream, token); } while (markerStart != 0xFF && stream.Position < stream.Length);
            int marker;
            do { marker = await ReadByteAsync(stream, token); } while (marker == 0xFF);
            if (marker is 0xD9 or 0xDA) break;
            if (marker is >= 0xD0 and <= 0xD7 or 0x01) continue;
            var lengthBytes = await ReadExactlyAsync(stream, 2, token);
            var segmentLength = BinaryPrimitives.ReadUInt16BigEndian(lengthBytes);
            if (segmentLength < 2 || stream.Position + segmentLength - 2 > stream.Length)
                throw new InvalidDataException("JPEG segment length is invalid.");
            if (marker is 0xC0 or 0xC1 or 0xC2 or 0xC3 or 0xC5 or 0xC6 or 0xC7 or 0xC9 or 0xCA or 0xCB or 0xCD or 0xCE or 0xCF)
            {
                var frame = await ReadExactlyAsync(stream, 5, token);
                var height = BinaryPrimitives.ReadUInt16BigEndian(frame.AsSpan(1, 2));
                var width = BinaryPrimitives.ReadUInt16BigEndian(frame.AsSpan(3, 2));
                ValidateDimensions(width, height);
                return ("JPEG image", $"{width}x{height} pixels; frame header validated");
            }
            stream.Seek(segmentLength - 2, SeekOrigin.Current);
        }
        throw new InvalidDataException("JPEG dimensions were not found within bounded header parsing.");
    }

    private static async Task<(string, string)> InspectWebpAsync(string path, CancellationToken token)
    {
        var header = await ReadPrefixAsync(path, 30, token);
        if (!header.AsSpan(0, 4).SequenceEqual("RIFF"u8) || !header.AsSpan(8, 4).SequenceEqual("WEBP"u8))
            throw new InvalidDataException("WebP RIFF signature is invalid.");
        uint width; uint height;
        if (header.AsSpan(12, 4).SequenceEqual("VP8X"u8))
        {
            width = 1u + ReadUInt24LittleEndian(header.AsSpan(24, 3));
            height = 1u + ReadUInt24LittleEndian(header.AsSpan(27, 3));
        }
        else if (header.AsSpan(12, 4).SequenceEqual("VP8L"u8) && header[20] == 0x2F)
        {
            width = 1u + (uint)(header[21] | ((header[22] & 0x3F) << 8));
            height = 1u + (uint)((header[22] >> 6) | (header[23] << 2) | ((header[24] & 0x0F) << 10));
        }
        else if (header.AsSpan(12, 4).SequenceEqual("VP8 "u8) && header.AsSpan(23, 3).SequenceEqual(new byte[] { 0x9D, 0x01, 0x2A }))
        {
            width = (uint)(BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(26, 2)) & 0x3FFF);
            height = (uint)(BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(28, 2)) & 0x3FFF);
        }
        else throw new InvalidDataException("Unsupported or malformed WebP frame header.");
        ValidateDimensions(width, height);
        return ("WebP image", $"{width}x{height} pixels; RIFF/frame header validated");
    }

    private static async Task<(string, string)> InspectWavAsync(string path, CancellationToken token)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 16_384, FileOptions.Asynchronous);
        var header = await ReadExactlyAsync(stream, 12, token);
        if (!header.AsSpan(0, 4).SequenceEqual("RIFF"u8) || !header.AsSpan(8, 4).SequenceEqual("WAVE"u8))
            throw new InvalidDataException("WAV RIFF signature is invalid.");
        ushort? format = null, channels = null, bits = null; uint? sampleRate = null; long? dataBytes = null;
        for (var chunks = 0; chunks < 4096 && stream.Position + 8 <= stream.Length; chunks++)
        {
            var chunk = await ReadExactlyAsync(stream, 8, token);
            var id = Encoding.ASCII.GetString(chunk, 0, 4);
            var length = BinaryPrimitives.ReadUInt32LittleEndian(chunk.AsSpan(4, 4));
            if (stream.Position + length > stream.Length) throw new InvalidDataException("WAV chunk length is invalid.");
            if (id == "fmt ")
            {
                if (length < 16) throw new InvalidDataException("WAV format chunk is too short.");
                var body = await ReadExactlyAsync(stream, 16, token);
                format = BinaryPrimitives.ReadUInt16LittleEndian(body.AsSpan(0, 2));
                channels = BinaryPrimitives.ReadUInt16LittleEndian(body.AsSpan(2, 2));
                sampleRate = BinaryPrimitives.ReadUInt32LittleEndian(body.AsSpan(4, 4));
                bits = BinaryPrimitives.ReadUInt16LittleEndian(body.AsSpan(14, 2));
                stream.Seek(length - 16, SeekOrigin.Current);
            }
            else if (id == "data") { dataBytes = length; stream.Seek(length, SeekOrigin.Current); }
            else stream.Seek(length, SeekOrigin.Current);
            if ((length & 1) != 0 && stream.Position < stream.Length) stream.Seek(1, SeekOrigin.Current);
        }
        if (format is not 1 and not 3 || channels is null or < 1 or > 8 || sampleRate is null or < 8_000 or > 384_000 || bits is null || dataBytes is null)
            throw new InvalidDataException("WAV must contain bounded PCM/float format and data chunks.");
        return ("WAV audio", $"{channels} channels; {sampleRate} Hz; {bits}-bit; {dataBytes} data bytes");
    }

    private static async Task<(string, string)> InspectMp3Async(string path, CancellationToken token)
    {
        var header = await ReadPrefixAsync(path, 10, token);
        var id3 = header.AsSpan(0, 3).SequenceEqual("ID3"u8);
        var frame = header[0] == 0xFF && (header[1] & 0xE0) == 0xE0;
        if (!id3 && !frame) throw new InvalidDataException("MP3 ID3/frame signature is invalid.");
        return ("MP3 audio", id3 ? "ID3 header validated; compressed audio remains passive" : "MPEG frame sync validated; compressed audio remains passive");
    }

    private static async Task<(string, string)> InspectObjAsync(string path, CancellationToken token)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81_920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var reader = new StreamReader(stream, Encoding.UTF8, true, 81_920, leaveOpen: false);
        var vertices = 0; var faces = 0; var normals = 0; var textureCoordinates = 0; var lineCount = 0;
        while (await reader.ReadLineAsync(token) is { } line)
        {
            token.ThrowIfCancellationRequested(); lineCount++;
            if (line.Length > 1_000_000) throw new InvalidDataException("OBJ contains an oversized line.");
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("mtllib ", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("OBJ references an external material library; bundled multi-file imports are not implemented.");
            if (trimmed.StartsWith("v ", StringComparison.Ordinal)) vertices++;
            else if (trimmed.StartsWith("f ", StringComparison.Ordinal)) faces++;
            else if (trimmed.StartsWith("vn ", StringComparison.Ordinal)) normals++;
            else if (trimmed.StartsWith("vt ", StringComparison.Ordinal)) textureCoordinates++;
            if (vertices > MaxModelVertices || faces > MaxModelFaces)
                throw new InvalidDataException("OBJ exceeds ForgeCore's vertex or face limit.");
        }
        if (vertices == 0 || faces == 0) throw new InvalidDataException("OBJ must contain vertices and faces.");
        return ("Wavefront OBJ model", $"{vertices} vertices; {faces} faces; {normals} normals; {textureCoordinates} UV coordinates; {lineCount} lines; no external material library");
    }

    internal static async Task<(string, string)> InspectGlbAsync(string path, CancellationToken token)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81_920, FileOptions.Asynchronous);
        var header = await ReadExactlyAsync(stream, 12, token);
        if (!header.AsSpan(0, 4).SequenceEqual("glTF"u8)) throw new InvalidDataException("GLB magic is invalid.");
        var version = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(4, 4));
        var declaredLength = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(8, 4));
        if (version != 2 || declaredLength != stream.Length) throw new InvalidDataException("GLB must be version 2 with an exact declared length.");
        var chunkHeader = await ReadExactlyAsync(stream, 8, token);
        var jsonLength = BinaryPrimitives.ReadUInt32LittleEndian(chunkHeader.AsSpan(0, 4));
        var chunkType = BinaryPrimitives.ReadUInt32LittleEndian(chunkHeader.AsSpan(4, 4));
        if (jsonLength is < 2 or > MaxGlbJsonBytes || chunkType != 0x4E4F534A || stream.Position + jsonLength > stream.Length)
            throw new InvalidDataException("GLB JSON chunk is missing, oversized, or malformed.");
        var jsonBytes = await ReadExactlyAsync(stream, checked((int)jsonLength), token);
        using var document = JsonDocument.Parse(jsonBytes, new JsonDocumentOptions { MaxDepth = 64 });
        if (ContainsProperty(document.RootElement, "uri"))
            throw new InvalidDataException("GLB contains external or data URI references; only fully embedded buffer-view assets may be imported.");
        var nodes = CountArray(document.RootElement, "nodes"); var meshes = CountArray(document.RootElement, "meshes");
        var materials = CountArray(document.RootElement, "materials"); var textures = CountArray(document.RootElement, "textures");
        var animations = CountArray(document.RootElement, "animations");
        if (nodes > 100_000 || meshes > 10_000 || materials > 10_000 || textures > 10_000 || animations > 10_000)
            throw new InvalidDataException("GLB exceeds ForgeCore's scene resource limits.");
        var (vertices, faces) = CountGlbGeometry(document.RootElement);
        if (vertices > MaxModelVertices || faces > MaxModelFaces)
            throw new InvalidDataException("GLB exceeds ForgeCore's vertex or face limit.");
        return ("glTF 2.0 binary model", $"{nodes} nodes; {meshes} meshes; {materials} materials; {textures} textures; {animations} animations; approximately {vertices} vertices and {faces} faces; no URI references");
    }

    private static (long Vertices, long Faces) CountGlbGeometry(JsonElement root)
    {
        if (!root.TryGetProperty("accessors", out var accessors) || accessors.ValueKind != JsonValueKind.Array ||
            !root.TryGetProperty("meshes", out var meshes) || meshes.ValueKind != JsonValueKind.Array) return (0, 0);
        var counts = accessors.EnumerateArray().Select(x => x.TryGetProperty("count", out var count) && count.TryGetInt64(out var value) ? value : 0).ToArray();
        long vertices = 0, faces = 0;
        foreach (var mesh in meshes.EnumerateArray())
        {
            if (!mesh.TryGetProperty("primitives", out var primitives) || primitives.ValueKind != JsonValueKind.Array) continue;
            foreach (var primitive in primitives.EnumerateArray())
            {
                if (primitive.TryGetProperty("attributes", out var attributes) && attributes.ValueKind == JsonValueKind.Object &&
                    attributes.TryGetProperty("POSITION", out var position) && position.TryGetInt32(out var positionIndex) && positionIndex >= 0 && positionIndex < counts.Length)
                    vertices += counts[positionIndex];
                if (primitive.TryGetProperty("indices", out var indices) && indices.TryGetInt32(out var index) && index >= 0 && index < counts.Length)
                    faces += counts[index] / 3;
            }
        }
        return (vertices, faces);
    }

    private static bool ContainsProperty(JsonElement element, string name)
    {
        if (element.ValueKind == JsonValueKind.Object)
            foreach (var property in element.EnumerateObject())
            {
                if (property.NameEquals(name) || ContainsProperty(property.Value, name)) return true;
            }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) if (ContainsProperty(item, name)) return true;
        return false;
    }

    private static int CountArray(JsonElement root, string property) =>
        root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Array ? value.GetArrayLength() : 0;

    private static async Task<(string, string)> InspectSignatureAsync(string path, byte[] signature, string format, CancellationToken token)
    {
        var header = await ReadPrefixAsync(path, signature.Length, token);
        if (!header.AsSpan().SequenceEqual(signature)) throw new InvalidDataException($"{format} signature is invalid.");
        return (format, "Container signature validated; compressed media remains passive");
    }

    private static void ValidateDimensions(uint width, uint height)
    {
        if (width is 0 or > MaxImageDimension || height is 0 or > MaxImageDimension || (long)width * height > MaxImagePixels)
            throw new InvalidDataException("Image dimensions exceed ForgeCore's limits.");
    }

    private static uint ReadUInt24LittleEndian(ReadOnlySpan<byte> value) =>
        (uint)(value[0] | (value[1] << 8) | (value[2] << 16));

    private static async Task<byte[]> ReadPrefixAsync(string path, int length, CancellationToken token)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 16_384, FileOptions.Asynchronous);
        return await ReadExactlyAsync(stream, length, token);
    }

    private static async Task<byte[]> ReadExactlyAsync(Stream stream, int length, CancellationToken token)
    {
        var buffer = new byte[length];
        try { await stream.ReadExactlyAsync(buffer, token); }
        catch (EndOfStreamException ex) { throw new InvalidDataException("Asset ended before its required structure header was complete.", ex); }
        return buffer;
    }

    private static async Task<int> ReadByteAsync(Stream stream, CancellationToken token)
    {
        var value = new byte[1];
        if (await stream.ReadAsync(value, token) != 1) throw new InvalidDataException("Asset ended unexpectedly.");
        return value[0];
    }
}
