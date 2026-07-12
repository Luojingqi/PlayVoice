using System.Collections;
using System.Windows;
using System.Windows.Controls;

namespace PlayVoice.Pages.DropDownList;

/// <summary>
/// DropDownList.xaml 的交互逻辑
/// </summary>
public partial class DropDownList : UserControl
{

    public bool IsSyncing { get; set; } = false;
    public DropDownList()
    {
        InitializeComponent();

        comboBox.DropDownOpened += ComboBox_DropDownOpened;
        comboBox.SelectionChanged += ComboBox_SelectionChanged;
    }

    public event Action OnDropDownOpened;

    private void ComboBox_DropDownOpened(object sender, EventArgs e)
    {
        OnDropDownOpened?.Invoke();
    }

    public event Action<object, object> OnSelectionChanged;

    private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsSyncing) OnSelectionChanged?.Invoke(e.RemovedItems.Count > 0 ? e.RemovedItems[0] : null, e.AddedItems.Count > 0 ? e.AddedItems[0] : null);
    }

    public static readonly DependencyProperty HeaderProperty =
        DependencyProperty.Register("Header", typeof(string), typeof(DropDownList), new PropertyMetadata("未命名"));

    public string Header
    {
        get => (string)GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }


    public static readonly DependencyProperty TextBlockFontSizeProperty =
        DependencyProperty.Register("TextBlockFontSize", typeof(int), typeof(DropDownList), new PropertyMetadata(16));
    public int TextBlockFontSize
    {
        get => (int)GetValue(TextBlockFontSizeProperty);
        set => SetValue(TextBlockFontSizeProperty, value);
    }

    public static readonly DependencyProperty ComboBoxFontSizeProperty =
        DependencyProperty.Register("ComboBoxFontSize", typeof(int), typeof(DropDownList), new PropertyMetadata(14));
    public int ComboBoxFontSize
    {
        get => (int)GetValue(ComboBoxFontSizeProperty);
        set => SetValue(ComboBoxFontSizeProperty, value);
    }


    public static readonly DependencyProperty MaxDropDownHeightProperty =
        DependencyProperty.Register("MaxDropDownHeight", typeof(double), typeof(DropDownList), new PropertyMetadata(400.0));

    public double MaxDropDownHeight
    {
        get => (double)GetValue(MaxDropDownHeightProperty);
        set => SetValue(MaxDropDownHeightProperty, value);
    }

    public static readonly DependencyProperty DropDownWidthProperty =
        DependencyProperty.Register("DropDownWidth", typeof(double), typeof(DropDownList), new PropertyMetadata(80.0));

    public double DropDownWidth
    {
        get => (double)GetValue(DropDownWidthProperty);
        set => SetValue(DropDownWidthProperty, value);
    }

    /// <summary>
    /// 下拉列表数据源
    /// </summary>
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register("ItemsSource", typeof(IEnumerable), typeof(DropDownList), new PropertyMetadata(null));

    public IEnumerable ItemsSource
    {
        get => (IEnumerable)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }



    public static readonly DependencyProperty DisplayMemberPathProperty =
        DependencyProperty.Register("DisplayMemberPath", typeof(string), typeof(DropDownList), new PropertyMetadata(null));

    public string DisplayMemberPath
    {
        get => (string)GetValue(DisplayMemberPathProperty);
        set => SetValue(DisplayMemberPathProperty, value);
    }

    public static readonly DependencyProperty SelectedValuePathProperty =
        DependencyProperty.Register("SelectedValuePath", typeof(string), typeof(DropDownList), new PropertyMetadata(null));

    public string SelectedValuePath
    {
        get => (string)GetValue(SelectedValuePathProperty);
        set => SetValue(SelectedValuePathProperty, value);
    }


    /// <summary>
    /// 当前选中项（默认开启双向绑定 BindsTwoWayByDefault）
    /// </summary>
    public static readonly DependencyProperty SelectedItemProperty =
        DependencyProperty.Register("SelectedItem", typeof(object), typeof(DropDownList), new PropertyMetadata(null));

    public object SelectedItem
    {
        get => GetValue(SelectedItemProperty);
    }

    /// <summary>
    /// 当前选中项的索引（默认开启双向绑定 BindsTwoWayByDefault）
    /// </summary>
    public static readonly DependencyProperty SelectedIndexProperty =
        DependencyProperty.Register("SelectedIndex", typeof(int), typeof(DropDownList),
            new FrameworkPropertyMetadata(-1, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public int SelectedIndex
    {
        get => (int)GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    public static readonly DependencyProperty SelectedValueProperty =
        DependencyProperty.Register("SelectedValue", typeof(object), typeof(DropDownList), new PropertyMetadata(null));

    public object SelectedValue
    {
        get => GetValue(SelectedValueProperty);
    }

    public static readonly DependencyProperty ToolTipTextProperty =
        DependencyProperty.Register("ToolTipText", typeof(string), typeof(DropDownList), new PropertyMetadata(null));

    public string ToolTipText
    {
        get => (string)GetValue(ToolTipTextProperty);
        set => SetValue(ToolTipTextProperty, value);
    }


    public static readonly DependencyProperty LabelStatusProperty =
       DependencyProperty.Register("LabelStatus", typeof(LabelStatus), typeof(DropDownList), new PropertyMetadata(LabelStatus.None));

    public LabelStatus LabelStatus
    {
        get => (LabelStatus)GetValue(LabelStatusProperty);
        set => SetValue(LabelStatusProperty, value);
    }
}