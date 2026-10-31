using System;
using System.Collections.Generic;
using System.Linq;
using ShaderRipper.Plugins.Containers;
using ShaderRipper.Plugins.Emit;
using ShaderRipper.Plugins.Platforms;

namespace ShaderRipper.Plugins
{
    /// <summary>One registered backend: the descriptor plus the two things it can do.</summary>
    public sealed class PlatformEntry
    {
        /// <summary>What the backend is: id, name, payload format.</summary>
        public PlatformDescriptor Descriptor { get; init; }

        /// <summary>
        /// Enumerates the programs inside one payload.  Null for a built-in backend,
        /// whose reader lives in the pipeline rather than behind this interface.
        /// </summary>
        public ISlotReader SlotReader { get; init; }

        /// <summary>
        /// Turns one program into HLSL.  Null means "disassembly only": the programs
        /// are still enumerated and can still be written out, but no translation is
        /// attempted.  This is how d3d9 and DXIL are modelled.
        /// </summary>
        public IProgramTranslator Translator { get; init; }

        /// <summary>
        /// Registration priority.  Lower wins a slot it collides on and is consulted
        /// first; built-ins are <see cref="PluginRegistry.BuiltinPriority"/> and a
        /// plugin defaults to <see cref="PluginRegistry.DefaultPriority"/>.
        /// </summary>
        public int Priority { get; init; }

        /// <summary>True when this entry replaced one that was already registered.</summary>
        public bool Replaced { get; init; }

        /// <summary>Name of what it replaced, for the log.</summary>
        public string ReplacedName { get; init; }

        /// <summary>Renders as <c>name(id)/format priority=N</c>, with the takeover noted.</summary>
        public override string ToString() =>
            $"{Descriptor} priority={Priority}" + (Replaced ? $" (replaces {ReplacedName})" : "");
    }

    /// <summary>
    /// The registry a plugin writes into, and the lookup the pipeline reads from.
    ///
    /// Registration is closed once every plugin has been given its turn: a capability
    /// added afterwards would never be consulted, so it fails loudly instead.  Lookups
    /// are ordered by priority and then by insertion, which makes the effective order
    /// reproducible and printable.
    /// </summary>
    public sealed class PluginRegistry : IPluginRegistry
    {
        /// <summary>Priority a plugin gets when it does not ask for one.</summary>
        public const int DefaultPriority = 100;

        /// <summary>Priority of everything this build ships with.</summary>
        public const int BuiltinPriority = 0;

        private readonly List<RegisteredProvider> _providers = new();
        private readonly List<PlatformEntry> _platforms = new();
        private readonly List<RegisteredPass> _passes = new();

        private bool _closed;

        /// <summary>Diagnostics produced while registering, for the log.</summary>
        public List<string> Notes { get; } = new();

        private sealed class RegisteredProvider
        {
            public IContainerProvider Provider;
            public int Priority;
            public string Owner;
        }

        private sealed class RegisteredPass
        {
            public IEmitterPass Pass;
            public int Priority;
            public string Owner;
        }

        // ------------------------------------------------------------ registration

        /// <summary>
        /// Claim input bytes before the built-in readers see them.
        /// </summary>
        /// <param name="provider">The provider.  It is asked about every candidate
        /// file, so <see cref="IContainerProvider.CanHandle"/> must answer from the
        /// header snapshot alone.</param>
        /// <param name="priority">Lower is consulted first.  The built-ins are
        /// <see cref="BuiltinPriority"/>; omit this to run after them.</param>
        /// <exception cref="InvalidOperationException">Registration is closed.</exception>
        public void AddContainerProvider(IContainerProvider provider, int priority = DefaultPriority)
        {
            ThrowIfClosed();
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            _providers.Add(new RegisteredProvider { Provider = provider, Priority = priority, Owner = CurrentOwner });
        }

        /// <summary>
        /// Add or replace a backend.
        /// </summary>
        /// <remarks>
        /// Replacing a built-in id is allowed and logged: Unity's id space is closed, so
        /// a translator for an existing id -- Metal, say -- has nowhere else to go.  A
        /// later registration wins the slot, because execution consults the lowest
        /// priority first and an entry that never gets asked would be unusable.
        /// </remarks>
        /// <param name="descriptor">Id, name and payload format of the backend.</param>
        /// <param name="slotReader">Enumerates the programs in one payload.</param>
        /// <param name="translator">Turns one program into HLSL; null for a
        /// disassembly-only backend such as d3d9.</param>
        /// <param name="priority">Lower wins a colliding id and is consulted first.</param>
        /// <exception cref="InvalidOperationException">Registration is closed.</exception>
        public void AddPlatform(PlatformDescriptor descriptor,
                                ISlotReader slotReader,
                                IProgramTranslator translator = null,
                                int priority = DefaultPriority)
        {
            ThrowIfClosed();
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));

