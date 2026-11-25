using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;


namespace VF.Utils {
    /**
     * Reads one key from the VCC's Chromium LevelDB.
     * VRChat please stop storing things in the webview session.
     */
    internal sealed class LevelDbValueReader {
        private const int MaxFileSize = 32 * 1024 * 1024;
        private const int LogBlockSize = 32768;
        private const ulong TableMagic = 0xdb4775248b80fb57;

        private static readonly byte[] SelectedReposKey = Encoding.ASCII.GetBytes("_http://localhost:5476\0\u0001selectedRepos");

        public static (string directory, DateTime modified, Func<JArray> readSelection) GetRepositorySelection(string appDataDirectory) {
            var levelDbDirectory = Path.Combine(appDataDirectory, "Photino", "EBWebView", "Default", "Local Storage", "leveldb");
            try {
                if (Directory.Exists(levelDbDirectory)) {
                    var modified = new DirectoryInfo(levelDbDirectory).EnumerateFiles()
                        .Where(file => file.Extension is ".log" or ".ldb")
                        .Select(file => file.LastWriteTimeUtc).DefaultIfEmpty().Max();
                    return (levelDbDirectory, modified, () => ReadSelection(levelDbDirectory));
                }
            } catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) {
                // The database can disappear during enumeration.
            }
            return default;
        }

        private static JArray ReadSelection(string levelDbDirectory) {
            if (!TryRead(levelDbDirectory, SelectedReposKey, out var data)) {
                throw new IOException("Could not read the VCC repository selection.");
            }
            if (data == null || data.Length == 0 || data[0] > 1) return null;
            var encoding = data[0] == 0 ? Encoding.Unicode : Encoding.GetEncoding(28591);
            return JArray.Parse(encoding.GetString(data, 1, data.Length - 1));
        }

        // A successful lookup with a null value means the key does not exist.
        private static bool TryRead(string directory, byte[] key, out byte[] value) {
            value = null;
            try {
                var currentPath = Path.Combine(directory, "CURRENT");
                var current = ReadFile(currentPath);
                var manifestName = Encoding.ASCII.GetString(current).TrimEnd('\n', '\r');
                if (!manifestName.StartsWith("MANIFEST-", StringComparison.Ordinal)
                    || !ulong.TryParse(manifestName.Substring(9), out _)) return false;
                var manifestPath = Path.Combine(directory, manifestName);
                var manifest = ReadFile(manifestPath);
                var tables = new HashSet<ulong>();
                ulong logNumber = 0;
                ulong previousLog = 0;
                foreach (var edit in ReadLog(manifest)) {
                    while (edit.Remaining > 0) {
                        switch (edit.Varint()) {
                            case 1: edit.Slice(); break; // Comparator.
                            case 2: logNumber = edit.Varint(); break;
                            case 3: case 4: edit.Varint(); break; // Next file number or last sequence.
                            case 5: edit.Varint(); edit.Slice(); break; // Compaction pointer.
                            case 6: edit.Varint(); tables.Remove(edit.Varint()); break;
                            case 7:
                                edit.Varint(); // Level.
                                tables.Add(edit.Varint());
                                edit.Varint(); // File size.
                                edit.Slice(); // Smallest key.
                                edit.Slice(); // Largest key.
                                break;
                            case 9: previousLog = edit.Varint(); break;
                            default: throw new InvalidDataException("Unknown LevelDB manifest entry.");
                        }
                    }
                }

                ulong latest = 0;
                byte[] result = null;
                void Check(byte[] candidate, ulong sequence, byte[] data) {
                    if (sequence < latest || !candidate.SequenceEqual(key)) return;
                    latest = sequence;
                    result = data;
                }
                foreach (var table in tables) {
                    var path = Path.Combine(directory, $"{table:D6}.ldb");
                    ReadTable(ReadFile(path), Check);
                }
                var logs = Directory.GetFiles(directory, "*.log").Where(path =>
                    ulong.TryParse(Path.GetFileNameWithoutExtension(path), out var number)
                    && (number >= logNumber || number == previousLog)).ToArray();
                foreach (var path in logs) {
                    var bytes = ReadFile(path);
                    foreach (var batch in ReadLog(bytes)) {
                        var seq = batch.UInt64();
                        var count = batch.UInt32();
                        for (uint i = 0; i < count; i++) {
                            var type = batch.Byte();
                            var candidate = batch.Slice();
                            if (type > 1) throw new InvalidDataException("Unknown LevelDB write type.");
                            Check(candidate, seq + i, type == 1 ? batch.Slice() : null);
                        }
                        if (batch.Remaining != 0) throw new InvalidDataException("Incomplete LevelDB write batch.");
                    }
                    if (new FileInfo(path).Length != bytes.Length) return false;
                }
                // A compaction can replace both the manifest and its active files during the read.
                if (!current.SequenceEqual(ReadFile(currentPath)) || !manifest.SequenceEqual(ReadFile(manifestPath))) return false;
                value = result;
                return true;
            } catch (Exception e) when (e is IOException || e is UnauthorizedAccessException
                    || e is InvalidDataException || e is OverflowException) {
                return false;
            }
        }

