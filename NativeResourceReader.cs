using System;
using System.IO;

namespace RedefinedVehicleImporter
{
    // Maps GTA resource virtual addresses (0x50000000 system / 0x60000000 graphics)
    // to the extracted resource payload. This is the foundation used by both YFT meshes
    // and YTD texture dictionaries.
    internal sealed class NativeResourceReader
    {
        private const ulong SystemBase = 0x50000000;
        private const ulong GraphicsBase = 0x60000000;
        private readonly byte[] _system;
        private readonly byte[] _graphics;

        public NativeResourceReader(byte[] resourceData, uint systemFlags, uint graphicsFlags)
        {
            if (resourceData == null || resourceData.Length == 0) throw new InvalidDataException("The resource payload is empty.");
            var systemSize = GetPageSize(systemFlags);
            var graphicsSize = GetPageSize(graphicsFlags);
            if (systemSize < 0 || graphicsSize < 0 || systemSize + graphicsSize != resourceData.Length)
                throw new InvalidDataException("Resource page flags do not match the extracted payload length. This YFT may be encrypted, corrupted, or use an unsupported GTA resource variant.");

            _system = new byte[systemSize];
            _graphics = new byte[graphicsSize];
            Buffer.BlockCopy(resourceData, 0, _system, 0, systemSize);
            if (graphicsSize > 0) Buffer.BlockCopy(resourceData, systemSize, _graphics, 0, graphicsSize);
        }

        public uint ReadUInt32(ulong address) { return BitConverter.ToUInt32(Read(address, 4), 0); }
        public ushort ReadUInt16(ulong address) { return BitConverter.ToUInt16(Read(address, 2), 0); }
        public ulong ReadUInt64(ulong address) { return BitConverter.ToUInt64(Read(address, 8), 0); }
        public float ReadSingle(ulong address) { return BitConverter.ToSingle(Read(address, 4), 0); }
        public string ReadString(ulong address, int maximumLength)
        {
            if (address == 0) return string.Empty;
            var bytes = new System.Collections.Generic.List<byte>();
            for (var i = 0; i < maximumLength; i++) { var b = Read(address + (ulong)i, 1)[0]; if (b == 0) break; bytes.Add(b); }
            return System.Text.Encoding.ASCII.GetString(bytes.ToArray());
        }
        public byte[] Read(ulong address, int count)
        {
            if (count < 0) throw new ArgumentOutOfRangeException("count");
            int offset;
            var source = Resolve(address, out offset);
            if (offset < 0 || offset + count > source.Length) throw new InvalidDataException("Resource pointer " + address.ToString("X") + " falls outside its declared resource page.");
            var result = new byte[count];
            Buffer.BlockCopy(source, offset, result, 0, count);
            return result;
        }

        private byte[] Resolve(ulong address, out int offset)
        {
            if ((address & SystemBase) == SystemBase) { offset = checked((int)(address & ~SystemBase)); return _system; }
            if ((address & GraphicsBase) == GraphicsBase) { offset = checked((int)(address & ~GraphicsBase)); return _graphics; }
            throw new InvalidDataException("Unsupported GTA resource address " + address.ToString("X") + ".");
        }

        public static int GetPageSize(uint flags)
        {
            var pageShift = (int)(flags & 0xF);
            var pageSize = 0x200 << pageShift;
            long pages = ((flags >> 27) & 1) + (((flags >> 26) & 1) << 1) + (((flags >> 25) & 1) << 2) + (((flags >> 24) & 1) << 3)
                + (((flags >> 17) & 0x7F) << 4) + (((flags >> 11) & 0x3F) << 5) + (((flags >> 7) & 0xF) << 6) + (((flags >> 5) & 3) << 7) + (((flags >> 4) & 1) << 8);
            var size = pages * pageSize;
            if (size > int.MaxValue) throw new InvalidDataException("Resource page size exceeds supported limits.");
            return (int)size;
        }
    }

    internal sealed class NativeYftHeader
    {
        public ulong PagesInfoPointer;
        public float BoundX;
        public float BoundY;
        public float BoundZ;
        public float BoundRadius;
        public ulong DrawablePointer;

