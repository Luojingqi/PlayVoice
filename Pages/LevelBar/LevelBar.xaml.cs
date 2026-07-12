using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PlayVoice.Pages.LevelBar
{
    public partial class LevelBar : UserControl
    {
        public static readonly DependencyProperty LedCountProperty = DependencyProperty.Register("LedCount", typeof(int), typeof(LevelBar), new FrameworkPropertyMetadata(25, FrameworkPropertyMetadataOptions.AffectsRender, OnLayoutPropertyChanged));
        public static readonly DependencyProperty LedWidthProperty = DependencyProperty.Register("LedWidth", typeof(double), typeof(LevelBar), new FrameworkPropertyMetadata(4.0, FrameworkPropertyMetadataOptions.AffectsRender, OnLayoutPropertyChanged));
        public static readonly DependencyProperty LedSpacingProperty = DependencyProperty.Register("LedSpacing", typeof(double), typeof(LevelBar), new FrameworkPropertyMetadata(2.0, FrameworkPropertyMetadataOptions.AffectsRender, OnLayoutPropertyChanged));
        public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register("CornerRadius", typeof(double), typeof(LevelBar), new FrameworkPropertyMetadata(1.5, FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty GlowRadiusProperty = DependencyProperty.Register("GlowRadius", typeof(double), typeof(LevelBar), new FrameworkPropertyMetadata(4.0, FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty DecaySpeedProperty = DependencyProperty.Register("DecaySpeed", typeof(float), typeof(LevelBar), new FrameworkPropertyMetadata(0.03f));
        public static readonly DependencyProperty PeakHoldFramesProperty = DependencyProperty.Register("PeakHoldFrames", typeof(int), typeof(LevelBar), new FrameworkPropertyMetadata(30));
        public static readonly DependencyProperty PeakFalloffRateProperty = DependencyProperty.Register("PeakFalloffRate", typeof(float), typeof(LevelBar), new FrameworkPropertyMetadata(0.008f));
        public static readonly DependencyProperty MinColorProperty = DependencyProperty.Register("MinColor", typeof(Color), typeof(LevelBar), new FrameworkPropertyMetadata(Color.FromRgb(0, 255, 0), FrameworkPropertyMetadataOptions.AffectsRender, OnLayoutPropertyChanged));
        public static readonly DependencyProperty MidColorProperty = DependencyProperty.Register("MidColor", typeof(Color), typeof(LevelBar), new FrameworkPropertyMetadata(Color.FromRgb(255, 234, 0), FrameworkPropertyMetadataOptions.AffectsRender, OnLayoutPropertyChanged));
        public static readonly DependencyProperty MaxColorProperty = DependencyProperty.Register("MaxColor", typeof(Color), typeof(LevelBar), new FrameworkPropertyMetadata(Color.FromRgb(255, 0, 51), FrameworkPropertyMetadataOptions.AffectsRender, OnLayoutPropertyChanged));

        public int LedCount { get => (int)GetValue(LedCountProperty); set => SetValue(LedCountProperty, value); }
        public double LedWidth { get => (double)GetValue(LedWidthProperty); set => SetValue(LedWidthProperty, value); }
        public double LedSpacing { get => (double)GetValue(LedSpacingProperty); set => SetValue(LedSpacingProperty, value); }
        public double CornerRadius { get => (double)GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }
        public double GlowRadius { get => (double)GetValue(GlowRadiusProperty); set => SetValue(GlowRadiusProperty, value); }
        public float DecaySpeed { get => (float)GetValue(DecaySpeedProperty); set => SetValue(DecaySpeedProperty, value); }
        public int PeakHoldFrames { get => (int)GetValue(PeakHoldFramesProperty); set => SetValue(PeakHoldFramesProperty, value); }
        public float PeakFalloffRate { get => (float)GetValue(PeakFalloffRateProperty); set => SetValue(PeakFalloffRateProperty, value); }
        public Color MinColor { get => (Color)GetValue(MinColorProperty); set => SetValue(MinColorProperty, value); }
        public Color MidColor { get => (Color)GetValue(MidColorProperty); set => SetValue(MidColorProperty, value); }
        public Color MaxColor { get => (Color)GetValue(MaxColorProperty); set => SetValue(MaxColorProperty, value); }

        private float _targetValue = 0f;
        private float[] _ledBrightness = Array.Empty<float>();
        private float _peakValue = 0f;
        private int _peakHoldTimer = 0;
        private bool _isActive = false;

        private Color[] _baseLedColors = Array.Empty<Color>();

        private static readonly Dictionary<uint, SolidColorBrush> BrushCache = new Dictionary<uint, SolidColorBrush>();

        private SolidColorBrush _peakBrush;
        private SolidColorBrush _peakGlowBrush;
        private bool _layoutDirty = true;

        private TimeSpan _lastRenderTime = TimeSpan.Zero;
        private const double TargetFpsMs = 1000.0 / 60.0; // 锁定 60 FPS

        public LevelBar()
        {
            InitializeComponent();

            _peakBrush = new SolidColorBrush(Colors.White);
            _peakBrush.Freeze();
            _peakGlowBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255));
            _peakGlowBrush.Freeze();

            CompositionTarget.Rendering += OnRendering;
        }

        private async Task a()
        {
            Random rand = new Random();
            while (true)
            {
                SetLevel(rand.NextSingle());
                await Task.Delay(100);
            }
        }

        private void RebuildLayoutCache()
        {
            int count = Math.Max(1, LedCount);

            if (_ledBrightness.Length != count)
                _ledBrightness = new float[count];

            if (_baseLedColors.Length != count)
                _baseLedColors = new Color[count];

            Color min = MinColor, mid = MidColor, max = MaxColor;

            for (int i = 0; i < count; i++)
            {
                double t = (double)i / (count - 1);
                if (t < 0.5)
                    _baseLedColors[i] = LerpColor(min, mid, t * 2);
                else
                    _baseLedColors[i] = LerpColor(mid, max, (t - 0.5) * 2);
            }

            _layoutDirty = false;
        }

        private static void OnLayoutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var bar = (LevelBar)d;
            bar._layoutDirty = true;
            bar.InvalidateVisual();
        }

        public void SetLevel(float value)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => SetLevel(value)));
                return;
            }
            _targetValue = Math.Clamp(value, 0f, 1f);
            if (_targetValue > 0f) _isActive = true;
        }


        private void OnRendering(object sender, EventArgs e)
        {
            if (!_isActive) return;

            // 将逻辑和渲染限制在最多 60帧/秒
            var args = (RenderingEventArgs)e;
            if (_lastRenderTime == args.RenderingTime) return;

            double elapsedMs = (args.RenderingTime - _lastRenderTime).TotalMilliseconds;
            if (elapsedMs < TargetFpsMs) return;

            _lastRenderTime = args.RenderingTime;

            if (_layoutDirty) RebuildLayoutCache();

            bool stateChanged = false;
            int ledCount = LedCount;

            if (_targetValue >= _peakValue)
            {
                _peakValue = _targetValue;
                _peakHoldTimer = PeakHoldFrames;
            }
            else
            {
                if (_peakHoldTimer > 0) _peakHoldTimer--;
                else _peakValue = Math.Max(0, _peakValue - PeakFalloffRate);
                stateChanged = true;
            }

            float exactIndex = _targetValue * ledCount;
            int fullOnCount = (int)exactIndex;
            float fractional = exactIndex - fullOnCount;

            for (int i = 0; i < ledCount; i++)
            {
                float targetBrightness = (i < fullOnCount) ? 1f : ((i == fullOnCount && fullOnCount < ledCount) ? fractional : 0f);
                float current = _ledBrightness[i];

                if (targetBrightness >= current)
                {
                    if (Math.Abs(current - targetBrightness) > 0.001f)
                    {
                        _ledBrightness[i] = targetBrightness;
                        stateChanged = true;
                    }
                }
                else
                {
                    float newVal = Math.Max(targetBrightness, current - DecaySpeed);
                    if (Math.Abs(_ledBrightness[i] - newVal) > 0.001f)
                    {
                        _ledBrightness[i] = newVal;
                        stateChanged = true;
                    }
                }
            }

            if (stateChanged)
            {
                InvalidateVisual();
            }

            // 休眠判定
            if (_targetValue <= 0f && _peakValue <= 0f && _ledBrightness.All(b => b <= 0.001f))
            {
                _isActive = false;
                Array.Clear(_ledBrightness, 0, _ledBrightness.Length);
                _peakValue = 0f;
                _peakHoldTimer = 0;
                InvalidateVisual();
            }
        }

        private SolidColorBrush GetCachedBrush(Color baseColor, float brightness, byte alpha = 255)
        {
            brightness = (float)Math.Round(brightness * 64f) / 64f;

            byte r = (byte)(baseColor.R * brightness);
            byte g = (byte)(baseColor.G * brightness);
            byte b = (byte)(baseColor.B * brightness);

            uint key = (uint)((alpha << 24) | (r << 16) | (g << 8) | b);

            if (BrushCache.TryGetValue(key, out var brush))
                return brush;

            var newBrush = new SolidColorBrush(Color.FromArgb(alpha, r, g, b));
            newBrush.Freeze();
            BrushCache[key] = newBrush;

            return newBrush;
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double w = MainGrid.ActualWidth;
            double h = MainGrid.ActualHeight;
            if (w <= 0 || h <= 0) return;

            int ledCount = LedCount;
            if (ledCount <= 0 || _baseLedColors.Length != ledCount) return;

            double ledWidth = LedWidth;
            double spacing = LedSpacing;
            double step = ledWidth + spacing;
            double corner = CornerRadius;
            double glow = GlowRadius;
            double ledHeight = Math.Max(0, h - 4);
            double y = 2;

            for (int i = 0; i < ledCount; i++)
            {
                float brightness = _ledBrightness[i];
                if (brightness <= 0.001f) continue;

                double x = i * step;
                Color baseColor = _baseLedColors[i];

                Brush ledBrush = GetCachedBrush(baseColor, brightness, 255);
                Brush glowBrush = GetCachedBrush(baseColor, brightness, 40);

                Rect ledRect = new Rect(x, y, ledWidth, ledHeight);
                Rect glowRect = new Rect(x - glow, y - glow, ledWidth + glow * 2, ledHeight + glow * 2);

                dc.DrawRectangle(glowBrush, null, glowRect);

                dc.DrawRoundedRectangle(ledBrush, null, ledRect, corner, corner);
            }

            if (_peakValue > 0)
            {
                double peakX = _peakValue * w;
                if (peakX > w - 2) peakX = w - 2;

                Rect peakGlowRect = new Rect(peakX - 3, 0, 8, h);
                dc.DrawRectangle(_peakGlowBrush, null, peakGlowRect);

                Rect peakRect = new Rect(peakX, 0, 2, h);
                dc.DrawRectangle(_peakBrush, null, peakRect);
            }
        }

        private static Color LerpColor(Color c1, Color c2, double t)
        {
            t = Math.Clamp(t, 0, 1);
            return Color.FromRgb(
                (byte)(c1.R + (c2.R - c1.R) * t),
                (byte)(c1.G + (c2.G - c1.G) * t),
                (byte)(c1.B + (c2.B - c1.B) * t));
        }
    }
}