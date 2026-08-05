# Plugins

Drop a .NET 8 class-library DLL here and AVAK loads it on the next start.

Any public class with a parameterless constructor that implements
`AVAK.Engine.IDetectionProvider` becomes a detection provider and appears on the
**Rules** page, where it can be switched on and off.

See `docs/EXTENDING.md` for the interface contract and `samples/SamplePlugin`
for a working provider you can copy.

Nothing in this folder is loaded with reduced trust: a plugin runs with the same
rights as AVAK itself. Only add DLLs you built or audited.
