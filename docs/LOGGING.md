# Logging in TM:PE

This guide explains:

* Why we use `TMPE.log`
* How to enable predefined debug logging
* Log methods and when to use them

> TM:PE logging is not as simple as one would expect, [because reasons](https://github.com/krzychu124/Cities-Skylines-Traffic-Manager-President-Edition/issues/349#issuecomment-512650775).

## Why `TMPE.log`?

The game log (either `output_log.txt` on Windows, or `Player.log` on Mac/Linux) is generally huge and difficult to browse, so we use a dedicated `TMPE.log` to make life a bit easier for ourselves.

The log file location depends on operating system:

* Windows: Same folder as your `output_log.txt`
* Mac: See [Issue #379](https://github.com/krzychu124/Cities-Skylines-Traffic-Manager-President-Edition/issues/379)
* Linux: Probably in `~/.config/unity3d/Colossal Order/Cities: Skylines/TMPE.log`

More importantly, TM:PE has lots of ready made logging that can be turned on or off as required...

## Log switches

Log switches determine which sets of existing log messages are output to the `TMPE.log`.

### Toggling existing switches

Build the solution in `DEBUG` mode, then launch Cities: Skylines, then open mod settings to ensure the global config is updated.

This will add a `<Debug>` section to the `TMPE_GlobalConfig.xml` file, which can be found in the following folder (may be different depending on where you installed the game):

> `C:\Program Files (x86)\Steam\steamapps\common\Cities_Skylines`

The relevant section looks like this:

```xml
<Debug>
  <Switches>
    <boolean>false</boolean>
    <boolean>false</boolean>
    ...
  </Switches>
  <ExtVehicleType>None</ExtVehicleType>
  <ExtPathMode>None</ExtPathMode>
</Debug>
```

The boolean values correlate to the `Switches[]` array in `TLM/State/ConfigData/DebugSettings.cs`.

To enable a logging for a switch, change it's value from `false` to `true` and save the changes.

The `TMPE_GlobalConfig.xml` will then need to be reloaded:

* In TM:PE mod options, select the **Maintenance** tab
* Click the **Reload global configuration** button

Logging associated with that switch will now be output to the `TMPE.log` file.

### Extended options

Some of the existing logging can be filtered to specific vehicle types or path modes. You can define those in the `TMPE_GlobalConfig.xml` via the following elements (`None` means "don't filter"):

```xml
<ExtVehicleType>None</ExtVehicleType>
<ExtPathMode>None</ExtPathMode>
```

### Adding new switches

In `TLM/State/ConfigData/DebugSettings.cs`:

* Add a new `DebugSwitch` enum value
* Add the default value to the `Switches` array

> If your `TMPE_GlobalConfig.xml` already contains the `<Debug>` section, remember to add a new `<Boolean>` element _before_ launching the game.

Once the switch is defined, you can then use it with applicable logging methods listed below.

## Logging methods

We use our own logging class that provides some features tailored to the specific needs of TM:PE.

The `static` logging class can be found in [`CSUtil.Commons/Log.cs`.](https://github.com/krzychu124/Cities-Skylines-Traffic-Manager-President-Edition/blob/master/TLM/CSUtil.Commons/Log.cs)

### Performance considerations

Large swathes of TM:PE code is called every frame. Logging needs to be performance tuned.

> Note: We could probably, at some point, make the log writer run in its own thread, but haven't got round to that yet.

#### Log.Info, Log.Warning, Log.Error, Log.\_Debug, Log.\_Trace:

* String is formatted at call site
* ⚠ Expensive if multiple `$"string {interpolations}"` are used
* ✔ Cheap if wrapped in an `if (booleanValue) { ... }`
* ✔ Cheap if there is a const string or a very simple format call with a few args
* `Log._Debug()` and `Log._Trace()` calls are removed by compiler in all `RELEASE` builds

#### Log.InfoFormat, Log.\_DebugFormat, etc.:

* Message formatted at callee site, during logging
* ⚠ Expensive if not wrapped in a `if (boolValue) { ... }` condition
* ✔ Good for very long format strings with multiple complex arguments
* ✔ As they use format string literal, it can be split multiple lines without performance penalty
* ✔ Prevents multiple calls to `string.Format()` unlike multiline `$"string {interpolations}"`
* 💲 The cost incurred: building args array (with pointers)
* `Log._DebugFormat()` calls are removed by compiler in all `RELEASE` builds

#### Log.WarningIf, Log.\_DebugIf, etc:

* If the condition is `true`, invoke the lambda and log the string it returns
* ⚠ Cannot capture `out` and `ref` values
* ✔ Lambda building is just as cheap as format args building
* ✔ Actual string is formatted ONLY if the condition holds true
* 💲 The cost incurred: Each captured variable is copied into lambda function
* `Log.DebugIf()` calls are removed by compiler in all `RELEASE` builds

#### Log.NotImpl:

* Use to log an error if an unimplemented method is called
* `Log.NotImpl()` calls are removed by compiler in all `RELEASE` builds

### All builds

These methods are available in all builds, including `RELEASE`.

> :warning: Use sparingly and avoid in-game usage as they will [completely tank frame rate](https://github.com/krzychu124/Cities-Skylines-Traffic-Manager-President-Edition/issues/411).

#### `Log.Info(str)`

Writes a simple string to the log file.

```csharp
Log.Info("Hello world!");
```

Avoid concatenating multiple `$"string {interpolations}"` as it's very expensive; use `Log.InfoFormat()` instead.

#### `Log.InfoFormat(format, args)`

```csharp
Log.InfoFormat("Some {0} {1} {3}", "string", "with", "args");
```

#### `Log.Warning(str)`

Like `Log.Info()` but at warning level. Also outputs a stack trace.

#### `Log.WarningFormat(format, args)`

Like `Log.InfoFormat()` but at warning level. Also outputs a stack trace.

#### `Log.WarningIf(switch, func)`

Similar to `Log.Warning()` but the string is generated by a lambda function (`func` param).

The function is only called, while logging, if `switch` evaluates to `true`.

```csharp
Log.WarningIf(DebugSwitch.LaneConnections.Get(), () => $"string {interpolation}");
```

#### `Log.Error(str)`

Like `Log.Info()` but at error level. Also outputs a stack trace.

#### `Log.ErrorFormat(format, args)`

Like `Log.InfoFormat()` but at error level. Also outputs a stack trace.

#### `Log.ErrorIf(switch, func)`

Like `Log.WarningIf()` but at error level. Also outputs a stack trace.

### Debug builds only

Methods prefixed with `_`, and also the `NotImpl()` method, are only available in `DEBUG` build configurations.

#### `Log._Debug(str)`

Like `Log.Info()` but at `Debug` level.

#### `Log._DebugFormat(format, args)`

Like `Log.InfoFormat()` but at `Debug` level.

#### `Log._DebugIf(switch, func)`

Like `Log.WarningIf()` but at `Debug` level (no stack trace).

#### `Log._DebugOnlyWarning(str)`

Like `Log.Warning()`.

#### `Log._DebugOnlyWarningIf(switch, func)`

Like `Log.WarningIf()`.

#### `Log._DebugOnlyError(str)`

Like `Log.Error()`.

#### `Log._Trace(str)`

Like `Log.Info()` but at `Trace` level.

Requires both the `DEBUG` and `TRACE` defines.

#### `Log.NotImpl(str)`

Like `Log.DebugOnlyError()` but prefixes the string with `Not implemented:`.
