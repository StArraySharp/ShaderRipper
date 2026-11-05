using System;
using System.Collections.Generic;

namespace ShaderRipper.Plugins
{
    /// <summary>
    /// One command line's worth of already-parsed arguments, handed to a plugin's
    /// command handler.
    ///
    /// This is a read-only view rather than the parser itself: the flag vocabulary is
    /// the host's, and a plugin that added its own flags would make the help text
    /// disagree with the parser.  A handler that needs an argument takes it from
    /// <see cref="Value"/> or <see cref="Positionals"/> by the index the plugin
    /// documented for its own command.
    /// </summary>
    public sealed class CommandArgs
    {
        /// <summary>Everything on the command line, including the command word.</summary>
        public IReadOnlyList<string> Raw { get; init; }

        /// <summary>
        /// Arguments that are not options and not an option's value, in order.  The
        /// command word itself is not among them.
        /// </summary>
        public IReadOnlyList<string> Positionals { get; init; }

        /// <summary>
        /// Value of a <c>--flag value</c> pair, or null.  An empty string is the
        /// documented answer for "the flag was given without a value", which is how
        /// the host's own boolean flags read.
        /// </summary>
        public Func<string, string> Value { get; init; }

        /// <summary>True when the flag is present, whether or not it carries a value.</summary>
        public Func<string, bool> Has { get; init; }
    }

    /// <summary>
    /// A command a plugin adds to the command line.
    ///
    /// The host's own commands (<c>--game</c>, <c>--emit</c>, ...) take precedence: a
    /// plugin cannot shadow one, and a duplicate among plugins is refused at load time
    /// so the effective command set never depends on load order.
    /// </summary>
    public interface ICommandHandler
    {
        /// <summary>The command word, including its leading dashes (<c>--my-thing</c>).</summary>
        string Command { get; }

        /// <summary>One help line; shown in the usage block under a plugin heading.</summary>
        string Summary { get; }

        /// <summary>
        /// How many non-option arguments the command needs.  The host checks this
        /// before calling, so a handler can index <see cref="CommandArgs.Positionals"/>
        /// without bounds tests.
        /// </summary>
        int PositionalCount { get; }

        /// <summary>
        /// Run the command and return a process exit code.  0 means success, which is
        /// what a script wrapping the tool tests.
        /// </summary>
        int Run(CommandArgs args);
    }
}
