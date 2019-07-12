# Slick
A light-weight group communication tool [Experimental]

## To do
Immediate next steps:

* [x] Dynamic load/unload images
* [x] Multiple pages (target different folders)
  * [x] import from old format (basic)
* [x] Change image file format, use larger tiles.
  * [x] Packed single file format? (blob database or similar) - Maybe the version control could be linked in with this? (COW-like block store?)
  * [x] Implement Undo
  * [x] Lock tiles while they are loading (prevent drawing on or saving the grey proxies)
    * [ ] Special lock for tiles that have not completed DB query yet
  * [x] Invalidate unknown tile indexes (prevent drawing when we haven't made the proxy)
* [x] Scaling 'map' mode
  * [x] double-click to zoom to position
  * [x] change draw to scroll in map mode
* [x] Interest pins and quick-list
* [x] Open through file
* [x] Select region and export
* [x] Typing onto the canvas (floating tool & merge, like image import)
* [x] Importing images onto the canvas
  * [ ] Import on scaled canvas
* [x] Fix the drawing-over-tile-edge issue
* [ ] Hilighting ink
* [ ] Separate log and DB writer thread (to eliminate pauses)

## Possible features

* [ ] mediator endpoint (Azure func or similar? RabbitMQ?)
* [ ] peer-to-peer link up based on mediator?
* [ ] shared 'infinite whiteboard' inside group
  * [ ] 'strands' to loosely link conversations
  * [ ] 'bubbles' to tightly link conversations
  * [ ] a way to push off-topic or humourous chat aside without removing it
* [ ] peer-to-peer audio/video link
  * [ ] try the 3D CDF wavelet thing here?
* [ ] Non-euclidian surface -- as you scroll away, content is reduced but not hidden.
* [ ] Android client

## Links

https://docs.microsoft.com/en-us/dotnet/api/system.windows.input.stylus?view=netframework-4.7.2
https://www.researchgate.net/profile/Eugenio_Urdapilleta/publication/272195274/figure/fig1/AS:295001616601089@1447345274310/A-2D-model-of-hyperbolic-geometry-a-The-half-PS-is-generated-by-revolution-of-the.png
