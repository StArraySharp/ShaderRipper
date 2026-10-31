namespace ShaderRipper.Plugins.Emit
{
    /// <summary>
    /// A text-to-text pass over an emitted HLSL body.
    ///
    /// The built-in matrix-chain folder is one of these: it rewrites the
    /// instruction-level spelling of a matrix product into the single <c>mul</c> or
    /// Unity macro that means the same thing.  A pass must be a pure function of its
    /// arguments -- the same text in, the same text out -- because it runs on every
    /// program of every shader and may run concurrently.
    /// </summary>
    public interface IEmitterPass
    {
        /// <summary>Short name for diagnostics.</summary>
        string Name { get; }

        /// <summary>
        /// Rewrite one program body.  Return the input unchanged when the pass has
        /// nothing to do; that is cheaper than comparing strings afterwards.
        /// </summary>
        /// <param name="text">HLSL body, still inside its <c>HLSLPROGRAM</c> block.</param>
        /// <param name="macros">
        /// True when the Unity include chain is in scope, so a pass may emit
        /// <c>UnityObjectToClipPos</c> and friends.  False for a standalone program,
        /// where those macros do not exist and emitting one is a compile error.
        /// </param>
        string Apply(string text, bool macros);
    }
}
