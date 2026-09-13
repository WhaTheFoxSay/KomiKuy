using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Mangaplus.Services
{
    public enum WireType
    {
        Varint = 0,
        Fixed64 = 1,
        LengthDelimited = 2,
        StartGroup = 3,
        EndGroup = 4,
        Fixed32 = 5
    }

    public class ProtoField
    {
        public int Tag { get; set; }
        public WireType WireType { get; set; }
        public ulong VarintValue { get; set; }
        public byte[] RawBytes { get; set; }

        public string AsString() => RawBytes != null ? Encoding.UTF8.GetString(RawBytes) : "";
        public int AsInt32() => (int)VarintValue;
        public long AsInt64() => (long)VarintValue;
        public bool AsBool() => VarintValue != 0;

        public List<ProtoField> AsMessage()
        {
            if (RawBytes == null || RawBytes.Length == 0) return new List<ProtoField>();
            return ProtobufReader.ParseFields(RawBytes);
        }
    }

    public static class ProtobufReader
    {
        public static List<ProtoField> ParseFields(byte[] data, int offset = 0, int length = -1)
        {
            var list = new List<ProtoField>();
            if (data == null || data.Length == 0) return list;

            int end = length < 0 ? data.Length : Math.Min(data.Length, offset + length);
            int pos = offset;

            while (pos < end)
            {
                if (!TryReadVarint(data, ref pos, end, out ulong key))
                    break;

                int tag = (int)(key >> 3);
                WireType wireType = (WireType)(key & 0x07);

                if (tag <= 0) break;

                var field = new ProtoField { Tag = tag, WireType = wireType };

                switch (wireType)
                {
                    case WireType.Varint:
                        if (TryReadVarint(data, ref pos, end, out ulong varintVal))
                        {
                            field.VarintValue = varintVal;
                            list.Add(field);
                        }
                        else return list;
                        break;

                    case WireType.Fixed64:
                        if (pos + 8 <= end)
                        {
                            byte[] b64 = new byte[8];
                            Buffer.BlockCopy(data, pos, b64, 0, 8);
                            pos += 8;
                            field.RawBytes = b64;
                            list.Add(field);
                        }
                        else return list;
                        break;

                    case WireType.LengthDelimited:
                        if (TryReadVarint(data, ref pos, end, out ulong lenVal))
                        {
                            int len = (int)lenVal;
                            if (len >= 0 && pos + len <= end)
                            {
                                byte[] raw = new byte[len];
                                if (len > 0)
                                {
                                    Buffer.BlockCopy(data, pos, raw, 0, len);
                                    pos += len;
                                }
                                field.RawBytes = raw;
                                list.Add(field);
                            }
                            else return list;
                        }
                        else return list;
                        break;

                    case WireType.Fixed32:
                        if (pos + 4 <= end)
                        {
                            byte[] b32 = new byte[4];
                            Buffer.BlockCopy(data, pos, b32, 0, 4);
                            pos += 4;
                            field.RawBytes = b32;
                            list.Add(field);
                        }
                        else return list;
                        break;

                    default:
                        // Unsupported/corrupted wire type, stop parsing
                        return list;
                }
            }

            return list;
        }

        private static bool TryReadVarint(byte[] data, ref int pos, int end, out ulong value)
        {
            value = 0;
            int shift = 0;
            while (pos < end)
            {
                byte b = data[pos++];
                value |= (ulong)(b & 0x7F) << shift;
                if ((b & 0x80) == 0) return true;
                shift += 7;
                if (shift >= 64) return false;
            }
            return false;
        }
    }
}
