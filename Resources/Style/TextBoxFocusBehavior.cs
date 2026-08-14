using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace PlayVoice.Resources.Style;

public static class TextBoxFocusBehavior
{
    public static readonly DependencyProperty FocusOnContainerClickProperty =
        DependencyProperty.RegisterAttached(
            "FocusOnContainerClick",
            typeof(bool),
            typeof(TextBoxFocusBehavior),
            new PropertyMetadata(false, OnFocusOnContainerClickChanged));

    public static readonly DependencyProperty IsTextInputContainerProperty =
        DependencyProperty.RegisterAttached(
            "IsTextInputContainer",
            typeof(bool),
            typeof(TextBoxFocusBehavior),
            new PropertyMetadata(false));

    private static readonly DependencyProperty ContainerProperty =
        DependencyProperty.RegisterAttached(
            "Container",
            typeof(ListBox),
            typeof(TextBoxFocusBehavior));

    private static readonly DependencyProperty ContainerClickHandlerProperty =
        DependencyProperty.RegisterAttached(
            "ContainerClickHandler",
            typeof(MouseButtonEventHandler),
            typeof(TextBoxFocusBehavior));

    public static void SetFocusOnContainerClick(DependencyObject element, bool value) =>
        element.SetValue(FocusOnContainerClickProperty, value);

    public static bool GetFocusOnContainerClick(DependencyObject element) =>
        (bool)element.GetValue(FocusOnContainerClickProperty);

    public static bool GetIsTextInputContainer(DependencyObject element) =>
        (bool)element.GetValue(IsTextInputContainerProperty);

    private static void SetIsTextInputContainer(DependencyObject element, bool value) =>
        element.SetValue(IsTextInputContainerProperty, value);

    private static void OnFocusOnContainerClickChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not TextBox textBox)
            return;

        textBox.Loaded -= TextBox_Loaded;
        textBox.Unloaded -= TextBox_Unloaded;
        Detach(textBox);

        if (e.NewValue is true)
        {
            textBox.Loaded += TextBox_Loaded;
            textBox.Unloaded += TextBox_Unloaded;
            if (textBox.IsLoaded)
                Attach(textBox);
        }
    }

    private static void TextBox_Loaded(object sender, RoutedEventArgs e) =>
        Attach((TextBox)sender);

    private static void TextBox_Unloaded(object sender, RoutedEventArgs e) =>
        Detach((TextBox)sender);

    private static void Attach(TextBox textBox)
    {
        if (textBox.GetValue(ContainerProperty) != null)
            return;

        ListBox container = FindAncestor<ListBox>(textBox);
        if (container == null)
            return;

        textBox.SetValue(ContainerProperty, container);
        SetIsTextInputContainer(container, true);

        if (container.Items.Count != 1)
            return;

        MouseButtonEventHandler clickHandler = (sender, e) =>
        {
            if (!textBox.IsEnabled || !textBox.IsVisible)
                return;

            if (e.OriginalSource is DependencyObject originalSource
                && FindAncestor<TextBox>(originalSource) != null)
                return;

            Keyboard.Focus(textBox);
            textBox.Select(textBox.Text.Length, 0);
            e.Handled = true;
        };

        textBox.SetValue(ContainerClickHandlerProperty, clickHandler);
        container.PreviewMouseLeftButtonDown += clickHandler;
    }

    private static void Detach(TextBox textBox)
    {
        var container = textBox.GetValue(ContainerProperty) as ListBox;
        var clickHandler = textBox.GetValue(ContainerClickHandlerProperty)
            as MouseButtonEventHandler;

        if (container != null && clickHandler != null)
            container.PreviewMouseLeftButtonDown -= clickHandler;
        if (container != null)
            container.ClearValue(IsTextInputContainerProperty);

        textBox.ClearValue(ContainerProperty);
        textBox.ClearValue(ContainerClickHandlerProperty);
    }

    private static T FindAncestor<T>(DependencyObject current)
        where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T ancestor)
                return ancestor;

            current = GetParent(current);
        }

        return null;
    }

    private static DependencyObject GetParent(DependencyObject current)
    {
        if (current is Visual || current is Visual3D)
            return VisualTreeHelper.GetParent(current);
        if (current is FrameworkContentElement contentElement)
            return contentElement.Parent;
        return LogicalTreeHelper.GetParent(current);
    }
}
