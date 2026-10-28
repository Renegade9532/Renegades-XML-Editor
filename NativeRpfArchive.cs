using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace RedefinedVehicleImporter
{
    // Native, dependency-free reader for OpenIV-style add-on RPF archives. It deliberately
    // keeps archive ownership in the application rather than routing selected assets through
    // the CodeWalker form/cache.
    internal sealed class NativeVehicleArchive
    {
        private const uint Rpf7 = 0x52504637;
        private const uint OpenArchive = 0x4E45504F;

        internal sealed class VehicleFiles
        {
            public string ModelName;
            public byte[] Yft;
            public uint YftSystemFlags;
            public uint YftGraphicsFlags;
            public List<NativeResourceAsset> Ytds = new List<NativeResourceAsset>();
        }

        internal sealed class NativeResourceAsset
        {
            public byte[] Data;
            public uint SystemFlags;
            public uint GraphicsFlags;
        }

        private sealed class Entry
        {
            public string Name;
            public uint Offset;
            public uint Size;
            public bool IsResource;
            public uint SystemFlags;
            public uint GraphicsFlags;
        }

        public VehicleFiles ExtractVehicle(string archivePath, string modelName)
        {
            if (string.IsNullOrWhiteSpace(archivePath) || !File.Exists(archivePath)) throw new FileNotFoundException("Vehicle archive was not found.", archivePath);
            if (string.IsNullOrWhiteSpace(modelName)) throw new ArgumentException("A vehicle model name is required.", "modelName");
            var yftName = modelName + ".yft";
            var hiName = modelName + "_hi.yft";
            var ytdName = modelName + ".ytd";
            byte[] yft = null;
            uint yftSystemFlags = 0;
            uint yftGraphicsFlags = 0;
            var ytds = new List<NativeResourceAsset>();
            using (var stream = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                ScanStreamArchive(stream, 0, stream.Length, 0, yftName, hiName, ytdName, ref yft, ref yftSystemFlags, ref yftGraphicsFlags, ytds);

            if (yft == null) throw new FileNotFoundException("The selected vehicle YFT was not found in this archive or its nested vehicles.rpf.", yftName);
            return new VehicleFiles { ModelName = modelName, Yft = yft, YftSystemFlags = yftSystemFlags, YftGraphicsFlags = yftGraphicsFlags, Ytds = ytds };
        }

        private static void ScanStreamArchive(FileStream stream, long archiveStart, long archiveLength, int depth, string yftName, string hiName, string ytdName, ref byte[] yft, ref uint yftSystemFlags, ref uint yftGraphicsFlags, List<NativeResourceAsset> ytds)
        {
            if (depth > 8 || archiveStart < 0 || archiveStart + 16 > stream.Length) return;
            var header = ReadAt(stream, archiveStart, 16);
            if (BitConverter.ToUInt32(header, 0) != Rpf7) return;
            var count = BitConverter.ToInt32(header, 4);
            var namesLength = BitConverter.ToInt32(header, 8);
            var encryption = BitConverter.ToUInt32(header, 12);
            if (encryption != 0 && encryption != OpenArchive) throw new InvalidDataException("This archive uses encrypted RPF tables and cannot be decoded by the native viewer.");
            var tableLength = checked(count * 16);
            if (count < 0 || count > 500000 || namesLength < 0 || archiveStart + 16L + tableLength + namesLength > stream.Length || archiveStart + archiveLength > stream.Length) throw new InvalidDataException("The RPF header or entry table is invalid.");
            var table = ReadAt(stream, archiveStart + 16, tableLength);
            var names = ReadAt(stream, archiveStart + 16 + tableLength, namesLength);
            for (var i = 0; i < count; i++)
            {
                var p = i * 16;
                var secondWord = BitConverter.ToUInt32(table, p + 4);
                if (secondWord == 0x7FFFFF00) continue;
                var name = GetName(names, 0, names.Length, BitConverter.ToUInt16(table, p));
                if (string.IsNullOrWhiteSpace(name)) continue;
                var isResource = (secondWord & 0x80000000) != 0;
                var packedSize = ReadUInt24(table, p + 2);
                var entry = new Entry { Name = name, Size = (!isResource && packedSize == 0) ? BitConverter.ToUInt32(table, p + 8) : packedSize, Offset = ReadUInt24(table, p + 5) & 0x7FFFFF, IsResource = isResource, SystemFlags = isResource ? BitConverter.ToUInt32(table, p + 8) : 0, GraphicsFlags = isResource ? BitConverter.ToUInt32(table, p + 12) : 0 };
                if (entry.Name.EndsWith(".rpf", StringComparison.OrdinalIgnoreCase))
                {
                    if (entry.Size > 0) ScanStreamArchive(stream, archiveStart + ((long)entry.Offset * 512), entry.Size, depth + 1, yftName, hiName, ytdName, ref yft, ref yftSystemFlags, ref yftGraphicsFlags, ytds);
                    continue;
                }
                if (yft == null && (string.Equals(entry.Name, yftName, StringComparison.OrdinalIgnoreCase) || string.Equals(entry.Name, hiName, StringComparison.OrdinalIgnoreCase)))
                {
                    yft = Extract(stream, archiveStart, entry); yftSystemFlags = entry.SystemFlags; yftGraphicsFlags = entry.GraphicsFlags;
                }
                else if (string.Equals(entry.Name, ytdName, StringComparison.OrdinalIgnoreCase))
                {
                    var ytd = Extract(stream, archiveStart, entry);
                    if (ytd != null) ytds.Add(new NativeResourceAsset { Data = ytd, SystemFlags = entry.SystemFlags, GraphicsFlags = entry.GraphicsFlags });
                }
            }
        }

        private static byte[] Extract(FileStream stream, long archiveStart, Entry entry)
        {
            var skip = entry.IsResource ? 16 : 0;
            if (entry.Size <= skip || entry.Size - skip > int.MaxValue) throw new InvalidDataException("The selected vehicle asset is too large to decode safely.");
            var raw = ReadAt(stream, archiveStart + ((long)entry.Offset * 512) + skip, checked((int)(entry.Size - skip)));
            return TryInflate(raw) ?? raw;
        }

        private static byte[] ReadAt(FileStream stream, long offset, int count)
        {
            if (offset < 0 || count < 0 || offset + count > stream.Length) throw new InvalidDataException("An RPF entry points outside the archive.");
            var data = new byte[count]; stream.Position = offset; var read = 0;
            while (read < count) { var chunk = stream.Read(data, read, count - read); if (chunk == 0) throw new EndOfStreamException("The RPF archive ended unexpectedly."); read += chunk; }
            return data;
        }

        private static IEnumerable<Entry> ReadEntries(byte[] rpf)
        {
            if (rpf == null || rpf.Length < 16 || BitConverter.ToUInt32(rpf, 0) != Rpf7) yield break;
            var count = BitConverter.ToInt32(rpf, 4);
            var namesLength = BitConverter.ToInt32(rpf, 8);
            var encryption = BitConverter.ToUInt32(rpf, 12);
            if (encryption != 0 && encryption != OpenArchive) throw new InvalidDataException("This archive uses encrypted RPF tables and cannot be decoded by the native viewer.");
            var tableStart = 16;
            var namesStart = tableStart + (count * 16);
            if (count < 0 || count > 500000 || namesLength < 0 || namesStart + namesLength > rpf.Length) throw new InvalidDataException("The RPF header or entry table is invalid.");

            for (var i = 0; i < count; i++)
            {
                var p = tableStart + (i * 16);
                var secondWord = BitConverter.ToUInt32(rpf, p + 4);
                if (secondWord == 0x7FFFFF00 || (secondWord & 0x80000000) == 0 && GetName(rpf, namesStart, namesLength, BitConverter.ToUInt16(rpf, p)).Length == 0) continue;
                var name = GetName(rpf, namesStart, namesLength, BitConverter.ToUInt16(rpf, p));
                if (string.IsNullOrWhiteSpace(name)) continue;
                var isResource = (secondWord & 0x80000000) != 0;
                var packedSize = ReadUInt24(rpf, p + 2);
                yield return new Entry
                {
                    Name = name,
                    // Binary RPF entries store their length in the uncompressed-size word
                    // when packed size is zero. Add-on vehicles.rpf uses this form.
                    Size = (!isResource && packedSize == 0) ? BitConverter.ToUInt32(rpf, p + 8) : packedSize,
                    Offset = ReadUInt24(rpf, p + 5) & 0x7FFFFF,
                    IsResource = isResource,
                    SystemFlags = isResource ? BitConverter.ToUInt32(rpf, p + 8) : 0,
                    GraphicsFlags = isResource ? BitConverter.ToUInt32(rpf, p + 12) : 0
                };
            }
        }

        private static byte[] Extract(byte[] rpf, Entry entry)
        {
            var start = checked((int)(entry.Offset * 512));
            var skip = entry.IsResource ? 16 : 0;
            if (start < 0 || start + skip >= rpf.Length || entry.Size <= skip || start + entry.Size > rpf.Length) return null;
            var raw = new byte[entry.Size - skip];
            Buffer.BlockCopy(rpf, start + skip, raw, 0, raw.Length);
            var unpacked = TryInflate(raw);
            return unpacked ?? raw;
        }

        private static byte[] TryInflate(byte[] data)
        {
            try
            {
                using (var input = new MemoryStream(data))
                using (var deflate = new DeflateStream(input, CompressionMode.Decompress))
                using (var output = new MemoryStream())
                {
                    deflate.CopyTo(output);
                    return output.Length > 0 ? output.ToArray() : null;
                }
            }
            catch { return null; }
        }

        private static uint ReadUInt24(byte[] data, int offset) { return (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16)); }
        private static string GetName(byte[] rpf, int namesStart, int namesLength, int offset)
        {
            if (offset < 0 || offset >= namesLength) return string.Empty;
            var end = offset;
            while (end < namesLength && rpf[namesStart + end] != 0) end++;
            return Encoding.ASCII.GetString(rpf, namesStart + offset, end - offset);
        }
    }
}
