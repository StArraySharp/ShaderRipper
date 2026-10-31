using ShaderRipper.Emit;
using ShaderRipper.Plugins.Emit;

namespace ShaderRipper.Plugins
{
    /// <summary>
    /// Exposes the built-in matrix-chain folder as an <see cref="IEmitterPass"/>.
    ///
    /// The folder already has exactly the shape the interface asks for -- text in,
    /// text out, with a `macros` flag -- so this is a two-line adapter.  Registering
    /// it through the same registry a plugin uses is what keeps the pipeline uniform:
    /// there is no separate "built-in passes" path that a plugin could not join.
    /// </summary>
    internal sealed class MatrixChainPass : IEmitterPass
    {
        /// <inheritdoc />
        public string Name => "matrix-chain";

        /// <inheritdoc />
        public string Apply(string text, bool macros) => MatrixChain.FoldText(text, macros);
    }
}
