# uic-addin

An ArcGIS Pro add-in to help manage, create, and validate the UIC GIS database

## Releasing

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
