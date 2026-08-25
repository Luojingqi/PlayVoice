using Steamworks.Ugc;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace PlayVoice.Pages.Workshop;

/// <summary>
/// 创意工坊公共浏览页专用的横向卡片容器。
/// 负责固定宽度布局、鼠标拖动、滚轮动画、卡片吸附、边界限制和显式底部滚动条。
/// </summary>
public partial class WorkshopHorizontalView : UserControl
{
    private const double CardWidth = WorkshopRemoteItemCard.CollapsedWidth;
    private const double CardHorizontalMargin = 6;
    private const double CardStep = CardWidth + CardHorizontalMargin;
    private const double BoundaryTolerance = 0.5;

    private readonly ObservableCollection<Item> _items = new();
    private ScrollViewer? _scrollViewer;
    private bool _isAnimating;
    private double _targetOffset;
    private int _animationGeneration;
    private WorkshopRemoteItemCard? _expandedCard;
    private int _expandedCardVisibilityGeneration;
    private bool _isPointerDragPending;
    private bool _isPointerDragging;
    private Point _pointerDragStart;
    private double _pointerDragStartOffset;

    public WorkshopHorizontalView()
    {
        InitializeComponent();
        ItemListBox.ItemsSource = _items;
        AddHandler(
            WorkshopRemoteItemCard.ExpansionStateChangedEvent,
            new RoutedEventHandler(WorkshopRemoteItemCard_ExpansionStateChanged));
        Loaded += WorkshopHorizontalView_Loaded;
        Unloaded += WorkshopHorizontalView_Unloaded;
        SizeChanged += WorkshopHorizontalView_SizeChanged;
    }

    public void SetItems(IEnumerable<Item> items)
    {
        EndPointerDrag(snapToCard: false);
        CollapseExpandedCard(animate: false);
        CancelOffsetAnimation();
        _items.Clear();
        foreach (Item item in items)
            _items.Add(item);

        Dispatcher.InvokeAsync(
            ResetToBeginning,
            DispatcherPriority.Loaded);
    }

    public void Clear()
    {
        EndPointerDrag(snapToCard: false);
        CollapseExpandedCard(animate: false);
        CancelOffsetAnimation();
        _items.Clear();
        ResetToBeginning();
    }

    private void WorkshopHorizontalView_Loaded(object sender, RoutedEventArgs e)
    {
        Dispatcher.InvokeAsync(
            AttachScrollViewer,
            DispatcherPriority.Loaded);
    }

    private void WorkshopHorizontalView_Unloaded(object sender, RoutedEventArgs e)
    {
        EndPointerDrag(snapToCard: false);
        CancelOffsetAnimation();
        DetachScrollViewer();
    }

    private void AttachScrollViewer()
    {
        ScrollViewer? scrollViewer = FindVisualChild<ScrollViewer>(ItemListBox);
        if (ReferenceEquals(_scrollViewer, scrollViewer))
        {
            UpdateScrollMetrics();
            return;
        }

        DetachScrollViewer();
        _scrollViewer = scrollViewer;
        if (_scrollViewer == null) return;

        _scrollViewer.ScrollChanged += ScrollViewer_ScrollChanged;
        UpdateScrollMetrics();
    }

    private void DetachScrollViewer()
    {
        if (_scrollViewer != null)
            _scrollViewer.ScrollChanged -= ScrollViewer_ScrollChanged;
        _scrollViewer = null;
    }

    private void ResetToBeginning()
    {
        if (_scrollViewer == null)
            AttachScrollViewer();
        if (_scrollViewer == null) return;

        ScrollToExact(0);
        UpdateScrollMetrics();
    }

