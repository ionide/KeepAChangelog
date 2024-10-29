module Workspace

open EasyBuild.FileSystemProvider

type Workspace = RelativeFileSystem<".">

type VirtualWorkspace =
    VirtualFileSystem<
        ".",
        """
test-nupkgs/
test-package-cache/
fixtures/
    bin/
        Release/
"""
     >
