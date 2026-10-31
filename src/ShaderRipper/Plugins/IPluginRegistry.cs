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
    }
}
