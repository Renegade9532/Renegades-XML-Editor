using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RedefinedVehicleImporter
{
    // Small legacy-GTA YTD reader. It deliberately reads only ordinary 2D diffuse textures;
    // unsupported formats are ignored so one unusual texture cannot prevent the vehicle loading.
    internal static class NativeTextureDecoder
    {
        private const uint Dxt1 = 0x31545844, Dxt3 = 0x33545844, Dxt5 = 0x35545844;
        internal static BitmapSource FindVehicleLivery(IEnumerable<NativeVehicleArchive.NativeResourceAsset> assets)
        {
            BitmapSource selected = null;
            var pixels = 0;
            foreach (var asset in assets)
            {
                if (asset == null || asset.Data == null) continue;
                NativeResourceReader r;
                try { r = new NativeResourceReader(asset.Data, asset.SystemFlags, asset.GraphicsFlags); }
                catch (InvalidDataException) { continue; }
                try
                {
                    // TextureDictionary: two ResourceSimpleList64 blocks at +20 and +30.
                    var dictionary = 0x50000000UL;
                    var texturesList = dictionary + 0x30;
                    var entries = r.ReadUInt64(texturesList);
                    var count = r.ReadUInt16(texturesList + 8);
                    if (count == 0 || count > 2048 || entries == 0) continue;
                    for (var i = 0; i < count; i++)
                    {
                        var texture = r.ReadUInt64(entries + ((ulong)i * 8));
                        if (texture == 0) continue;
                        var width = r.ReadUInt16(texture + 0x50);
                        var height = r.ReadUInt16(texture + 0x52);
                        var stride = r.ReadUInt16(texture + 0x56);
                        var format = r.ReadUInt32(texture + 0x58);
                        var data = r.ReadUInt64(texture + 0x70);
                        if (width < 4 || height < 4 || width > 8192 || height > 8192 || data == 0) continue;
                        var bitmap = Decode(r, data, width, height, stride, format);
                        if (bitmap != null && width * height > pixels) { selected = bitmap; pixels = width * height; }
                    }
                }
                catch (InvalidDataException) { }
            }
            return selected;
        }

        internal static Dictionary<uint, BitmapSource> ReadTextures(IEnumerable<NativeVehicleArchive.NativeResourceAsset> assets, ISet<uint> requiredHashes)
        {
            var result = new Dictionary<uint, BitmapSource>();
            foreach (var asset in assets)
            {
                if (asset == null || asset.Data == null) continue; NativeResourceReader r;
                try { r = new NativeResourceReader(asset.Data, asset.SystemFlags, asset.GraphicsFlags); var dictionary = 0x50000000UL; var names = dictionary + 0x20; var textures = dictionary + 0x30; var nameEntries = r.ReadUInt64(names); var textureEntries = r.ReadUInt64(textures); var count = Math.Min(r.ReadUInt16(names + 8), r.ReadUInt16(textures + 8)); if (count > 2048) continue;
                    for (var i = 0; i < count; i++) { var hash = r.ReadUInt32(nameEntries + ((ulong)i * 4)); if (requiredHashes != null && !requiredHashes.Contains(hash)) continue; var texture = r.ReadUInt64(textureEntries + ((ulong)i * 8)); if (texture == 0) continue; var width = r.ReadUInt16(texture + 0x50); var height = r.ReadUInt16(texture + 0x52); var stride = r.ReadUInt16(texture + 0x56); var format = r.ReadUInt32(texture + 0x58); var data = r.ReadUInt64(texture + 0x70); if (width < 4 || height < 4 || data == 0) continue; var bitmap = Decode(r, data, width, height, stride, format); if (bitmap != null) result[hash] = bitmap; }
                } catch (InvalidDataException) { }
            }
            return result;
        }

        internal static BitmapSource LoadDds(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) throw new FileNotFoundException("The selected DDS file was not found.", path);
            var file = File.ReadAllBytes(path); if (file.Length < 128 || BitConverter.ToUInt32(file, 0) != 0x20534444) throw new InvalidDataException("This is not a valid DDS texture file.");
            var height = BitConverter.ToInt32(file, 12); var width = BitConverter.ToInt32(file, 16); uint format = BitConverter.ToUInt32(file, 84);
            if (width < 1 || height < 1 || width > 8192 || height > 8192) throw new InvalidDataException("The DDS image dimensions are unsupported.");
            var rgba = new byte[width * height * 4]; var offset = 128;
            if (format == Dxt1 || format == Dxt3 || format == Dxt5) DecodeDxt(ReadBytes(file, offset, ((width + 3) / 4) * ((height + 3) / 4) * (format == Dxt1 ? 8 : 16)), width, height, format, rgba);
            else if (format == 0) { var raw = ReadBytes(file, offset, width * height * 4); for (var i = 0; i < raw.Length; i += 4) { rgba[i] = raw[i + 2]; rgba[i + 1] = raw[i + 1]; rgba[i + 2] = raw[i]; rgba[i + 3] = raw[i + 3]; } }
            else throw new InvalidDataException("This DDS format is not supported. Use DXT1, DXT3, DXT5, or 32-bit RGBA.");
            var image = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, rgba, width * 4); image.Freeze(); return image;
        }
        private static byte[] ReadBytes(byte[] source, int offset, int count) { if (offset < 0 || count < 0 || offset + count > source.Length) throw new InvalidDataException("The DDS texture is truncated."); var result = new byte[count]; Buffer.BlockCopy(source, offset, result, 0, count); return result; }

        private static BitmapSource Decode(NativeResourceReader r, ulong address, int width, int height, int stride, uint format)
        {
            var rgba = new byte[width * height * 4];
            if (format == 21 || format == 22 || format == 32)
            {
                var raw = r.Read(address, width * height * 4);
                for (var i = 0; i < raw.Length; i += 4) { rgba[i] = raw[i + 2]; rgba[i + 1] = raw[i + 1]; rgba[i + 2] = raw[i]; rgba[i + 3] = format == 22 ? (byte)255 : raw[i + 3]; }
            }
            else if (format == Dxt1 || format == Dxt3 || format == Dxt5)
            {
                var bytes = ((width + 3) / 4) * ((height + 3) / 4) * (format == Dxt1 ? 8 : 16);
                DecodeDxt(r.Read(address, bytes), width, height, format, rgba);
            }
            else return null;
            var image = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, rgba, width * 4);
            image.Freeze(); return image;
        }

        private static void DecodeDxt(byte[] input, int width, int height, uint format, byte[] output)
        {
            var offset = 0;
            for (var by = 0; by < height; by += 4) for (var bx = 0; bx < width; bx += 4)
            {
                var alpha = new byte[16]; for (var i = 0; i < 16; i++) alpha[i] = 255;
                if (format == Dxt3) { for (var i = 0; i < 16; i++) alpha[i] = (byte)(((input[offset + i / 2] >> ((i & 1) * 4)) & 15) * 17); offset += 8; }
                else if (format == Dxt5) { DecodeDxt5Alpha(input, offset, alpha); offset += 8; }
                var c0 = input[offset] | (input[offset + 1] << 8); var c1 = input[offset + 2] | (input[offset + 3] << 8);
                var colors = new byte[16]; Set565(colors, 0, c0); Set565(colors, 4, c1);
                Mix(colors, 8, colors, 0, colors, 4, 2, 1); Mix(colors, 12, colors, 0, colors, 4, 1, 2);
                if (format == Dxt1 && c0 <= c1) { colors[15] = 0; colors[12] = colors[13] = colors[14] = 0; }
                var indices = BitConverter.ToUInt32(input, offset + 4); offset += 8;
                for (var y = 0; y < 4; y++) for (var x = 0; x < 4; x++) { var n = y * 4 + x; var px = bx + x; var py = by + y; if (px >= width || py >= height) continue; var ci = ((indices >> (n * 2)) & 3) * 4; var p = (py * width + px) * 4; output[p] = colors[ci + 2]; output[p + 1] = colors[ci + 1]; output[p + 2] = colors[ci]; output[p + 3] = format == Dxt1 && c0 <= c1 && ci == 12 ? (byte)0 : alpha[n]; }
            }
        }
        private static void DecodeDxt5Alpha(byte[] b, int o, byte[] a)
        {
            var table = new byte[8]; table[0] = b[o]; table[1] = b[o + 1]; if (table[0] > table[1]) for (var i = 2; i < 8; i++) table[i] = (byte)(((8 - i) * table[0] + (i - 1) * table[1]) / 7); else { for (var i = 2; i < 6; i++) table[i] = (byte)(((6 - i) * table[0] + (i - 1) * table[1]) / 5); table[6] = 0; table[7] = 255; } ulong bits = 0; for (var i = 0; i < 6; i++) bits |= (ulong)b[o + 2 + i] << (8 * i); for (var i = 0; i < 16; i++) a[i] = table[(bits >> (3 * i)) & 7];
        }
        private static void Set565(byte[] c, int o, int v) { c[o] = (byte)(((v >> 11) & 31) * 255 / 31); c[o + 1] = (byte)(((v >> 5) & 63) * 255 / 63); c[o + 2] = (byte)((v & 31) * 255 / 31); c[o + 3] = 255; }
        private static void Mix(byte[] d, int o, byte[] a, int ao, byte[] b, int bo, int av, int bv) { for (var i = 0; i < 3; i++) d[o + i] = (byte)((a[ao + i] * av + b[bo + i] * bv) / (av + bv)); d[o + 3] = 255; }
    }
}
