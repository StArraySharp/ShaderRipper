# Plugin API reference

The extensible surface of ShaderRipper: containers, backends and emitter passes.
A plugin is a class library that implements [`IShaderRipperPlugin`](#ishaderripperplugin) and registers whichever of those it
needs. For the worked example, see [`examples/ShaderRipper.Plugin.Example`](../examples/ShaderRipper.Plugin.Example/ExamplePlugin.cs).

> Generated from the compiler's XML documentation — do not edit by hand. Regenerate with `py -3 scripts/gen_api_docs.py`.

## Contents

- **`ShaderRipper.Plugins`**
  - [`EmitterPipeline`](#emitterpipeline)
  - [`IPluginRegistry`](#ipluginregistry)
  - [`IShaderRipperPlugin`](#ishaderripperplugin)
  - [`PlatformEntry`](#platformentry)
  - [`PluginApiVersion`](#pluginapiversion)
  - [`PluginCall`](#plugincall)
  - [`PluginRegistry`](#pluginregistry)
- **`ShaderRipper.Plugins.Containers`**
  - [`ContainerRequest`](#containerrequest)
  - [`ContainerStream`](#containerstream)
  - [`IContainerProvider`](#icontainerprovider)
- **`ShaderRipper.Plugins.Emit`**
  - [`IEmitterPass`](#iemitterpass)
- **`ShaderRipper.Plugins.Platforms`**
  - [`IProgramTranslator`](#iprogramtranslator)
  - [`ISlotReader`](#islotreader)
  - [`PlatformDescriptor`](#platformdescriptor)
  - [`ProgramDescriptor`](#programdescriptor)
  - [`SlotContext`](#slotcontext)
  - [`TranslationRequest`](#translationrequest)
  - [`TranslationResult`](#translationresult)

## Namespace `ShaderRipper.Plugins`

### EmitterPipeline

```csharp
public class EmitterPipeline
```

The emitter pipeline: every registered `IEmitterPass` in priority
order.

This is the single place the translators hand their HLSL to for post-processing,
so a plugin pass runs on the same text as the built-in matrix-chain folder and
in a defined order relative to it. The pipeline is set once per run; when no
registry is installed (a host calling the library directly, a unit test), it
falls back to the built-in pass alone, which is what the code did before the
registry existed.

#### `Passes`

The passes that will run, for diagnostics and tests.

#### `Install(PluginRegistry)`

Install the passes from a registry. Null restores the default.

#### `Run(string, bool)`

Run every pass over one program body.

A pass that throws is skipped and the text keeps the state it had before
that pass: a broken plugin must not take the export down, and it must not
leave a half-rewritten body either.

### IPluginRegistry

```csharp
public interface IPluginRegistry
```

What a plugin may add. Handed to `Register`
once, then closed.

Every slot takes a priority: lower runs first, built-ins are 0, plugins default
to 100. Ordering is explicit rather than "last registration wins" because the
effective order has to be printable and testable -- this codebase has been
bitten before by results that depended on an implicit ordering.

#### `AddContainerProvider(IContainerProvider, int)`

Claim input bytes before the built-in readers see them. Returning false
from `CanHandle` passes the file on.

#### `AddEmitterPass(IEmitterPass, int)`

Add a post-processing pass over the emitted HLSL.

#### `AddPlatform(PlatformDescriptor, ISlotReader, IProgramTranslator, int)`

Add or replace a backend. Replacing a built-in id is allowed (and logged):
Unity's id space is closed, so a translator for an existing id -- Metal, say
-- has nowhere else to go.

### IShaderRipperPlugin

```csharp
public interface IShaderRipperPlugin
```

Entry point of an external assembly.

The host instantiates one instance per plugin, calls `Register`
once, and keeps the instance for the life of the process -- so a plugin may
cache per-game state (an index table, a key schedule) between files. Only
`Register` is guaranteed to run on a single thread; the capabilities it
registers are called concurrently and must be thread-safe if they carry
mutable state.

#### `ApiVersion`

Contract the plugin was compiled against.

#### `Description`

Optional one-line description; null is allowed.

#### `Name`

Shown by `--plugins` and in the log.

#### `Register(IPluginRegistry)`

Register capabilities. The registry is closed after this returns, so a
later call throws instead of silently having no effect.

### PlatformEntry

```csharp
public class PlatformEntry
```

One registered backend: the descriptor plus the two things it can do.

#### `Descriptor`

What the backend is: id, name, payload format.

#### `Priority`

Registration priority. Lower wins a slot it collides on and is consulted
first; built-ins are `BuiltinPriority` and a
plugin defaults to `DefaultPriority`.

#### `Replaced`

True when this entry replaced one that was already registered.

#### `ReplacedName`

Name of what it replaced, for the log.

#### `SlotReader`

Enumerates the programs inside one payload. Null for a built-in backend,
whose reader lives in the pipeline rather than behind this interface.

#### `Translator`

Turns one program into HLSL. Null means "disassembly only": the programs
are still enumerated and can still be written out, but no translation is
attempted. This is how d3d9 and DXIL are modelled.

#### `ToString`

Renders as `name(id)/format priority=N`, with the takeover noted.

### PluginApiVersion

```csharp
public readonly struct PluginApiVersion
```

Version of the plugin contract, independent of the library version.

A plugin compiled against a different `Major` is refused at load
time rather than failing somewhere in the middle of an export: the interface
can only grow within a major version, so a lower minor still works.

#### `Current`

The contract this build implements. A plugin should report this value
unless it deliberately targets an older one.

#### `Major`

Breaking-change counter. It is compared for equality at load time: a
mismatch rejects the plugin outright, because an interface that gained a
member cannot be implemented by an assembly compiled against the old shape.

#### `Minor`

Additive counter. A plugin built against a lower minor still loads: every
change within a major version only adds members or defaults, so an old
implementation stays a valid one.

#### `PluginApiVersion(int, int)`

Describe a contract version.

**Parameters**

- `major` — Breaking-change counter; must match the host exactly.
- `minor` — Additive counter; a lower value is accepted.

#### `Equals(PluginApiVersion)`

Value equality over both counters.

**Parameters**

- `other` — The version to compare against.

#### `Equals(object)`

Value equality over both counters.

**Parameters**

- `obj` — The value to compare against, if it is a version.

#### `GetHashCode`

A hash over both counters, so the version can key a dictionary.

#### `IsCompatibleWith(PluginApiVersion)`

Same major version, so the interface a plugin was compiled against is still
the one the host provides.

**Parameters**

- `host` — The version the running host implements.

#### `ToString`

Renders as `major.minor`, the form the loader logs.

#### `op_Equality(PluginApiVersion, PluginApiVersion)`

Value equality.

**Parameters**

- `a` — Left operand.
- `b` — Right operand.

#### `op_Inequality(PluginApiVersion, PluginApiVersion)`

Value inequality.

**Parameters**

- `a` — Left operand.
- `b` — Right operand.

### PluginCall

```csharp
public class PluginCall
```

Wraps every call into plugin code.

The rule this codebase already follows for its own risky paths applies doubly
to third-party code: a plugin that throws must not abort an export. Each call
is isolated, the failure is recorded once -- a plugin that fails on every
program of a 400-shader run would otherwise bury the real diagnostics -- and
the caller gets the "no opinion" value so the pipeline carries on.

#### `Reset`

Clear the deduplication set. Called at the start of a run so two exports in
one process each report their own failures.

#### `Try(string, Action, String>)`

Run a void action, recording a note if it throws.

#### `Try``1(string, Func<``0>, ``0, String>)`

Run `body`, returning `fallback` and
recording a note if it throws.

### PluginRegistry

```csharp
public class PluginRegistry
```

The registry a plugin writes into, and the lookup the pipeline reads from.

Registration is closed once every plugin has been given its turn: a capability
added afterwards would never be consulted, so it fails loudly instead. Lookups
are ordered by priority and then by insertion, which makes the effective order
reproducible and printable.

#### `ContainerProviders`

Providers in the order they should be asked.

#### `CurrentOwner`

Who is registering right now, so a note can name the plugin.

#### `EmitterPasses`

Post-processing passes in the order they should run.

#### `Notes`

Diagnostics produced while registering, for the log.

#### `Platforms`

Every backend, built-in or plugin, ordered for display.

#### `BuiltinPriority`

Priority of everything this build ships with.

#### `DefaultPriority`

Priority a plugin gets when it does not ask for one.

#### `AddContainerProvider(IContainerProvider, int)`

Claim input bytes before the built-in readers see them.

**Parameters**

- `provider` — The provider. It is asked about every candidate
file, so `CanHandle` must answer from the
header snapshot alone.
- `priority` — Lower is consulted first. The built-ins are
`BuiltinPriority`; omit this to run after them.

**Exceptions**

- `InvalidOperationException` — Registration is closed.

#### `AddEmitterPass(IEmitterPass, int)`

Add a post-processing pass over the emitted HLSL.

**Parameters**

- `pass` — The pass. It runs on every program body of every shader,
so it must be a pure function of its arguments.
- `priority` — Lower runs first. The built-in matrix-chain folder is
`BuiltinPriority`; omit this to run after it.

**Exceptions**

- `InvalidOperationException` — Registration is closed.

#### `AddPlatform(PlatformDescriptor, ISlotReader, IProgramTranslator, int)`

Add or replace a backend.

**Parameters**

- `descriptor` — Id, name and payload format of the backend.
- `slotReader` — Enumerates the programs in one payload.
- `translator` — Turns one program into HLSL; null for a
disassembly-only backend such as d3d9.
- `priority` — Lower wins a colliding id and is consulted first.

**Exceptions**

- `InvalidOperationException` — Registration is closed.

Replacing a built-in id is allowed and logged: Unity's id space is closed, so
a translator for an existing id -- Metal, say -- has nowhere else to go. A
later registration wins the slot, because execution consults the lowest
priority first and an entry that never gets asked would be unusable.

#### `Close`

Refuse any later registration.

#### `Describe`

Capabilities worth printing under `--plugins`.

#### `HasTranslator(int)`

Does this backend have a translator? A backend with only a slot reader can
still be enumerated and disassembled, which is what d3d9 and DXIL do.

#### `PlatformFor(int)`

The backend registered for `id`, or null.

## Namespace `ShaderRipper.Plugins.Containers`

### ContainerRequest

```csharp
public class ContainerRequest
```

One input file offered to the container providers.

`Head` is a fixed-size prefix rather than the whole file: a game
can ship thousands of containers and the provider is asked about every one of
them, so a provider must be able to answer from the header alone. A provider
that decides to claim the file then reads whatever else it needs itself.

#### `Head`

First `HeadSize` bytes (or fewer for a short file).

#### `Length`

Total file length, so a provider can decide whether to stream.

#### `Path`

Full path; `null` when the caller only has bytes.

#### `HeadSize`

How many bytes `Head` holds at most.

### ContainerStream

```csharp
public class ContainerStream
```

One decoded payload produced by a provider.

The content is a `Stream` rather than a byte array: a provider may
be looking at tens of gigabytes, and materialising that would fail before the
first shader was read. The host disposes it.

#### `Content`

Decoded bytes: a UnityFS container or a serialized file.

#### `Name`

Node name, used for logging and as a path hint.

#### `Dispose`

Release the decoded stream.

### IContainerProvider

```csharp
public interface IContainerProvider
```

Claims and decodes an input this build cannot read on its own.

A provider must return content the host already understands -- a stock
UnityFS container or a serialized file -- so the rest of the pipeline is
unchanged. Returning anything else produces the same unreadable-input error
the file had before, only later.

#### `CanHandle(ContainerRequest)`

Cheap test, called for every candidate file. Only
`Head` and `Path`
may be inspected: reading the file here defeats the point of the header
snapshot.

#### `Read(ContainerRequest)`

Decode. Called only after `CanHandle` returned true, so a
failure here is a provider bug and is counted as an error rather than as
"not mine".

The request is passed by value rather than `in` on purpose: this is
almost always implemented as an iterator (the natural shape for "a container
expands into some files"), and an iterator method cannot declare a
`ref`/`in`/`out` parameter.

## Namespace `ShaderRipper.Plugins.Emit`

### IEmitterPass

```csharp
public interface IEmitterPass
```

A text-to-text pass over an emitted HLSL body.

The built-in matrix-chain folder is one of these: it rewrites the
instruction-level spelling of a matrix product into the single `mul` or
Unity macro that means the same thing. A pass must be a pure function of its
arguments -- the same text in, the same text out -- because it runs on every
program of every shader and may run concurrently.

#### `Name`

Short name for diagnostics.

#### `Apply(string, bool)`

Rewrite one program body. Return the input unchanged when the pass has
nothing to do; that is cheaper than comparing strings afterwards.

**Parameters**

- `text` — HLSL body, still inside its `HLSLPROGRAM` block.
- `macros` — True when the Unity include chain is in scope, so a pass may emit
`UnityObjectToClipPos` and friends. False for a standalone program,
where those macros do not exist and emitting one is a compile error.

## Namespace `ShaderRipper.Plugins.Platforms`

### IProgramTranslator

```csharp
public interface IProgramTranslator
```

Turns one compiled program into HLSL.

#### `CanTranslate(TranslationRequest)`

Cheap check; false means "not my syntax", and the program is skipped.

#### `Translate(TranslationRequest)`

Translate. Never returns a partially valid body: null means failure.

### ISlotReader

```csharp
public interface ISlotReader
```

Enumerates the compiled programs inside a decoded platform payload.

This is a property of the *format*, not of the translator: a DXBC slot is a
per-program header followed by cbuffer name tables followed by a container,
a GLSL slot is bare text, a Metal slot is a 0xF00DCAFE archive. The host has
to know what is in the slot before it can translate anything, so the two are
separate interfaces.

#### `Read(Byte>, SlotContext)`

Enumerate the programs in one decoded payload, in the order they appear.

**Parameters**

- `payload` — The decompressed platform slot.
- `context` — Which backend this is and which blob indices the asset
expects; use it to pair each program with its `m_BlobIndex`.

**Returns** — The programs found; empty when the payload holds none.

A descriptor whose `Offset` and
`Length` fall outside `payload`
is counted as skipped rather than failed, so a truncated slot degrades into a
partial result instead of aborting the shader.

### PlatformDescriptor

```csharp
public class PlatformDescriptor
```

One row of the backend table: what a `m_Platforms` id means.

Ids come from `UnityEditor.Rendering.ShaderCompilerPlatform` read out of
the editor by reflection, which is why d3d11 is 4 and Vulkan is 18. A plugin
may also add an id this build has never heard of.

#### `Format`

Payload format: dxbc / glsl / d3d9 / spirv / metal / wgsl.

#### `HasSource`

True when the payload is itself readable source (GLSL, Metal).

#### `HasTranslator`

False when the payload can only be disassembled, not translated.

#### `Id`

The numeric id stored in `m_Platforms`.

#### `Name`

Name accepted by `--platform`; lower case, no spaces.

#### `OverridesBuiltin`

True when this descriptor replaces one the build already had.

#### `ToString`

Renders as `name(id)/format`, the form the registry logs.

### ProgramDescriptor

```csharp
public class ProgramDescriptor
```

One compiled program found inside a decoded platform payload.

#### `BlobIndex`

Unity's `m_BlobIndex`; the output file name uses it.

#### `Entry`

Entry point name when the archive records one (Metal does).

#### `Length`

Length of the program inside the payload, in bytes.

#### `Offset`

Offset of the program inside the payload, in bytes.

#### `Stage`

"vs" / "ps" / "cs", or `null` when only the translation can tell.
A null stage means `Stage` decides.

### SlotContext

```csharp
public class SlotContext
```

Context handed to a slot reader.

#### `BlobIndices`

`SubProgram.m_BlobIndex` values, in slot order.

#### `Platform`

The backend this payload belongs to.

### TranslationRequest

```csharp
public class TranslationRequest
```

Everything one translation needs, so a translator never reads game data.

#### `BlobIndex`

Unity's `m_BlobIndex`.

#### `Entry`

Entry point name, when the format records one.

#### `Payload`

Program bytes, already decompressed out of the platform slot.

#### `Platform`

The backend this program came from.

#### `PropertyNames`

The shader's property names, in declaration order.

#### `Stage`

Stage from `Stage`; may be null.

### TranslationResult

```csharp
public class TranslationResult
```

Outcome of translating one program.

#### `Hlsl`

HLSL program body; null when nothing was produced.

#### `Notes`

Comment lines to write into the `.shader`.

#### `Stage`

Stage the translation turned out to be. Overrides
`Stage` when that was unknown.

#### `Target`

Shader model the body needs, as 40 / 50; 0 keeps the default.

#### `Unsupported`

The translator decided this payload is beyond it -- a newer syntax version
say. Counted as skipped rather than failed, because the input was valid.

#### `WantsMacros`

True when `Hlsl` needs the Unity include chain (and so may use
`UnityObjectToClipPos` and friends). False for a standalone program.

---

*Source of truth: `ShaderRipper.xml`.*