        public static NativeYftHeader Read(NativeResourceReader reader)
        {
            // FragType begins with a 16-byte ResourceFileBase followed by the fragment fields.
            var header = new NativeYftHeader();
            header.PagesInfoPointer = reader.ReadUInt64(0x50000008);
            header.BoundX = reader.ReadSingle(0x50000020);
            header.BoundY = reader.ReadSingle(0x50000024);
            header.BoundZ = reader.ReadSingle(0x50000028);
            header.BoundRadius = reader.ReadSingle(0x5000002C);
            header.DrawablePointer = reader.ReadUInt64(0x50000030);
            if (header.DrawablePointer == 0) throw new InvalidDataException("The YFT contains no primary drawable.");
            return header;
        }
    }

    internal sealed class NativeDrawableInfo
    {
        public ulong ShaderGroupPointer;
        public float CenterX;
        public float CenterY;
        public float CenterZ;
        public float Radius;
        public ulong HighModelsPointer;

        public static NativeDrawableInfo Read(NativeResourceReader reader, ulong drawablePointer)
        {
            var info = new NativeDrawableInfo();
            // DrawableBase starts with ResourceFileBase (16 bytes).
            info.ShaderGroupPointer = reader.ReadUInt64(drawablePointer + 0x10);
            info.CenterX = reader.ReadSingle(drawablePointer + 0x20);
            info.CenterY = reader.ReadSingle(drawablePointer + 0x24);
            info.CenterZ = reader.ReadSingle(drawablePointer + 0x28);
            info.Radius = reader.ReadSingle(drawablePointer + 0x2C);
            info.HighModelsPointer = reader.ReadUInt64(drawablePointer + 0x50);
            if (info.Radius <= 0 || float.IsNaN(info.Radius) || float.IsInfinity(info.Radius)) throw new InvalidDataException("The drawable has invalid bounds.");
            return info;
        }
    }

    internal sealed class NativeGeometryInfo
    {
        public ulong VertexBufferPointer;
        public ulong IndexBufferPointer;
        public ulong VertexDataPointer;
        public uint IndexCount;
        public ushort VertexCount;
        public ushort VertexStride;
        public uint TextureHash;
    }

    internal static class NativeMeshManifest
    {
        public static NativeGeometryInfo[] ReadHighDetail(NativeResourceReader reader, NativeDrawableInfo drawable)
        {
            if (drawable.HighModelsPointer == 0) throw new InvalidDataException("The drawable has no high-detail model list.");
            var modelsPointer = reader.ReadUInt64(drawable.HighModelsPointer);
            var modelCount = reader.ReadUInt16(drawable.HighModelsPointer + 8);
            if (modelCount == 0 || modelCount > 4096) throw new InvalidDataException("The drawable high-detail model count is invalid.");
            var geometries = new System.Collections.Generic.List<NativeGeometryInfo>();
            var shaderTextures = NativeShaderMaterials.Read(reader, drawable.ShaderGroupPointer);
            for (var modelIndex = 0; modelIndex < modelCount; modelIndex++)
            {
                var modelPointer = reader.ReadUInt64(modelsPointer + ((ulong)modelIndex * 8));
                if (modelPointer == 0) continue;
                var geometryPointers = reader.ReadUInt64(modelPointer + 8);
                var geometryCount = reader.ReadUInt16(modelPointer + 16);
                var shaderMapping = reader.ReadUInt64(modelPointer + 0x20);
                if (geometryCount > 2048) throw new InvalidDataException("The drawable geometry count is invalid.");
                for (var geometryIndex = 0; geometryIndex < geometryCount; geometryIndex++)
                {
                    var geometryPointer = reader.ReadUInt64(geometryPointers + ((ulong)geometryIndex * 8));
                    if (geometryPointer == 0) continue;
                    var vertexBufferPointer = reader.ReadUInt64(geometryPointer + 0x18);
                    var indexBufferPointer = reader.ReadUInt64(geometryPointer + 0x38);
                    var vertexCount = reader.ReadUInt16(geometryPointer + 0x60);
                    var indexCount = reader.ReadUInt32(geometryPointer + 0x58);
                    if (vertexCount == 0 && vertexBufferPointer != 0) vertexCount = (ushort)Math.Min(65535u, reader.ReadUInt32(vertexBufferPointer + 0x18));
                    if (indexCount == 0 && indexBufferPointer != 0) indexCount = reader.ReadUInt32(indexBufferPointer + 8);
                    geometries.Add(new NativeGeometryInfo
                    {
                        VertexBufferPointer = vertexBufferPointer,
                        IndexBufferPointer = indexBufferPointer,
                        IndexCount = indexCount,
                        VertexCount = vertexCount,
                        VertexStride = reader.ReadUInt16(geometryPointer + 0x70),
                        VertexDataPointer = reader.ReadUInt64(geometryPointer + 0x78),
                        TextureHash = shaderMapping == 0 ? 0 : shaderTextures.Get(reader.ReadUInt16(shaderMapping + ((ulong)geometryIndex * 2)))
                    });
                }
            }
            if (geometries.Count == 0) throw new InvalidDataException("The selected drawable contains no renderable high-detail geometry.");
            return geometries.ToArray();
        }
    }

