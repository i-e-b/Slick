using System;
using Windows.Foundation;
using Windows.Graphics.DirectX;
using Windows.UI;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using JetBrains.Annotations;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;

namespace SlickUWP.Canvas
{
    /// <summary>
    /// Looks after UI elements of a tile
    /// </summary>
    public class CachedTile : ICachedTile
    {
        [NotNull] private readonly CanvasControlAsyncProxy UiCanvas;

        public TileState State;
        public bool IsSelected = false;

        private byte[] RawImageData;
        private float _x, _y;
        private volatile bool _detached = false;

        public const int ByteSize = 256 * 256 * 4;
        public int Width => 256;
        public int Height => 256;

        public CachedTile([NotNull]Panel container)
        {
            State = TileState.Locked;
            UiCanvas = Win2dCanvasPool.Employ(container, this);
            UiCanvas.QueueAction(canv =>
            {
                canv.RenderTransform = new TranslateTransform { X = _x, Y = _y };
                //canv.Invalidate();
            });
        }

        ~CachedTile() {
            if (!_detached)
                throw new Exception("Cached tile was garbage collected without being detached!");
        }
        

        public void SetTileData(byte[] rawData) {
            RawImageData = rawData;
            UiCanvas.QueueAction(canv => canv?.Invalidate());
        }

        public byte[] GetTileData()
        {
            return RawImageData;
        }

        public void DrawToSession([NotNull]CanvasControl sender, [NotNull]CanvasDrawingSession g, bool drawGrid = false)
        {
            switch (State)
            {
                case TileState.Locked:
                    if (RawImageData != null)
                    {
                        g.Clear(Colors.DarkKhaki); // wrong state
                    }
                    else
                    {
                        g.Clear(Colors.DarkGray); // which is lighter than 'Gray'
                    }
                    return;
                    
                case TileState.Corrupted:
                    g.Clear(Colors.Red);
                    return;

                case TileState.Empty:
                    g.Clear(Colors.White);
                    if (drawGrid) DrawGrid(g, Color.FromArgb(100, 155, 155, 155));
                    if (IsSelected) DrawSelection(g);
                    return;

                case TileState.Ready:

                    if (RawImageData == null)
                    {
                        g.Clear(Colors.Fuchsia); // Should not happen!
                        return;
                    }

                    g.Clear(Colors.White);
                    try
                    {
                        using (var bmp = CanvasBitmap.CreateFromBytes(sender, RawImageData, 256, 256, // these two should be doubled if we interpolate
                            DirectXPixelFormat.B8G8R8A8UIntNormalized, 96, CanvasAlphaMode.Premultiplied))
                        {
                            g.DrawImage(bmp, new Rect(0, 0, 256, 256));
                            g.Flush();
                            // you'll get "Exception thrown at 0x12F9AF43 (Microsoft.Graphics.Canvas.dll) in SlickUWP.exe: 0xC0000005: Access violation reading location 0x1B2EEF78. occurred"
                            // if you fail to flush before disposing of the bmp
                        }
                    }
                    catch
                    {
                        g.Clear(Colors.DarkOrange);
                    }

                    if (drawGrid) DrawGrid(g, Color.FromArgb(100, 155, 155, 155));
                    if (IsSelected) DrawSelection(g);
                    g.Flush();

                    return;

                default:
                    //g.Clear(Colors.MediumTurquoise);
                    //return;
                    throw new Exception("Non exhaustive switch in Tile_Draw");
            }
        }

        private void DrawSelection(CanvasDrawingSession g)
        {
            g.Blend = CanvasBlend.SourceOver;
            g.FillRectangle(-1,-1, Width+2, Height+2, Color.FromArgb(127, 127, 127, 127));
        }

        private void DrawGrid([NotNull]CanvasDrawingSession g, Color c)
        {
            var gridSize = TileCanvas.GridSize;
            var centre = 0;

            for (int y = centre; y < TileCanvas.TileImageSize; y += gridSize)
            {
                for (int x = centre; x < TileCanvas.TileImageSize; x += gridSize)
                {
                    g.FillRectangle(new Rect(x, y, 2, 2), c);
                }
            }
        }

        /// <summary>
        /// Position this tile (in viewport coordinates)
        /// </summary>
        public void MoveTo(float x, float y)
        {
            if ((int)_x == (int)x && (int)_y == (int)y) return;
            _x = x;
            _y = y;
            UiCanvas.QueueAction(canv =>
            {
                canv.RenderTransform = new TranslateTransform
                {
                    X = (int)_x,
                    Y = (int)_y
                };
                canv.Invalidate();
            });
        }

        /// <summary>
        /// Remove event bindings
        /// </summary>
        public void Detach()
        {
            Win2dCanvasPool.Retire(UiCanvas);
            _detached = true;
        }

        /// <summary>
        /// Change the tile state
        /// </summary>
        public void SetState(TileState state)
        {
            State = state;
            UiCanvas.QueueAction(canv => canv?.Invalidate());
        }

        /// <inheritdoc />
        public void MarkCorrupted()
        {
            State = TileState.Corrupted;
        }

        /// <summary>
        /// Set the raw image data to solid white
        /// </summary>
        public void AllocateEmptyImage()
        {
            RawImageData = new byte[ByteSize];
            for (int i = 0; i < ByteSize; i++) { RawImageData[i] = 255; }
        }

        /// <summary>
        /// Returns true if the image data is empty, or all pixels are *very* close to white. Ignores alpha.
        /// </summary>
        public bool ImageIsBlank()
        {
            if (RawImageData == null || State == TileState.Empty) return true;
            if (State == TileState.Locked) return false;

            
            for (int i = 0; i < ByteSize; i+=4) {
                if (RawImageData[i+0] < 252) return false; // B
                if (RawImageData[i+1] < 252) return false; // G
                if (RawImageData[i+2] < 252) return false; // R
            }
            return true;
        }

        public void Invalidate()
        {
            UiCanvas.QueueAction(canv => canv?.Invalidate());
        }

        /// <summary>
        /// Remove the cache, and set state to empty
        /// </summary>
        public void Deallocate()
        {
            State = TileState.Empty;
            RawImageData = null;
        }

        public void SetSelected(bool isSelected)
        {
            // relies on the background being a different color
            //UiCanvas.Opacity = isSelected ? 0.5 : 1.0;

            IsSelected = isSelected;
            Invalidate();
        }
    }
}