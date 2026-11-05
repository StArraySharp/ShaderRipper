using ShaderRipper.Plugins.Containers;
using ShaderRipper.Plugins.Emit;
using ShaderRipper.Plugins.Platforms;

namespace ShaderRipper.Plugins
{
    /// <summary>
    /// What a plugin may add.  Handed to <see cref="IShaderRipperPlugin.Register"/>
    /// once, then closed.
    ///
    /// Every slot takes a priority: lower runs first, built-ins are 0, plugins default
    /// to 100.  Ordering is explicit rather than "last registration wins" because the
    /// effective order has to be printable and testable -- this codebase has been
    /// bitten before by results that depended on an implicit ordering.
    /// </summary>
    public interface IPluginRegistry
    {
        /// <summary>
        /// Claim input bytes before the built-in readers see them.  Returning false
        /// from <see cref="IContainerProvider.CanHandle"/> passes the file on.
        /// </summary>
        void AddContainerProvider(IContainerProvider provider, int priority = PluginRegistry.DefaultPriority);

        /// <summary>
        /// Add or replace a backend.  Replacing a built-in id is allowed (and logged):
        /// Unity's id space is closed, so a translator for an existing id -- Metal, say
        /// -- has nowhere else to go.
        /// </summary>
        void AddPlatform(PlatformDescriptor descriptor,
                         ISlotReader slotReader,
                         IProgramTranslator translator = null,
                         int priority = PluginRegistry.DefaultPriority);

        /// <summary>Add a post-processing pass over the emitted HLSL.</summary>
        void AddEmitterPass(IEmitterPass pass, int priority = PluginRegistry.DefaultPriority);

        /// <summary>
        /// Add a command word to the tool's command line.
        ///
        /// The host answers its own words first and refuses a plugin that tries to
        /// take one, so <c>--game</c> keeps meaning what the help text says.  A word
        /// two plugins both claim is refused as well, which is what keeps the command
        /// set independent of load order.
        /// </summary>
        void AddCommand(ICommandHandler handler);

        /// <summary>
        /// Register a hook that runs once an export finishes.
        ///
        /// The place for anything derived from the output as a whole -- a test
        /// manifest, a report, an index of what was produced.  An emitter pass cannot
        /// do it: a pass sees one program's text at a time and never learns which
        /// shaders the run covered.
        /// </summary>
        void AddExportHook(IExportHook hook);

        /// <summary>
        /// Register a file name the export directory should treat as this tool's own
        /// output.
        ///
        /// An export refuses to overwrite a directory holding anything it did not
        /// produce, and that check is a name list.  A hook that writes a file without
        /// registering the name here would make the next <c>--overwrite</c> run refuse
        /// the directory it wrote itself.
        /// </summary>
        void AddExportArtifact(string fileName);
    }
}
