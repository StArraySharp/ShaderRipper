using System;
using System.Collections.Generic;
using System.IO;

namespace ShaderRipper.Plugins.Containers
{
    /// <summary>
    /// One input file offered to the container providers.
    ///
    /// <see cref="Head"/> is a fixed-size prefix rather than the whole file: a game
    /// can ship thousands of containers and the provider is asked about every one of
    /// them, so a provider must be able to answer from the header alone.  A provider
    /// that decides to claim the file then reads whatever else it needs itself.
    /// </summary>
    public sealed class ContainerRequest
    {
        /// <summary>Full path; <c>null</c> when the caller only has bytes.</summary>
        public string Path { get; init; }

        /// <summary>First <see cref="HeadSize"/> bytes (or fewer for a short file).</summary>
        public ReadOnlyMemory<byte> Head { get; init; }

        /// <summary>Total file length, so a provider can decide whether to stream.</summary>
        public long Length { get; init; }

        /// <summary>How many bytes <see cref="Head"/> holds at most.</summary>
        public const int HeadSize = 4096;
    }

    /// <summary>
    /// One decoded payload produced by a provider.
    ///
    /// The content is a <see cref="Stream"/> rather than a byte array: a provider may
    /// be looking at tens of gigabytes, and materialising that would fail before the
    /// first shader was read.  The host disposes it.
    /// </summary>
    public sealed class ContainerStream : IDisposable
    {
        /// <summary>Node name, used for logging and as a path hint.</summary>
        public string Name { get; init; }

        /// <summary>Decoded bytes: a UnityFS container or a serialized file.</summary>
        public Stream Content { get; init; }

        /// <summary>Release the decoded stream.</summary>
        public void Dispose() => Content?.Dispose();
    }

    /// <summary>
    /// Claims and decodes an input this build cannot read on its own.
    ///
    /// A provider must return content the host already understands -- a stock
    /// UnityFS container or a serialized file -- so the rest of the pipeline is
    /// unchanged.  Returning anything else produces the same unreadable-input error
    /// the file had before, only later.
    /// </summary>
    public interface IContainerProvider
    {
        /// <summary>
        /// Cheap test, called for every candidate file.  Only
        /// <see cref="ContainerRequest.Head"/> and <see cref="ContainerRequest.Path"/>
        /// may be inspected: reading the file here defeats the point of the header
        /// snapshot.
        /// </summary>
        bool CanHandle(ContainerRequest request);

        /// <summary>
        /// Decode.  Called only after <see cref="CanHandle"/> returned true, so a
        /// failure here is a provider bug and is counted as an error rather than as
        /// "not mine".
        /// </summary>
        /// <remarks>
        /// The request is passed by value rather than <c>in</c> on purpose: this is
        /// almost always implemented as an iterator (the natural shape for "a container
        /// expands into some files"), and an iterator method cannot declare a
        /// <c>ref</c>/<c>in</c>/<c>out</c> parameter.
        /// </remarks>
        IEnumerable<ContainerStream> Read(ContainerRequest request);
    }
}
