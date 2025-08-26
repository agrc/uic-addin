# uic-addin

An ArcGIS Pro add-in to help manage, create, and validate the UIC GIS database

## Releasing

### Automated Release Process

1. **Prepare the release** by running the "Prepare Release" workflow:
   - Go to [Actions](../../actions/workflows/prepare-release.yml)
   - Click "Run workflow"
   - Enter the version number (e.g., `3.0.0` or `3.0.0-beta.1`)
   - Click "Run workflow"

2. **Create the GitHub release**:
   - The workflow will automatically update versions and create a tag
   - Follow the instructions in the workflow output to create the release
   - Use the suggested random release name (Adjective + Dog)
   - Copy the latest changelog entries to the release description
   - Mark as pre-release if using `-beta.x` suffix

3. **Automatic deployment**:
   - Once the release is published, the release workflow automatically builds and attaches the `.esriAddinX` file

### Manual Release Process (Legacy)

<details>
<summary>Click to expand manual steps</summary>

1. Bump `Config.daml` and `package.json` version
1. Run `npm install && npm run changelog`
1. Commit changes `chore(release): x.x.x`
1. Tag it `git tag x.x.x`
1. Push to github
1. Create release on github
1. Come up with release name [Adjective](https://www.randomlists.com/random-adjectives) + [Dog](https://www.getrandomthings.com/list-dogs.php)
1. Paste in change log
1. Run a release build
1. Copy `/bin/Release/uic-addin.esriAddinX` to the release assets
1. Mark as pre-release if using the `-beta.x`

</details>

### Important References

[NAICS Index Download](https://www.census.gov/eos/www/naics/downloadables/downloadables.html)

### Release Naming Convention

[Adjective](https://www.randomlists.com/random-adjectives) + [Dog](https://www.getrandomthings.com/list-dogs.php)

### Creating a Changelog

[conventional changelog](https://github.com/conventional-changelog/conventional-changelog/tree/master/packages/conventional-changelog-cli)

`npm install -g conventional-changelog-cli`
`conventional-changelog -p angular -i CHANGELOG.md -s`

## References

- [Pro Styles](https://github.com/Esri/arcgis-pro-sdk/wiki/ProGuide-Style-Guide)
- [Available Controls](https://github.com/Esri/arcgis-pro-sdk/wiki/ProConcepts-Framework)
- [Brushes](http://arcgis.github.io/arcgis-pro-sdk/content/brushescolors/brushes.html)
- [Icon References](https://github.com/Esri/arcgis-pro-sdk/wiki/DAML-ID-Reference-Icons)
- [DAML Id References](https://github.com/Esri/arcgis-pro-sdk/wiki/ArcGIS-Pro-DAML-ID-Reference)
