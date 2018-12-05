﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using LibDmd.Output.Virtual;
using NLog;
using SkiaSharp;

namespace LibDmd.Common
{
	/// <summary>
	/// Interaction logic for AlphaNumericSettings.xaml
	/// </summary>
	public partial class VirtualAlphaNumericSettings : Window
	{

		private static readonly AlphaNumericResources _res = AlphaNumericResources.GetInstance();
		protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private readonly DisplaySetting DisplaySetting;
		private WriteableBitmap _writeableBitmap;

		public VirtualAlphaNumericSettings(AlphanumericControl control, double top, double left)
		{
			Top = top;
			Left = left;
			InitializeComponent();
			Title = "[" + control.DisplaySetting.Display + "] " + Title;

			DisplaySetting = new DisplaySetting(
				control.DisplaySetting.Display + 100, 
				control.DisplaySetting.SegmentType, 
				control.DisplaySetting.Style.Copy(), 
				1, 
				1, 
				(int)Preview.Width, 
				(int)Preview.Height
			);
			_writeableBitmap = new WriteableBitmap((int)Preview.Width, (int)Preview.Height, 96, 96, PixelFormats.Bgra32, BitmapPalettes.Halftone256Transparent);
			Preview.Source = _writeableBitmap;

			CompositionTarget.Rendering += (o, e) => DrawPreview(_writeableBitmap);

			// name each layer
			ForegroundStyle.Label = "Foreground Layer";
			InnerGlowStyle.Label = "Inner Glow Layer";
			OuterGlowStyle.Label = "Outer Glow Layer";
			BackgroundStyle.Label = "Unlit Layer";

			// save our editable copy of the control's style
			ForegroundStyle.RasterizeStyle = DisplaySetting.Style.Foreground;
			InnerGlowStyle.RasterizeStyle = DisplaySetting.Style.InnerGlow;
			OuterGlowStyle.RasterizeStyle = DisplaySetting.Style.OuterGlow;
			BackgroundStyle.RasterizeStyle = DisplaySetting.Style.Background;
		
			// rasterize preview a first time
			_res.Rasterize(DisplaySetting, true);

			var segments = new[] {0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14};

			// subscribe to control changes that trigger rasterization
			ForegroundStyle.OnLayerChanged.DistinctUntilChanged().Subscribe(layerStyle => {
				DisplaySetting.Style.Foreground = layerStyle;
				_res.RasterizeLayer(DisplaySetting, RasterizeLayer.Foreground, layerStyle, segments, DisplaySetting.Style.SkewAngle);
			});
			InnerGlowStyle.OnLayerChanged.DistinctUntilChanged().Subscribe(layerStyle => {
				DisplaySetting.Style.InnerGlow = layerStyle;
				_res.RasterizeLayer(DisplaySetting, RasterizeLayer.InnerGlow, layerStyle, segments, DisplaySetting.Style.SkewAngle);
			});
			OuterGlowStyle.OnLayerChanged.DistinctUntilChanged().Subscribe(layerStyle => {
				DisplaySetting.Style.OuterGlow = layerStyle;
				_res.RasterizeLayer(DisplaySetting, RasterizeLayer.OuterGlow, layerStyle, segments, DisplaySetting.Style.SkewAngle);
			});
			BackgroundStyle.OnLayerChanged.DistinctUntilChanged().Subscribe(layerStyle => {
				DisplaySetting.Style.Background = layerStyle;
				_res.RasterizeLayer(DisplaySetting, RasterizeLayer.Background, layerStyle, new [] { AlphaNumericResources.FullSegment }, DisplaySetting.Style.SkewAngle);
			});
		}

		private void DrawPreview(WriteableBitmap writeableBitmap)
		{
			var width = (int)writeableBitmap.Width;
			var height = (int)writeableBitmap.Height;

			writeableBitmap.Lock();

			var surfaceInfo = new SKImageInfo {
				Width = width,
				Height = height,
				ColorType = SKColorType.Bgra8888,
				AlphaType = SKAlphaType.Premul,
			};
			using (var surface = SKSurface.Create(surfaceInfo, writeableBitmap.BackBuffer, width * 4)) {
				var canvas = surface.Canvas;
				var pos = new SKPoint(-15, 0);
				canvas.Clear(SKColors.Black);
				if (BackgroundStyle.RasterizeStyle.IsEnabled) {
					DrawFullSegment(canvas, pos);
				}
				if (OuterGlowStyle.RasterizeStyle.IsEnabled) {
					DrawSegment(RasterizeLayer.OuterGlow, canvas, pos);
				}
				if (InnerGlowStyle.RasterizeStyle.IsEnabled) {
					DrawSegment(RasterizeLayer.InnerGlow, canvas, pos);
				}
				if (ForegroundStyle.RasterizeStyle.IsEnabled) {
					DrawSegment(RasterizeLayer.Foreground, canvas, pos);
				}
			}
			writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
			writeableBitmap.Unlock();
		}

		private void DrawSegment(RasterizeLayer layer, SKCanvas canvas, SKPoint canvasPosition)
		{
			const int seg = 16640;
			using (var surfacePaint = new SKPaint()) {
				for (var j = 0; j < _res.SegmentSize[DisplaySetting.SegmentType]; j++) {
					var rasterizedSegment = _res.GetRasterized(DisplaySetting.Display, layer, DisplaySetting.SegmentType, j);
					if (((seg >> j) & 0x1) != 0 && rasterizedSegment != null) {
						canvas.DrawSurface(rasterizedSegment, canvasPosition, surfacePaint);
					}
				}
			}
		}

		private void DrawFullSegment(SKCanvas canvas, SKPoint position)
		{
			var segment = _res.GetRasterized(DisplaySetting.Display, RasterizeLayer.Background, DisplaySetting.SegmentType, AlphaNumericResources.FullSegment);
			if (segment != null) {
				canvas.DrawSurface(segment, position);
			}
		}

		private void Cancel_Click(object sender, RoutedEventArgs e)
		{
			Hide();
		}
	}
}