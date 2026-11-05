# Plugin API reference

The extensible surface of ShaderRipper: containers, backends and emitter passes.
A plugin is a class library that implements [`IShaderRipperPlugin`](#ishaderripperplugin) and registers whichever of those it
needs. For the worked example, see [`examples/ShaderRipper.Plugin.Example`](../examples/ShaderRipper.Plugin.Example/ExamplePlugin.cs).

> Generated from the compiler's XML documentation — do not edit by hand. Regenerate with `py -3 scripts/gen_api_docs.py`.

## Contents

- **`ShaderRipper.Plugins`**
  - [`CommandArgs`](#commandargs)
  - [`EmitterPipeline`](#emitterpipeline)
  - [`ExportCompleted`](#exportcompleted)
  - [`ExportedMaterial`](#exportedmaterial)
  - [`ExportedProperty`](#exportedproperty)
  - [`ExportedShader`](#exportedshader)
  - [`ICommandHandler`](#icommandhandler)
  - [`IExportHook`](#iexporthook)
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
- **`ShaderRipper.Plugins.Hlsl`**
  - [`HlslItem`](#hlslitem)
  - [`HlslItems`](#hlslitems)
  - [`StructMember`](#structmember)
  - [`StructTools`](#structtools)
- **`ShaderRipper.Plugins.Platforms`**
  - [`EngineBuffers`](#enginebuffers)
  - [`IProgramTranslator`](#iprogramtranslator)
  - [`ISlotReader`](#islotreader)
  - [`PlatformDescriptor`](#platformdescriptor)
  - [`ProgramDescriptor`](#programdescriptor)
  - [`SlotContext`](#slotcontext)
  - [`TranslationRequest`](#translationrequest)
  - [`TranslationResult`](#translationresult)

## Namespace `ShaderRipper.Plugins`

### CommandArgs

```csharp
public class CommandArgs
```

One command line's worth of already-parsed arguments, handed to a plugin's
command handler.

This is a read-only view rather than the parser itself: the flag vocabulary is
the host's, and a plugin that added its own flags would make the help text
disagree with the parser. A handler that needs an argument takes it from
`Value` or `Positionals` by the index the plugin
documented for its own command.

#### `Has`

True when the flag is present, whether or not it carries a value.

#### `Positionals`

Arguments that are not options and not an option's value, in order. The
command word itself is not among them.

#### `Raw`

Everything on the command line, including the command word.

#### `Value`

Value of a `--flag value` pair, or null. An empty string is the
documented answer for "the flag was given without a value", which is how
the host's own boolean flags read.

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

### ExportCompleted

```csharp
public class ExportCompleted
```

What an export produced, handed to every `IExportHook` when it
finishes.

A hook that wants to write extra files has to register their names first
(through `AddExportArtifact`): the export directory
refuses to overwrite anything it did not produce, and a file a plugin writes
without registering would make the next run look foreign.

#### `Cancelled`

True when the run was cancelled, so a hook should not write.

#### `Materials`

Every material the run indexed -- the game's own plus the synthetic entries
built for shaders that ship none. A hook that has to instantiate something
to render works from this list.

#### `OutputDirectory`

The directory the export wrote to.

#### `Shaders`

Every shader the run emitted, in write order.

#### `Skipped`

Shaders seen but not written (built-ins, failures), for a summary.

#### `Translated`

Programs the run translated.

### ExportedMaterial

```csharp
public class ExportedMaterial
```

One material the game shipped, with the property values it saved.

This is the same data the export writes to its own material index; it is handed
to hooks as well so a hook can build its own view without re-reading the game
data, which it has no access to.

#### `AssetPath`

Suggested project path for the material.

#### `Colors`

Color and Vector properties, four components each.

#### `Floats`

Float and Range properties.

#### `Name`

Material asset name.

#### `Shader`

Shader name the material binds, or null when unresolved.

#### `ShaderFile`

Emitted `.shader` file, or null when that shader was not exported.

#### `Synthetic`

True when the game shipped no material and this entry was built from the
shader's Properties block: the values are Unity's defaults, not the game's.

#### `Textures`

Texture properties: property name to asset path or `fileId:pathId`.

### ExportedProperty

```csharp
public class ExportedProperty
```

One entry of a shader's Properties block.

#### `DefaultTexture`

Default texture for a Texture property (`white`, `black`).

#### `DefaultValue`

Declared default, four components (a scalar uses the first).

#### `Name`

Property name, including the leading underscore where it has one.

#### `Type`

Unity's property type id: 0 Color, 1 Vector, 2 Float, 3 Range, 4 Texture.
Kept as the raw number because that is what the asset stores and what a
reader has to branch on.

### ExportedShader

```csharp
public class ExportedShader
```

One shader an export wrote, as the post-export hooks see it.

This is a value snapshot rather than a live reference: the export is finished by
the time a hook runs, and handing out the mutable objects the exporter used
would let a hook change state that has already been written to disk.

#### `Name`

Shader name as the asset spells it (`UI/Pause Planets`).

#### `Path`

Emitted file, relative to the export root (`Assets/Shader/X.shader`).

#### `Properties`

The asset's Properties block, in declaration order.

### ICommandHandler

```csharp
public interface ICommandHandler
```

A command a plugin adds to the command line.

The host's own commands (`--game`, `--emit`, ...) take precedence: a
plugin cannot shadow one, and a duplicate among plugins is refused at load time
so the effective command set never depends on load order.

#### `Command`

The command word, including its leading dashes (`--my-thing`).

#### `PositionalCount`

How many non-option arguments the command needs. The host checks this
before calling, so a handler can index `Positionals`
without bounds tests.

#### `Summary`

One help line; shown in the usage block under a plugin heading.

#### `Run(CommandArgs)`

Run the command and return a process exit code. 0 means success, which is
what a script wrapping the tool tests.

### IExportHook

```csharp
public interface IExportHook
```

Runs after an export finishes.

This is the extension point for anything derived from the output as a whole --
a test manifest, a report, an index of what was produced. It is deliberately
not an `IEmitterPass`: a pass sees one program's text at a time
and has no idea which shaders the run covered.

Hooks run in registration order, on the export's own thread, after every file
is written. One that throws is skipped and the export still succeeds: a broken
plugin must not turn a finished export into a failed one.

#### `Name`

Short name for diagnostics.

#### `ExportFinished(ExportCompleted)`

Called once per export. `context` is a snapshot; a hook
that writes files must use `OutputDirectory`.

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

#### `AddCommand(ICommandHandler)`

Add a command word to the tool's command line.

The host answers its own words first and refuses a plugin that tries to
take one, so `--game` keeps meaning what the help text says. A word
two plugins both claim is refused as well, which is what keeps the command
set independent of load order.

#### `AddContainerProvider(IContainerProvider, int)`

Claim input bytes before the built-in readers see them. Returning false
from `CanHandle` passes the file on.

#### `AddEmitterPass(IEmitterPass, int)`

Add a post-processing pass over the emitted HLSL.

#### `AddExportArtifact(string)`

Register a file name the export directory should treat as this tool's own
output.

An export refuses to overwrite a directory holding anything it did not
produce, and that check is a name list. A hook that writes a file without
registering the name here would make the next `--overwrite` run refuse
the directory it wrote itself.

#### `AddExportHook(IExportHook)`

Register a hook that runs once an export finishes.

The place for anything derived from the output as a whole -- a test
manifest, a report, an index of what was produced. An emitter pass cannot
do it: a pass sees one program's text at a time and never learns which
shaders the run covered.

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

#### `DependsOn`

Names of plugins this one needs loaded first, or null/empty when it stands
alone.

A plugin that extends another (a testing harness driving a specific
backend, say) has to run after it, or it registers against a registry whose
platform slot is still empty. The loader sorts by this list before calling
`Register`, and reports a dependency that is not present rather
than silently loading out of order.

The name is `Name`, not the assembly name: that is what the
user sees in `--plugins`, and what a plugin author can state without
knowing how the host files assemblies.

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

#### `Commands`

Commands plugins added, in registration order.

#### `ContainerProviders`

Providers in the order they should be asked.

#### `CurrentOwner`

Who is registering right now, so a note can name the plugin.

#### `EmitterPasses`

Post-processing passes in the order they should run.

#### `ExportArtifacts`

Names the export directory should accept as this tool's own output.
Empty when no plugin registered one.

#### `ExportHooks`

Hooks to run when an export finishes, in registration order.

#### `Notes`

Diagnostics produced while registering, for the log.

#### `Platforms`

Every backend, built-in or plugin, ordered for display.

#### `BuiltinPriority`

Priority of everything this build ships with.

#### `DefaultPriority`

Priority a plugin gets when it does not ask for one.

#### `ReservedCommands`

Words the host itself answers. Kept here so the check is one list rather
than a copy of the dispatcher's switch.

#### `AddCommand(ICommandHandler)`

Add a command word to the tool's command line.

The host's own commands win: registering `--game` or any other built-in
word is refused rather than silently shadowing it, and so is a word another
plugin already claimed. That keeps the command set independent of load
order, which is the same rule the platform slots follow.

**Parameters**

- `handler` — The command.

**Exceptions**

- `InvalidOperationException` — Registration is closed, or the
word is taken.

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

#### `AddExportArtifact(string)`

Register a name the export directory should treat as this tool's own
output.

An export refuses to overwrite a directory holding anything it did not
produce, and that check is a name list. A plugin writing a file without
registering its name here would make the next `--overwrite` run look at
a foreign directory -- its own output -- and refuse.

**Parameters**

- `fileName` — File name at the export root (no directory part).

#### `AddExportHook(IExportHook)`

Register a hook that runs once the export finishes.

This is the place for anything derived from the output as a whole: a test
manifest, a report, an index. An `IEmitterPass` cannot do
it -- a pass sees one program's text at a time and never learns which
shaders the run covered.

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

#### `CommandFor(string)`

The plugin command registered for a word, or null.

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

## Namespace `ShaderRipper.Plugins.Hlsl`

### HlslItem

```csharp
public class HlslItem
```

One top-level item of a translated HLSL program.

#### `Kind`

`pp` (preprocessor line), `cbuffer`, `struct`,
`decl` (a global declaration), `func` (a function definition) or
`semi` (a stray semicolon).

#### `Name`

Declared name, when the item has one (struct name, entry point).

#### `Register`

Register a cbuffer names, or -1.

#### `Text`

The item's text, taken verbatim from the input.

### HlslItems

```csharp
public class HlslItems
```

Splits a program body into its top-level items, for plugin passes that need to
reason about declarations rather than lines.

The split runs on the project's `Hlsl.g4` parse tree, so a
`float4 _Color;` inside a function body is never mistaken for a global --
which is exactly the bug a line scanner produces. The item texts come from the
original source over the line spans the tree reports, so joining them
reproduces the input.

A body the grammar cannot carry comes back as one item of kind `raw`, and
`parseErrors` says how many errors the parser logged. A pass
that only wants "declarations before the first function" can stop at the first
`func` either way.

#### `Split(string, Int32@)`

Split one program body.

**Parameters**

- `hlsl` — Program text, without the surrounding ShaderLab tags.
- `parseErrors` — Number of parse errors the grammar reported.

### StructMember

```csharp
public class StructMember
```

One member of an HLSL interface struct.

Plain fields rather than a record so a plugin compiled against an older minor
keeps working when the host adds one.

#### `Name`

Member name.

#### `Semantic`

D3D semantic after the colon, e.g. `TEXCOORD0`.

#### `Type`

Declared type, e.g. `float4`.

#### `ToString`

Renders one member declaration line, without indentation.

### StructTools

```csharp
public class StructTools
```

Structural edits on emitted HLSL text, offered to plugins.

A translator that produces interface structs of its own needs the same two
operations the built-in SPIR-V route does: fold a vertex-output/pixel-input
pair into one struct so the stages link, and rewrite a struct's semantics from
a name table when the backend only recorded location numbers. Both live here
rather than in the plugin that happens to need them first, because a second
backend (Metal, WebGPU) faces exactly the same shape.

Everything is a pure function of its arguments -- no static state, no file
access -- so it is safe to call concurrently from any number of translators.

#### `IsSystemSemantic(string)`

True when the semantic is D3D's own rather than an interpolator:
`SV_Position` and the render targets sit outside the attribute
numbering and must not consume a slot when a table is walked positionally.

#### `Members(string)`

The members of a struct declaration, in declaration order. Only the
`type name : semantic;` shape a translator emits is understood; a
member without a semantic is skipped.

#### `MergeInterface(string, string, string, string, string, string)`

Fold a vertex-output / pixel-input struct pair into one struct so the two
stages link.

Unity's pixel-stage link needs the vertex's output members and the
fragment's input members declared as one shape carrying
`SV_Position`; a pair of separate declarations with the same bytes
does not bind, and the material then draws nothing with no compile error.
The two programs also name their copies independently -- the vertex calls
its colour `vs_134`, the fragment `ps_22` -- so the merge keeps
the vertex's spelling and rewrites the fragment's
`parameter.<name>` accesses.

The fragment normally reads a subset of what the vertex writes (a vertex
program emits interpolators a keyword-off path never consumes), so the
merge keeps the vertex's full member list and matches each fragment member
to the vertex member with the same semantic. It declines (returns
`Unchanged`) when a fragment member has no vertex
counterpart: that reads an interpolator the vertex never writes, which no
spelling of the merged struct could fix.

**Parameters**

- `vertex` — Vertex program text.
- `fragment` — Fragment program text.
- `vertexOutput` — Name of the vertex's output struct.
- `pixelInput` — Name of the pixel's input struct.
- `mergedName` — Name for the merged struct.
- `parameter` — Parameter name both entries use.

#### `PatchSemantics(string, string, String>)`

Rewrite the `: semantic` of the named struct's members from a
positional table, leaving names and types alone.

Each table entry is a declaration name in the source language
(`in_COLOR0`, `vs_COLOR0`) and is mapped through
`SemanticOf`. System semantics are skipped and do not consume
a table entry. When the table runs out, the remaining members keep what
they had -- guessing past the end would invent a binding.

**Parameters**

- `hlsl` — Program text to rewrite.
- `structName` — Struct to patch.
- `names` — Source-language declaration names, in location order.

#### `ReadStruct(string, string)`

The full text of a struct declaration, or null when the text has none.

The match is on the declaration head, so a name that is a prefix of
another (`Foo` and `FooBar`) is not confused for the shorter one.

#### `ReplaceStruct(string, string, string)`

Replace a struct declaration with `replacement`; an empty
replacement deletes it. Returns the input unchanged when the struct is not
declared.

#### `SemanticOf(string)`

The D3D semantic a source-language declaration name stands for:
`in_POSITION0` / `vs_POSITION0` is POSITION, `in_COLOR0` is
COLOR0, `vs_TEXCOORD1` is TEXCOORD1. Null for a name whose stem is
not one this understands, so the caller keeps the original spelling.

## Namespace `ShaderRipper.Plugins.Platforms`

### EngineBuffers

```csharp
public class EngineBuffers
```

Unity's built-in constant-buffer layout, for backends whose payload does not
carry one.

A payload records the member names of the buffers it binds, but not of the
engine's own: `UnityPerDraw` is recorded as a size with no members at all.
A translated program still reads it, and the engine layout is fixed, so it can
be recovered from the include that declares it rather than left positional.

#### `At(string, int)`

The member covering a row, or null when the table does not describe it.

#### `ForBuffer(string)`

Every member the layout table knows for one buffer.

#### `Known(string)`

True when the layout table describes this buffer at all.

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

#### `VertexAttributes`

GLSL vertex attribute names in declaration order: `Inputs` are the mesh
streams the vertex reads (`in_COLOR0`, ...), `Interpolators` are what it
writes out (`vs_COLOR0`, ...) -- the fragment's inputs repeat those names,
and both lists are the only record of which semantic each SPIR-V location
carries. Null when the asset shipped no GL build.

#### `VertexInterpolators`

The vertex's interpolator outputs, in declaration order.

### TranslationResult

```csharp
public class TranslationResult
```

Outcome of translating one program.

#### `Hlsl`

HLSL program body; null when nothing was produced.

#### `Notes`

Comment lines to write into the `.shader`.

#### `Original`

The original program text, when the backend can spell it -- a GLSL source,
a SPIR-V disassembly, a byte dump. Written into the `.shader` as a
comment block when the user asks for it, so the recovered source and the
bytes it came from sit in the same file instead of two directories.

Null when the backend has no readable form of its own (the built-in DXBC
route supplies the disassembly directly, not through this field).

#### `OriginalKind`

What `Original` is, for the comment's heading.

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