    internal sealed class NativeShaderMaterials
    {
        private readonly System.Collections.Generic.Dictionary<ushort, uint> _textures = new System.Collections.Generic.Dictionary<ushort, uint>();
        public uint Get(ushort shader) { uint hash; return _textures.TryGetValue(shader, out hash) ? hash : 0; }
        public static NativeShaderMaterials Read(NativeResourceReader reader, ulong groupPointer)
        {
            var result = new NativeShaderMaterials(); if (groupPointer == 0) return result;
            try
            {
                var shaders = reader.ReadUInt64(groupPointer + 0x10); var count = reader.ReadUInt16(groupPointer + 0x18);
                if (shaders == 0 || count > 2048) return result;
                for (ushort i = 0; i < count; i++)
                {
                    var shader = reader.ReadUInt64(shaders + ((ulong)i * 8)); if (shader == 0) continue;
                    var parameters = reader.ReadUInt64(shader); var parameterCount = reader.Read(shader + 0x10, 1)[0];
                    if (parameters == 0 || parameterCount == 0 || parameterCount > 64) continue;
                    var dataBytes = 0; for (var p = 0; p < parameterCount; p++) dataBytes += 16 * reader.Read(parameters + ((ulong)p * 16), 1)[0];
                    var hashStart = parameters + ((ulong)parameterCount * 16) + (ulong)dataBytes;
                    ulong fallbackTexture = 0; ulong diffuseTexture = 0;
                    for (var p = 0; p < parameterCount; p++)
                    {
                        var parameter = parameters + ((ulong)p * 16); if (reader.Read(parameter, 1)[0] != 0) continue;
                        var texture = reader.ReadUInt64(parameter + 8); if (texture == 0) continue; if (fallbackTexture == 0) fallbackTexture = texture;
                        var parameterHash = reader.ReadUInt32(hashStart + ((ulong)p * 4));
                        if (parameterHash == 4059966321u || parameterHash == 181641832u || parameterHash == 1429813046u || parameterHash == 1399472831u) { diffuseTexture = texture; break; }
                    }
                    var selectedTexture = diffuseTexture != 0 ? diffuseTexture : fallbackTexture;
                    if (selectedTexture != 0) { var name = reader.ReadString(reader.ReadUInt64(selectedTexture + 0x28), 255); if (!string.IsNullOrEmpty(name)) result._textures[i] = JenkHash(name.ToLowerInvariant()); }
                }
            }
            catch (InvalidDataException) { }
            return result;
        }
        private static uint JenkHash(string text) { uint hash = 0; foreach (var c in text) { hash += c; hash += hash << 10; hash ^= hash >> 6; } hash += hash << 3; hash ^= hash >> 11; return hash + (hash << 15); }
    }

    internal sealed class NativeVertexLayout
    {
        public ushort Stride;
        public uint Flags;
        public ulong Types;
        public int PositionOffset = -1;
        public int NormalOffset = -1;
        public int Uv0Offset = -1;
        public int Uv0Type;

