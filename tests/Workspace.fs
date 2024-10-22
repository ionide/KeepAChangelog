module Workspace

open EasyBuild.FileSystemProvider

type Workspace = RelativeFileSystem<".">

type VirtualWorkspace =
    VirtualFileSystem<
        ".",
        """
packages/
"""
     >