        private static byte[] ReadFile(string path) {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length > MaxFileSize) throw new InvalidDataException("LevelDB file exceeds the read limit.");
            using var reader = new BinaryReader(stream);
            var length = (int)stream.Length;
            var data = reader.ReadBytes(length);
            if (data.Length != length) throw new EndOfStreamException();
            return data;
        }

        private static IEnumerable<LevelDbValueReader> ReadLog(byte[] bytes) {
            var log = new LevelDbValueReader(bytes);
            using var fragments = new MemoryStream();
            var fragmented = false;
            while (log.Remaining > 0) {
                var blockRemaining = LogBlockSize - log.Position % LogBlockSize;
                if (blockRemaining < 7) {
                    log.Skip(Math.Min(blockRemaining, log.Remaining));
                    continue;
                }
                var checksum = log.UInt32();
                var size = log.Byte() | log.Byte() << 8;
                var type = log.Byte();
                if (type == 0 && size == 0 && checksum == 0) {
                    log.Skip(Math.Min(blockRemaining - 7, log.Remaining));
                    continue;
                }
                if (size > blockRemaining - 7) throw new InvalidDataException("Bad LevelDB record size.");
                CheckChecksum(bytes, log.Position - 1, size + 1, checksum);
                var data = log.Bytes(size);
                if (type == 1 && !fragmented) {
                    yield return new LevelDbValueReader(data);
                } else if (type == 2 && !fragmented || (type == 3 || type == 4) && fragmented) {
                    fragments.Write(data, 0, data.Length);
                    fragmented = type != 4;
                    if (fragmented) continue;
                    yield return new LevelDbValueReader(fragments.ToArray());
                    fragments.SetLength(0);
                } else {
                    throw new InvalidDataException("Bad LevelDB record sequence.");
                }
            }
            if (fragmented) throw new InvalidDataException("Incomplete LevelDB record.");
        }

        private static void ReadTable(byte[] table, Action<byte[], ulong, byte[]> check) {
            var footer = new LevelDbValueReader(table, table.Length - 48, 40);
            if (new LevelDbValueReader(table, table.Length - 8, 8).UInt64() != TableMagic) throw new InvalidDataException("Unknown LevelDB table.");
            footer.Varint(); // Metaindex block offset.
            footer.Varint(); // Metaindex block size.
            var index = ReadBlock(table, footer);
            foreach (var entry in ReadEntries(index)) {
                var block = ReadBlock(table, new LevelDbValueReader(entry.value));
                foreach (var (key, data) in ReadEntries(block)) {
                    var sequenceAndType = new LevelDbValueReader(key, key.Length - 8, 8).UInt64();
                    var type = sequenceAndType & 255;
                    if (type > 1) throw new InvalidDataException("Unknown LevelDB value type.");
                    check(key.Take(key.Length - 8).ToArray(), sequenceAndType >> 8, type == 1 ? data : null);
                }
            }
        }

        private static byte[] ReadBlock(byte[] table, LevelDbValueReader handle) {
            var offset = checked((int)handle.Varint());
            var size = checked((int)handle.Varint());
            var block = new LevelDbValueReader(table, offset, checked(size + 5));
            var data = block.Bytes(size);
            var type = block.Byte();
            CheckChecksum(table, offset, size + 1, block.UInt32());
            if (type == 0) return data;
            if (type == 1) return DecompressSnappy(data);
            throw new InvalidDataException("Unknown LevelDB compression type.");
        }

        private static IEnumerable<(byte[] key, byte[] value)> ReadEntries(byte[] block) {
            var restarts = new LevelDbValueReader(block, block.Length - 4, 4).UInt32();
            var reader = new LevelDbValueReader(block, 0, checked(block.Length - (int)(restarts + 1) * 4));
            var previous = Array.Empty<byte>();
            while (reader.Remaining > 0) {
                var shared = checked((int)reader.Varint());
                var next = checked((int)reader.Varint());
                var valueSize = checked((int)reader.Varint());
                if (shared > previous.Length || next > reader.Remaining) throw new InvalidDataException("Bad LevelDB table key.");
                var key = new byte[checked(shared + next)];
                Buffer.BlockCopy(previous, 0, key, 0, shared);
                Buffer.BlockCopy(reader.Bytes(next), 0, key, shared, next);
                yield return (key, reader.Bytes(valueSize));
                previous = key;
            }
        }

        private static byte[] DecompressSnappy(byte[] compressed) {
            var input = new LevelDbValueReader(compressed);
            var size = checked((int)input.Varint());
            if (size > MaxFileSize) throw new InvalidDataException("LevelDB block exceeds the read limit.");
            var output = new byte[size];
            var position = 0;
            while (input.Remaining > 0) {
                var tag = input.Byte();
                var kind = tag & 3;
                var length = tag >> 2;
                if (kind == 0) {
                    if (length >= 60) length = checked((int)input.LittleEndian(length - 59));
                    length = checked(length + 1);
                    if (length > output.Length - position) throw new InvalidDataException("Bad Snappy literal.");
                    Buffer.BlockCopy(input.Bytes(length), 0, output, position, length);
                } else {
                    var offset = kind == 1 ? ((tag & 224) << 3) | input.Byte()
                        : checked((int)input.LittleEndian(kind == 2 ? 2 : 4));
                    length = kind == 1 ? ((tag >> 2) & 7) + 4 : length + 1;
                    if (offset == 0 || offset > position || length > output.Length - position)
                        throw new InvalidDataException("Bad Snappy copy.");
                    for (var i = 0; i < length; i++) output[position + i] = output[position + i - offset];
                }
                position += length;
            }
            if (position != size) throw new InvalidDataException("Incomplete Snappy block.");
            return output;
        }

        private static void CheckChecksum(byte[] bytes, int start, int count, uint expected) {
            if (count > bytes.Length - start) throw new EndOfStreamException();
            var crc = uint.MaxValue;
            for (var i = start; i < start + count; i++) {
                crc ^= bytes[i];
                for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) == 1 ? 0x82f63b78U : 0);
            }
            crc = ~crc;
            if (unchecked(((crc >> 15) | (crc << 17)) + 0xa282ead8U) != expected)
                throw new InvalidDataException("Bad LevelDB checksum.");
        }

        private readonly byte[] buffer;
        private readonly int end;
        private int Position { get; set; }
        private int Remaining => end - Position;

        private LevelDbValueReader(byte[] buffer) : this(buffer, 0, buffer.Length) { }

        private LevelDbValueReader(byte[] buffer, int start, int length) {
            if (start < 0 || length < 0 || length > buffer.Length - start) throw new EndOfStreamException();
            this.buffer = buffer;
            Position = start;
            end = start + length;
        }

        private int Byte() {
            if (Remaining == 0) throw new EndOfStreamException();
            return buffer[Position++];
        }

        private ulong LittleEndian(int count) {
            ulong result = 0;
            for (var i = 0; i < count; i++) result |= (ulong)Byte() << (i * 8);
            return result;
        }

        private uint UInt32() => (uint)LittleEndian(4);
        private ulong UInt64() => LittleEndian(8);
        private ulong Varint() {
            ulong result = 0;
            for (var shift = 0; shift < 64; shift += 7) {
                var value = Byte();
                if (shift == 63 && value > 1) break;
                result |= (ulong)(value & 127) << shift;
                if (value < 128) return result;
            }
            throw new InvalidDataException("Bad LevelDB integer.");
        }

        private void Skip(int count) {
            if (count < 0 || count > Remaining) throw new EndOfStreamException();
            Position += count;
        }
        private byte[] Bytes(int count) {
            var start = Position;
            Skip(count);
            var data = new byte[count];
            Buffer.BlockCopy(buffer, start, data, 0, count);
            return data;
        }
        private byte[] Slice() => Bytes(checked((int)Varint()));
    }
}