        public static NativeVertexLayout Read(NativeResourceReader reader, ulong vertexBufferPointer)
        {
            var declarationPointer = reader.ReadUInt64(vertexBufferPointer + 0x30);
            if (declarationPointer == 0) throw new InvalidDataException("The geometry vertex buffer has no declaration.");
            var layout = new NativeVertexLayout();
            layout.Flags = reader.ReadUInt32(declarationPointer);
            layout.Stride = reader.ReadUInt16(declarationPointer + 4);
            layout.Types = reader.ReadUInt64(declarationPointer + 8);
            var offset = 0;
            for (var semantic = 0; semantic < 16; semantic++)
            {
                if ((layout.Flags & (1u << semantic)) == 0) continue;
                var type = (int)((layout.Types >> (semantic * 4)) & 0xF);
                if (semantic == 0) layout.PositionOffset = offset;
                if (semantic == 3) layout.NormalOffset = offset;
                if (semantic == 6) { layout.Uv0Offset = offset; layout.Uv0Type = type; }
                offset += ComponentSize(type);
            }
            if (layout.Stride == 0 || layout.PositionOffset < 0) throw new InvalidDataException("The vertex declaration does not contain a usable position channel.");
            return layout;
        }

        private static int ComponentSize(int type)
        {
            switch (type)
            {
                case 1: return 4; case 2: return 4; case 3: return 8; case 5: return 8;
                case 6: return 12; case 7: return 16; case 8: return 4; case 9: return 4; case 10: return 4;
                default: return 0;
            }
        }
    }

    internal sealed class NativeMeshData
    {
        public float[] Positions;
        public float[] TextureCoordinates;
        public ushort[] Indices;
        public uint TextureHash;
    }

    internal static class NativeMeshDecoder
    {
        public static NativeMeshData Decode(NativeResourceReader reader, NativeGeometryInfo geometry)
        {
            if (geometry.VertexCount == 0 || geometry.IndexCount < 3) throw new InvalidDataException("The geometry contains no triangles.");
            if (geometry.VertexCount > 65535 || geometry.IndexCount > 1000000) throw new InvalidDataException("The geometry exceeds the native preview safety limits.");
            var layout = NativeVertexLayout.Read(reader, geometry.VertexBufferPointer);
            if (layout.PositionOffset + 12 > layout.Stride) throw new InvalidDataException("The vertex declaration position channel is not a Float3.");
            var positions = new float[geometry.VertexCount * 3];
            var uvSize = layout.Uv0Type == 1 ? 4 : 8;
            var textureCoordinates = layout.Uv0Offset >= 0 && layout.Uv0Offset + uvSize <= layout.Stride ? new float[geometry.VertexCount * 2] : null;
            for (var i = 0; i < geometry.VertexCount; i++)
            {
                var address = geometry.VertexDataPointer + ((ulong)i * geometry.VertexStride) + (uint)layout.PositionOffset;
                positions[(i * 3)] = reader.ReadSingle(address);
                positions[(i * 3) + 1] = reader.ReadSingle(address + 4);
                positions[(i * 3) + 2] = reader.ReadSingle(address + 8);
                if (textureCoordinates != null)
                {
                    var uv = geometry.VertexDataPointer + ((ulong)i * geometry.VertexStride) + (uint)layout.Uv0Offset;
                    if (layout.Uv0Type == 1) { textureCoordinates[i * 2] = HalfToSingle(reader.ReadUInt16(uv)); textureCoordinates[(i * 2) + 1] = HalfToSingle(reader.ReadUInt16(uv + 2)); }
                    else { textureCoordinates[i * 2] = reader.ReadSingle(uv); textureCoordinates[(i * 2) + 1] = reader.ReadSingle(uv + 4); }
                }
            }
            var indexPointer = reader.ReadUInt64(geometry.IndexBufferPointer + 0x10);
            if (indexPointer == 0) throw new InvalidDataException("The geometry index buffer has no index data.");
            var indices = new ushort[geometry.IndexCount];
            for (var i = 0; i < geometry.IndexCount; i++) indices[i] = reader.ReadUInt16(indexPointer + ((ulong)i * 2));
            return new NativeMeshData { Positions = positions, TextureCoordinates = textureCoordinates, Indices = indices };
        }
        private static float HalfToSingle(ushort value)
        {
            var sign = (value >> 15) == 0 ? 1f : -1f; var exponent = (value >> 10) & 31; var fraction = value & 1023;
            if (exponent == 0) return sign * fraction * 0.000000059604645f;
            if (exponent == 31) return fraction == 0 ? sign * float.PositiveInfinity : float.NaN;
            return sign * (1f + fraction / 1024f) * (float)Math.Pow(2, exponent - 15);
        }
    }
}
