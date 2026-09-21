namespace Microsoft.Build.Framework

open System

/// Provides the multithreaded-task marker while this project targets MSBuild packages that do not define it.
/// MSBuild detects the marker by namespace and type name, so older task assemblies can provide this polyfill.
/// Remove this type when Microsoft.Build.Utilities.Core and Microsoft.Build.Framework use version 18.3.3 or later.
[<AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)>]
type MSBuildMultiThreadableTaskAttribute() =
    inherit Attribute()
