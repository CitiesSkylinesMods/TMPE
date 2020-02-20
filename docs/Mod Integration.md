# Mod Integration Guide

This guide is in early stages of construction and we'll add to it as time permits.

## Determine TM:PE version

From TM:PE version 1.11.0 onwards, it is possible to quickly and accurately determine the version of TM:PE via it's assembly info as follows:

```cs
Version TMPE_Version = Assembly.Load("TrafficManager").GetName().Version;

if (TMPE_Version < new Version(11)) {
    // Version 11.0 and earlier all had assembly version 1.0.*.*
} else if (TMPE_Version >= new Version(11, 1, 0)) {
   // Version 11.1.0 and above all have correct assembly version in form M.m.b.* (Major, minor, build, *)
}
```
