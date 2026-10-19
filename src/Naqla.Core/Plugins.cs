namespace Naqla.Core;

public sealed record PluginManifest(int SchemaVersion, string Id, string Name, string Version, int ApiVersion, string[] Capabilities, PluginAction[] Actions);
public sealed record PluginAction(string Id, string Label, string Operation, string[] Extensions);
public sealed record InstalledPlugin(PluginManifest Manifest, bool Enabled, bool Changed, string Fingerprint);
public sealed record TextStatistics(int Characters, int Words, int Lines, int Utf8Bytes);
