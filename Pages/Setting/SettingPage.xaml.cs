using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace PlayVoice.Pages.Setting;

public partial class SettingPage : Page
{
    private const double NarrowThreshold = 600;
    private bool _isMerged = false;
    private List<UIElement> _cachedRightElements = new List<UIElement>();

    // 用于防止 SizeChanged 递归重入
    private bool _suppressSizeChanged = false;

    public SettingPage()
    {
        InitializeComponent();
        this.SizeChanged += OnPageSizeChanged;
    }

    private void OnPageSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_suppressSizeChanged) return;   // 正在执行合并/拆分，忽略中间触发的 SizeChanged

        UpdateLayoutState(e.NewSize.Width);
    }

    private void UpdateLayoutState(double width)
    {
        bool shouldMerge = width < NarrowThreshold;
        if (shouldMerge == _isMerged) return;

        _suppressSizeChanged = true;   // 锁定，避免移动控件时触发布局更新再次进入

        if (shouldMerge)
        {
            MergePanels();
        }
        else
        {
            SplitPanels();
        }

        _isMerged = shouldMerge;
        _suppressSizeChanged = false;
    }

    private void MergePanels()
    {
        // 缓存右侧所有子控件，然后清空右侧
        _cachedRightElements.Clear();
        foreach (UIElement element in ContentRightPanel.Children)
        {
            _cachedRightElements.Add(element);
        }
        ContentRightPanel.Children.Clear();

        // 将缓存的控件按原顺序添加到左侧面板底部
        foreach (UIElement element in _cachedRightElements)
        {
            ContentLeftPanel.Children.Add(element);
        }

        // 调整间距：左侧面板此时为“全内容”，右侧面板可隐藏或自动缩小
        // 由于右侧面板无内容，会自动塌陷，但最好显式将右侧列宽度设为0，左侧占满
        ContentGrid.ColumnDefinitions[1].Width = new GridLength(0);
        ContentGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
    }

    private void SplitPanels()
    {
        // 从左侧面板中移除之前移入的控件
        foreach (UIElement element in _cachedRightElements)
        {
            ContentLeftPanel.Children.Remove(element);
        }

        // 将这些控件加回右侧面板
        foreach (UIElement element in _cachedRightElements)
        {
            ContentRightPanel.Children.Add(element);
        }

        // 恢复列宽为左右各半
        ContentGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
        ContentGrid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
    }
}