    private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        // 用户发起的滚轮、拖拽和滚动条操作会在各自的入口收起卡片。
        // 此处也会收到展开卡片自动跟随产生的滚动，不能统一按位移收起。
        if (!_isAnimating)
            _targetOffset = _scrollViewer.HorizontalOffset;
        UpdateScrollMetrics();
    }

    private void UpdateScrollMetrics()
    {
        if (_scrollViewer == null)
        {
            HorizontalScrollBar.Visibility = Visibility.Collapsed;
            return;
        }

        double maximum = Math.Max(0, _scrollViewer.ScrollableWidth);
        HorizontalScrollBar.Minimum = 0;
        HorizontalScrollBar.Maximum = maximum;
        HorizontalScrollBar.ViewportSize = Math.Max(0, _scrollViewer.ViewportWidth);
        HorizontalScrollBar.LargeChange = Math.Max(CardStep, _scrollViewer.ViewportWidth);
        HorizontalScrollBar.SmallChange = CardStep;
        HorizontalScrollBar.Value = Math.Clamp(
            _scrollViewer.HorizontalOffset,
            0,
            maximum);
        HorizontalScrollBar.Visibility = maximum > BoundaryTolerance
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void ItemListBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // 可纵向滚动的描述和音频列表拥有滚轮；即使它们已经到达边界，
        // 也不能把滚轮传给横向容器。不需要滚动的内部区域则由这里接管。
        if (OriginatesInScrollableNestedViewer(e.OriginalSource as DependencyObject))
            return;

        HandleHorizontalMouseWheel(e);
    }

    private void ItemListBox_PreviewMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (_scrollViewer == null || GetMaximumOffset() <= BoundaryTolerance)
            return;

        CancelOffsetAnimation();
        _pointerDragStart = e.GetPosition(ItemListBox);
        _pointerDragStartOffset = _scrollViewer.HorizontalOffset;
        _isPointerDragPending = true;
        _isPointerDragging = false;
    }

    private void ItemListBox_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isPointerDragPending || _scrollViewer == null)
            return;

        if (e.LeftButton != MouseButtonState.Pressed)
        {
            EndPointerDrag(snapToCard: false);
            return;
        }

        Point currentPosition = e.GetPosition(ItemListBox);
        Vector displacement = currentPosition - _pointerDragStart;
        if (!_isPointerDragging)
        {
            bool passedHorizontalThreshold =
                Math.Abs(displacement.X) >= SystemParameters.MinimumHorizontalDragDistance;
            bool passedVerticalThreshold =
                Math.Abs(displacement.Y) >= SystemParameters.MinimumVerticalDragDistance;
            if (!passedHorizontalThreshold && !passedVerticalThreshold)
                return;

            // 明显的纵向手势留给卡片内部的描述或音频列表。
            if (Math.Abs(displacement.Y) >= Math.Abs(displacement.X))
            {
                EndPointerDrag(snapToCard: false);
                return;
            }

            CollapseExpandedCard();
            if (!Mouse.Capture(ItemListBox, CaptureMode.Element))
            {
                EndPointerDrag(snapToCard: false);
                return;
            }

            _isPointerDragging = true;
            ItemListBox.Cursor = Cursors.ScrollWE;
        }

        double requestedOffset = _pointerDragStartOffset - displacement.X;
        ScrollDuringPointerDrag(requestedOffset);
        e.Handled = true;
    }

    private void ItemListBox_PreviewMouseLeftButtonUp(
        object sender,
        MouseButtonEventArgs e)
    {
        if (!_isPointerDragPending)
            return;

        bool handled = _isPointerDragging;
        EndPointerDrag(snapToCard: handled);
        if (handled)
            e.Handled = true;
    }

    private void ItemListBox_LostMouseCapture(object sender, MouseEventArgs e)
    {
        if (_isPointerDragging)
            EndPointerDrag(snapToCard: true);
    }

    private void ScrollDuringPointerDrag(double requestedOffset)
    {
        if (_scrollViewer == null) return;

        double offset = Math.Clamp(requestedOffset, 0, GetMaximumOffset());
        _targetOffset = offset;
        SetCurrentValue(AnimatedHorizontalOffsetProperty, offset);
        _scrollViewer.ScrollToHorizontalOffset(offset);
        UpdateScrollMetrics();
    }

    private void EndPointerDrag(bool snapToCard)
    {
        bool wasDragging = _isPointerDragging;
        _isPointerDragPending = false;
        _isPointerDragging = false;
        ItemListBox.ClearValue(CursorProperty);

        if (ReferenceEquals(Mouse.Captured, ItemListBox))
            Mouse.Capture(null);

        if (snapToCard && wasDragging && _scrollViewer != null)
            AnimateToOffset(SnapToNearestCard(_scrollViewer.HorizontalOffset));
    }

    private void HorizontalScrollBar_PreviewMouseWheel(object sender, MouseWheelEventArgs e) =>
        HandleHorizontalMouseWheel(e);

    private void HandleHorizontalMouseWheel(MouseWheelEventArgs e)
    {
        e.Handled = true;
        if (_scrollViewer == null || e.Delta == 0) return;

        CollapseExpandedCard();
        ScrollOneCard(e.Delta < 0 ? 1 : -1);
    }

    private bool OriginatesInScrollableNestedViewer(DependencyObject? source)
    {
        DependencyObject? current = source;
        while (current != null && !ReferenceEquals(current, ItemListBox))
        {
            if (current is ScrollViewer viewer &&
                !ReferenceEquals(viewer, _scrollViewer) &&
                viewer.ScrollableHeight > BoundaryTolerance)
                return true;

            current = GetParent(current);
        }

        return false;
    }

    private void ScrollOneCard(int direction)
    {
        double maximum = GetMaximumOffset();
        double basis = _isAnimating
            ? _targetOffset
            : _scrollViewer.HorizontalOffset;

        if (direction > 0)
        {
            if (basis >= maximum - BoundaryTolerance) return;

            double nextStep = (Math.Floor((basis + BoundaryTolerance) / CardStep) + 1) * CardStep;
            AnimateToOffset(Math.Min(nextStep, maximum));
            return;
        }

        if (basis <= BoundaryTolerance) return;

        double previousStep = basis >= maximum - BoundaryTolerance
            ? Math.Floor((maximum - BoundaryTolerance) / CardStep) * CardStep
            : Math.Floor((basis - BoundaryTolerance) / CardStep) * CardStep;
        AnimateToOffset(Math.Max(0, previousStep));
    }

    private void HorizontalScrollBar_Scroll(object sender, ScrollEventArgs e)
    {
        if (_scrollViewer == null) return;

        e.Handled = true;
        CollapseExpandedCard();
        switch (e.ScrollEventType)
        {
            case ScrollEventType.ThumbTrack:
                ScrollToExact(e.NewValue);
                break;
            case ScrollEventType.EndScroll:
            case ScrollEventType.ThumbPosition:
                AnimateToOffset(SnapToNearestCard(e.NewValue));
                break;
            case ScrollEventType.SmallIncrement:
                ScrollOneCard(1);
                break;
            case ScrollEventType.SmallDecrement:
                ScrollOneCard(-1);
                break;
            case ScrollEventType.LargeIncrement:
                AnimateToOffset(SnapToNearestCard(
                    _scrollViewer.HorizontalOffset + _scrollViewer.ViewportWidth));
                break;
            case ScrollEventType.LargeDecrement:
                AnimateToOffset(SnapToNearestCard(
                    _scrollViewer.HorizontalOffset - _scrollViewer.ViewportWidth));
                break;
            case ScrollEventType.First:
                AnimateToOffset(0);
                break;
            case ScrollEventType.Last:
                AnimateToOffset(GetMaximumOffset());
                break;
        }
    }

    private double SnapToNearestCard(double offset)
    {
        double maximum = GetMaximumOffset();
        double clampedOffset = Math.Clamp(offset, 0, maximum);
        if (maximum - clampedOffset <= CardStep / 2)
            return maximum;

        return Math.Clamp(
            Math.Round(clampedOffset / CardStep, MidpointRounding.AwayFromZero) * CardStep,
            0,
            maximum);
    }

    private void AnimateToOffset(double requestedOffset)
    {
        if (_scrollViewer == null) return;

        double targetOffset = Math.Clamp(requestedOffset, 0, GetMaximumOffset());
        double currentOffset = _scrollViewer.HorizontalOffset;
        if (Math.Abs(targetOffset - currentOffset) <= BoundaryTolerance)
        {
            ScrollToExact(targetOffset);
            return;
        }

        CancelOffsetAnimation();
        _isAnimating = true;
        _targetOffset = targetOffset;
        int generation = ++_animationGeneration;

        SetCurrentValue(AnimatedHorizontalOffsetProperty, currentOffset);
        var animation = new DoubleAnimation
        {
            From = currentOffset,
            To = targetOffset,
            Duration = TimeSpan.FromMilliseconds(230),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.HoldEnd
        };
        animation.Completed += (_, _) =>
        {
            if (generation != _animationGeneration) return;

            _isAnimating = false;
            BeginAnimation(AnimatedHorizontalOffsetProperty, null);
            SetCurrentValue(AnimatedHorizontalOffsetProperty, targetOffset);
            _scrollViewer?.ScrollToHorizontalOffset(targetOffset);
            UpdateScrollMetrics();
        };
        BeginAnimation(
            AnimatedHorizontalOffsetProperty,
            animation,
            HandoffBehavior.SnapshotAndReplace);
    }

    private void ScrollToExact(double requestedOffset)
    {
        if (_scrollViewer == null) return;

        CancelOffsetAnimation();
        double offset = Math.Clamp(requestedOffset, 0, GetMaximumOffset());
        _targetOffset = offset;
        SetCurrentValue(AnimatedHorizontalOffsetProperty, offset);
        _scrollViewer.ScrollToHorizontalOffset(offset);
        UpdateScrollMetrics();
    }

    private void CancelOffsetAnimation()
    {
        _animationGeneration++;
        _isAnimating = false;
        BeginAnimation(AnimatedHorizontalOffsetProperty, null);
        if (_scrollViewer != null)
        {
            double currentOffset = _scrollViewer.HorizontalOffset;
            SetCurrentValue(AnimatedHorizontalOffsetProperty, currentOffset);
            _targetOffset = currentOffset;
        }
    }

    private double GetMaximumOffset() =>
        Math.Max(0, _scrollViewer?.ScrollableWidth ?? 0);

    private void WorkshopRemoteItemCard_ExpansionStateChanged(
        object sender,
        RoutedEventArgs e)
    {
        if (e.OriginalSource is not WorkshopRemoteItemCard card) return;

        if (card.IsExpanded)
        {
            WorkshopRemoteItemCard? previousCard = _expandedCard;
            StopFollowingExpandedCard();
            _expandedCard = card;
            _expandedCard.SizeChanged += ExpandedCard_SizeChanged;
            _expandedCardVisibilityGeneration++;
            QueueEnsureExpandedCardVisible(card);
            if (previousCard != null && !ReferenceEquals(previousCard, card))
                previousCard.Collapse();
        }
        else if (ReferenceEquals(_expandedCard, card))
        {
            StopFollowingExpandedCard();
        }

        e.Handled = true;
    }

    private void CollapseExpandedCard(bool animate = true)
    {
        WorkshopRemoteItemCard? card = _expandedCard;
        StopFollowingExpandedCard();
        card?.Collapse(animate);
    }

    private void StopFollowingExpandedCard()
    {
        if (_expandedCard != null)
            _expandedCard.SizeChanged -= ExpandedCard_SizeChanged;

        _expandedCard = null;
        _expandedCardVisibilityGeneration++;
    }

    private void ExpandedCard_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is WorkshopRemoteItemCard card)
            QueueEnsureExpandedCardVisible(card);
    }

    private void QueueEnsureExpandedCardVisible(WorkshopRemoteItemCard card)
    {
        int generation = _expandedCardVisibilityGeneration;
        Dispatcher.InvokeAsync(() =>
        {
            if (generation != _expandedCardVisibilityGeneration ||
                !ReferenceEquals(_expandedCard, card) ||
                !card.IsExpanded)
                return;

            EnsureExpandedCardVisible(card);
        }, DispatcherPriority.Render);
    }

    private void EnsureExpandedCardVisible(WorkshopRemoteItemCard card)
    {
        if (_scrollViewer == null || !card.IsLoaded) return;

        Rect cardBounds;
        try
        {
            cardBounds = card.TransformToAncestor(_scrollViewer).TransformBounds(
                new Rect(new Point(), card.RenderSize));
        }
        catch (InvalidOperationException)
        {
            return;
        }

        double viewportWidth = _scrollViewer.ViewportWidth;
        if (viewportWidth <= BoundaryTolerance) return;

        double offsetChange = 0;
        if (cardBounds.Right > viewportWidth + BoundaryTolerance)
            offsetChange = cardBounds.Right - viewportWidth;
        else if (cardBounds.Left < -BoundaryTolerance)
            offsetChange = cardBounds.Left;

        if (Math.Abs(offsetChange) <= BoundaryTolerance) return;

        ScrollToExact(_scrollViewer.HorizontalOffset + offsetChange);
    }

    private void WorkshopHorizontalView_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_scrollViewer == null) return;

        double previousMaximum = HorizontalScrollBar.Maximum;
        bool wasAtEnd = previousMaximum > BoundaryTolerance &&
                        _scrollViewer.HorizontalOffset >= previousMaximum - BoundaryTolerance;
        double currentOffset = _scrollViewer.HorizontalOffset;

        Dispatcher.InvokeAsync(() =>
        {
            if (_scrollViewer == null) return;

            double targetOffset = wasAtEnd
                ? GetMaximumOffset()
                : Math.Min(currentOffset, GetMaximumOffset());
            ScrollToExact(targetOffset);
            if (_expandedCard != null)
                QueueEnsureExpandedCardVisible(_expandedCard);
        }, DispatcherPriority.Loaded);
    }

    public static readonly DependencyProperty AnimatedHorizontalOffsetProperty =
        DependencyProperty.Register(
            nameof(AnimatedHorizontalOffset),
            typeof(double),
            typeof(WorkshopHorizontalView),
            new PropertyMetadata(0d, AnimatedHorizontalOffsetChanged));

    public double AnimatedHorizontalOffset
    {
        get => (double)GetValue(AnimatedHorizontalOffsetProperty);
        set => SetValue(AnimatedHorizontalOffsetProperty, value);
    }

    private static void AnimatedHorizontalOffsetChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs eventArgs)
    {
        var view = (WorkshopHorizontalView)dependencyObject;
        view._scrollViewer?.ScrollToHorizontalOffset((double)eventArgs.NewValue);
    }

    private static T? FindVisualChild<T>(DependencyObject? parent)
        where T : DependencyObject
    {
        if (parent == null) return null;

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            if (child is T result)
                return result;

            T? descendant = FindVisualChild<T>(child);
            if (descendant != null)
                return descendant;
        }

        return null;
    }

    private static DependencyObject? GetParent(DependencyObject child)
    {
        if (child is Visual || child is System.Windows.Media.Media3D.Visual3D)
            return VisualTreeHelper.GetParent(child);

        return LogicalTreeHelper.GetParent(child);
    }
}
