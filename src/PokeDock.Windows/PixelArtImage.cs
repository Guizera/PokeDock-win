using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;

namespace PokeDock.Windows;

public sealed class PixelArtImage : Grid
{
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source),
        typeof(string),
        typeof(PixelArtImage),
        new PropertyMetadata(null, SourceChanged));

    private LoadedImageSurface? _surface;
    private CompositionSurfaceBrush? _brush;
    private SpriteVisual? _sprite;

    public PixelArtImage()
    {
        IsHitTestVisible = false;
        Loaded += PixelArtImage_Loaded;
        Unloaded += PixelArtImage_Unloaded;
        SizeChanged += PixelArtImage_SizeChanged;
    }

    public string? Source
    {
        get => (string?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    private static void SourceChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is PixelArtImage image && image.IsLoaded) image.LoadSurface();
    }

    private void PixelArtImage_Loaded(object sender, RoutedEventArgs e) => LoadSurface();

    private void PixelArtImage_Unloaded(object sender, RoutedEventArgs e)
    {
        _brush?.Dispose();
        _surface?.Dispose();
        _brush = null;
        _surface = null;
        _sprite = null;
        ElementCompositionPreview.SetElementChildVisual(this, null!);
    }

    private void PixelArtImage_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_sprite is not null)
        {
            _sprite.Size = new Vector2((float)e.NewSize.Width, (float)e.NewSize.Height);
        }
    }

    private void LoadSurface()
    {
        _brush?.Dispose();
        _surface?.Dispose();
        _brush = null;
        _surface = null;

        if (string.IsNullOrWhiteSpace(Source))
        {
            ElementCompositionPreview.SetElementChildVisual(this, null!);
            _sprite = null;
            return;
        }

        _surface = LoadedImageSurface.StartLoadFromUri(new Uri(Source));
        var compositor = ElementCompositionPreview.GetElementVisual(this).Compositor;
        _brush = compositor.CreateSurfaceBrush(_surface);
        _brush.Stretch = CompositionStretch.Uniform;
        _brush.BitmapInterpolationMode = CompositionBitmapInterpolationMode.NearestNeighbor;
        _sprite = compositor.CreateSpriteVisual();
        _sprite.Brush = _brush;
        _sprite.Size = new Vector2((float)ActualWidth, (float)ActualHeight);
        ElementCompositionPreview.SetElementChildVisual(this, _sprite);
    }
}