            int index = _platforms.FindIndex(p => p.Descriptor.Id == descriptor.Id);
            string replacedName = null;
            if (index >= 0)
            {
                var existing = _platforms[index];
                replacedName = existing.Descriptor.Name;
                // Replacement is the opposite of execution order.  Execution asks the
                // lowest number first, so a higher number would never be consulted --
                // which means a later registration must win to be usable at all.  The
                // built-ins register at 0 and a plugin defaults to 100, so a plugin
                // replaces the built-in; a second plugin can take the slot back by
                // registering again, and the log says which one won.
                if (existing.Priority > priority)
                {
                    Notes.Add($"platform {descriptor.Id} ({descriptor.Name}) from {CurrentOwner} " +
                              $"at priority {priority} loses to {existing.Descriptor.Name} " +
                              $"at {existing.Priority}");
                    return;
                }
                _platforms.RemoveAt(index);
                Notes.Add($"platform {descriptor.Id} ({replacedName}) replaced by " +
                          $"{descriptor.Name} from {CurrentOwner}");
            }

            _platforms.Add(new PlatformEntry
            {
                Descriptor = descriptor,
                SlotReader = slotReader,
                Translator = translator,
                Priority = priority,
                Replaced = replacedName != null,
                ReplacedName = replacedName,
            });
        }

        /// <summary>
        /// Add a post-processing pass over the emitted HLSL.
        /// </summary>
        /// <param name="pass">The pass.  It runs on every program body of every shader,
        /// so it must be a pure function of its arguments.</param>
        /// <param name="priority">Lower runs first.  The built-in matrix-chain folder is
        /// <see cref="BuiltinPriority"/>; omit this to run after it.</param>
        /// <exception cref="InvalidOperationException">Registration is closed.</exception>
        public void AddEmitterPass(IEmitterPass pass, int priority = DefaultPriority)
        {
            ThrowIfClosed();
            if (pass == null) throw new ArgumentNullException(nameof(pass));
            _passes.Add(new RegisteredPass { Pass = pass, Priority = priority, Owner = CurrentOwner });
        }

        /// <summary>Who is registering right now, so a note can name the plugin.</summary>
        internal string CurrentOwner { get; set; } = "builtin";

        /// <summary>Refuse any later registration.</summary>
        internal void Close() => _closed = true;

        private void ThrowIfClosed()
        {
            if (_closed)
                throw new InvalidOperationException(
                    "plugin registration is closed; capabilities must be added from Register()");
        }

        // ---------------------------------------------------------------- lookups

        /// <summary>Providers in the order they should be asked.</summary>
        public IReadOnlyList<IContainerProvider> ContainerProviders =>
            _providers.OrderBy(p => p.Priority).Select(p => p.Provider).ToList();

        /// <summary>Every backend, built-in or plugin, ordered for display.</summary>
        public IReadOnlyList<PlatformEntry> Platforms =>
            _platforms.OrderBy(p => p.Descriptor.Id).ToList();

        /// <summary>Post-processing passes in the order they should run.</summary>
        public IReadOnlyList<IEmitterPass> EmitterPasses =>
            _passes.OrderBy(p => p.Priority).Select(p => p.Pass).ToList();

        /// <summary>The backend registered for <paramref name="id"/>, or null.</summary>
        public PlatformEntry PlatformFor(int id) =>
            _platforms.FirstOrDefault(p => p.Descriptor.Id == id);

        /// <summary>
        /// Does this backend have a translator?  A backend with only a slot reader can
        /// still be enumerated and disassembled, which is what d3d9 and DXIL do.
        /// </summary>
        public bool HasTranslator(int id) => PlatformFor(id)?.Translator != null;

        /// <summary>Capabilities worth printing under <c>--plugins</c>.</summary>
        public IEnumerable<string> Describe()
        {
            foreach (var p in _providers.OrderBy(p => p.Priority))
                yield return $"container  priority {p.Priority,4}  {p.Owner}";
            foreach (var p in _platforms.OrderBy(p => p.Priority))
                yield return $"platform   priority {p.Priority,4}  {p.Descriptor.Name}({p.Descriptor.Id}) " +
                             (p.Translator != null || p.Descriptor.HasTranslator
                                  ? "translator" : "disassembly only") +
                             (p.Replaced ? $"  (replaces {p.ReplacedName})" : "");
            foreach (var p in _passes.OrderBy(p => p.Priority))
                yield return $"emitter    priority {p.Priority,4}  {p.Pass.Name} ({p.Owner})";
        }
    }
}
