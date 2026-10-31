using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ShaderRipper.Plugins;
using ShaderRipper.Plugins.Containers;
using ShaderRipper.Plugins.Emit;
using ShaderRipper.Plugins.Platforms;

namespace ShaderRipper.Plugin.Example
{
    /// <summary>
    /// A worked example of every extension point, kept deliberately small so the shape
    /// is readable.
    ///
    /// It does three things, one per capability:
    ///
    /// * claims files that begin with a made-up <c>XORC</c> marker and unwraps a
    ///   single-byte XOR -- the shape a real container plugin takes;
    /// * registers a backend with a slot reader and a translator, which is how Metal
    ///   or SPIR-V support would be added;
    /// * adds an emitter pass that appends a provenance comment, which is how a
    ///   project-specific readability fix would be added.
    ///
    /// Build it and point the tool at the output:
    ///
    ///     dotnet build tools/plugins/ShaderRipper.Plugin.Example
    ///     ShaderRipper --plugins --plugin-dir .../bin/Debug/net10.0
    /// </summary>
    public sealed class ExamplePlugin : IShaderRipperPlugin
    {
        public string Name => "Example (container + backend + pass)";

        public string Description =>
            "Demonstrates the three extension points; not a real decoder";

        public PluginApiVersion ApiVersion => PluginApiVersion.Current;

        public void Register(IPluginRegistry registry)
        {
            // Priority 10: claims before other plugins but after the built-ins, which
            // is right for a container nobody else can read.  A provider that wanted
            // to intercept a format the tool already handles would ask for less.
            registry.AddContainerProvider(new XorContainer(), priority: 10);
            registry.AddPlatform(new PlatformDescriptor
            {
                Id = 9001,
                Name = "example",
                Format = "example",
                HasSource = true,
                HasTranslator = true,
            }, new ExampleSlotReader(), new ExampleTranslator());
            registry.AddEmitterPass(new ProvenancePass());
        }
    }

    /// <summary>
    /// Claims <c>XORC&lt;key&gt;&lt;payload&gt;</c> and returns the de-XORed bytes.
    ///
    /// The decoded output has to be something the host already understands -- a
    /// UnityFS container or a serialized file -- because the rest of the pipeline is
    /// unchanged.
    /// </summary>
    internal sealed class XorContainer : IContainerProvider
    {
        private static readonly byte[] Marker = { (byte)'X', (byte)'O', (byte)'R', (byte)'C' };

        public bool CanHandle(ContainerRequest request)
        {
            // Only the head snapshot is inspected: this runs once per candidate file and
            // a game ships thousands of them, so reading the file here would defeat the
            // point.
            var head = request.Head.Span;
            return head.Length >= Marker.Length && head[..Marker.Length].SequenceEqual(Marker);
        }

        public IEnumerable<ContainerStream> Read(ContainerRequest request)
        {
            // An iterator method cannot take an `in` parameter, so the yield body lives
            // in a helper that takes the request by value.
            return ReadCore(request);
        }

        private static IEnumerable<ContainerStream> ReadCore(ContainerRequest request)
        {
            if (string.IsNullOrEmpty(request.Path) || !File.Exists(request.Path))
                yield break;

            byte[] raw = File.ReadAllBytes(request.Path);
            if (raw.Length <= Marker.Length + 1) yield break;

            byte key = raw[Marker.Length];
            var body = new byte[raw.Length - Marker.Length - 1];
            for (int i = 0; i < body.Length; i++)
                body[i] = (byte)(raw[Marker.Length + 1 + i] ^ key);

            yield return new ContainerStream
            {
                Name = Path.GetFileNameWithoutExtension(request.Path) + ".unity3d",
                Content = new MemoryStream(body, writable: false),
            };
        }
    }

    /// <summary>
    /// Enumerates programs in a made-up slot: each one is a 4-byte length followed by
    /// text.  A real format would parse its own header here.
    /// </summary>
    internal sealed class ExampleSlotReader : ISlotReader
    {
        public IReadOnlyList<ProgramDescriptor> Read(ReadOnlyMemory<byte> payload, SlotContext context)
        {
            var list = new List<ProgramDescriptor>();
            var span = payload.Span;
            int at = 0, index = 0;

            while (at + 4 <= span.Length)
            {
                int length = span[at] | span[at + 1] << 8 | span[at + 2] << 16 | span[at + 3] << 24;
                if (length <= 0 || at + 4 + length > span.Length) break;

                int blobIndex = index < context.BlobIndices.Count ? context.BlobIndices[index] : index;
                list.Add(new ProgramDescriptor
                {
                    BlobIndex = blobIndex,
                    // The format does not record a stage; the translator fills it in
                    // from the entry point name.
                    Stage = null,
                    Offset = at + 4,
                    Length = length,
                    Entry = index % 2 == 0 ? "vert" : "frag",
                });

                at += 4 + length;
                index++;
            }
            return list;
        }
    }

    /// <summary>Wraps the payload text in an entry point so the result is a program.</summary>
    internal sealed class ExampleTranslator : IProgramTranslator
    {
        public bool CanTranslate(TranslationRequest request) => request.Payload.Length > 0;

        public TranslationResult Translate(TranslationRequest request)
        {
            // Anything that is not printable is not this format after all; report it as
            // unsupported so it counts as skipped rather than failed.
            var payload = request.Payload.Span;
            for (int i = 0; i < payload.Length; i++)
                if (payload[i] < 0x09 || payload[i] > 0x7E)
                    return new TranslationResult { Unsupported = true };

            string body = Encoding.UTF8.GetString(payload);
            bool vertex = string.Equals(request.Entry, "vert", StringComparison.OrdinalIgnoreCase);

            return new TranslationResult
            {
                Hlsl = vertex
                    ? $"float4 vert(float4 v : POSITION) : SV_Position {{ {body} }}"
                    : $"float4 frag() : SV_Target {{ {body} }}",
                Stage = vertex ? "vs" : "ps",
                Notes = new[] { $"example backend, entry '{request.Entry}'" },
                Target = 50,
            };
        }
    }

    /// <summary>
    /// Appends one provenance line.  A pass returns the input unchanged when it has
    /// nothing to do, which keeps it cheap on the thousands of programs it sees.
    /// </summary>
    internal sealed class ProvenancePass : IEmitterPass
    {
        public string Name => "example-provenance";

        public string Apply(string text, bool macros)
        {
            // Visible proof that the pass ran at all, which is otherwise hard to tell
            // apart from "the pass ran and found nothing to do".
            if (Environment.GetEnvironmentVariable("SHADERRIP_PLUGINDBG") == "1")
                Console.Error.WriteLine($"[example-provenance] called, {text?.Length ?? 0} chars, macros={macros}");

            if (string.IsNullOrEmpty(text) || text.Contains("post-processed by Example")) return text;
            return text + "\n// post-processed by Example\n";
        }
    }
}
