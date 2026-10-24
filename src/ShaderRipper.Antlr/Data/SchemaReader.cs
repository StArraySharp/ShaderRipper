using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ShaderRipper.Antlr.Data
{
    /// <summary>A node of Unity's type tree (schema), keeping MetaFlag bits.</summary>
    internal sealed class TypeNode
    {
        public string Type;
        public string Name;
        public int Size;
        public int Flags;
        public List<TypeNode> Children = new List<TypeNode>();

        public const int FlagAlign = 0x4000;

        public bool Align => (Flags & FlagAlign) != 0;
        public bool IsArray => Type == "Array" && Children.Count == 2;
        public TypeNode Element => Children[1];
        public bool IsPrimitive => Children.Count == 0 && IsPrimitiveType(Type);

        /// <summary>vector/map/Array-wrapper node whose single child is the Array.</summary>
        public bool IsArrayContainer => !IsArray && Children.Count == 1 && Children[0].IsArray;

        public static bool IsPrimitiveType(string t) =>
            t == "int" || t == "unsigned int" || t == "float" ||
            t == "SInt8" || t == "UInt8" || t == "SInt16" || t == "UInt16" ||
            t == "SInt32" || t == "UInt32" || t == "SInt64" || t == "UInt64" ||
            t == "bool" || t == "char";

        public static TypeNode FromJson(JsonElement e)
        {
            var n = new TypeNode
            {
                Type = e.GetProperty("t").GetString(),
                Name = e.TryGetProperty("n", out var nm) ? nm.GetString() : "",
                Size = e.TryGetProperty("s", out var s) ? s.GetInt32() : -1,
                Flags = e.TryGetProperty("f", out var f) ? f.GetInt32() : 0,
            };
            if (e.TryGetProperty("c", out var c) && c.ValueKind == JsonValueKind.Array)
                foreach (var ch in c.EnumerateArray()) n.Children.Add(FromJson(ch));
            return n;
        }

        public static TypeNode LoadSchema(string path)
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            return FromJson(doc.RootElement[0]);
        }

        public TypeNode Find(string name)
        {
            foreach (var c in Children) if (c.Name == name) return c;
            return null;
        }
    }

    /// <summary>Walks a serialized object strictly by its type tree.</summary>
    internal sealed class SchemaReader
    {
        private readonly EndianReader _r;
        public readonly TypeNode Root;

        public SchemaReader(byte[] data, int start, int end, TypeNode root)
        {
            _r = new EndianReader(data, start, end);
            Root = root;
        }

        public int Position => _r.Position;

        public object ReadValue(TypeNode n)
        {
            if (IsString(n)) return _r.ReadAlignedString();

            if (n.IsArrayContainer)
            {
                object inner = ReadValue(n.Children[0]);
                if (n.Align) _r.Align4();
                return inner;
            }

            if (n.IsArray)
            {
                int count = _r.ReadI32();
                if (count < 0 || count > 20_000_000)
                    throw new InvalidOperationException($"array count {count} at {_r.Position - 4}");
                var list = new List<object>(Math.Min(count, 4096));
                var elem = n.Element;
                for (int i = 0; i < count; i++) list.Add(ReadValue(elem));
                if (n.Align) _r.Align4();
                return list;
            }

            if (n.IsPrimitive)
            {
                object v = ReadPrimitive(n);
                if (n.Align) _r.Align4();
                return v;
            }

            var map = new Dictionary<string, object>();
            foreach (var c in n.Children) map[c.Name] = ReadValue(c);
            return map;
        }

        /// <summary>Skip advances the cursor exactly like ReadValue.</summary>
        public void Skip(TypeNode n) => ReadValue(n);

        private static bool IsString(TypeNode n) =>
            n.Type == "string" && n.Children.Count == 1 && n.Children[0].Type == "Array";

        private object ReadPrimitive(TypeNode n)
        {
            switch (n.Type)
            {
                case "int": case "SInt32": return _r.ReadI32();
                case "unsigned int": case "UInt32": return _r.ReadU32();
                case "float": return _r.ReadF32();
                case "SInt8": return (sbyte)_r.ReadByte();
                case "UInt8": case "char": case "bool": return _r.ReadByte();
                case "SInt16": return _r.ReadI16();
                case "UInt16": return _r.ReadU16();
                case "SInt64": return _r.ReadI64();
                default: throw new NotSupportedException("primitive " + n.Type);
            }
        }
    }
}
