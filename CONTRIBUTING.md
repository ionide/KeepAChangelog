## Contributing


## Release

1. Update version in CHANGELOG.md and add notes
    1. If possible link the pull request of the changes and mention the author of the pull request
2. Update `src.Directory.Build.props`
3. Create new commit
    1. `git add CHANGELOG.md`
    1. `git commit -m "changelog for v0.45.0"`
4. Make a new version tag (for example, `v0.45.0`)
    1. `git tag v0.45.0`
5. Push changes to the repo.
    1. `git push --atomic [remote] main v0.45.0`
        - Replace `[remote]` with your remote name, usually `origin` or `upstream`