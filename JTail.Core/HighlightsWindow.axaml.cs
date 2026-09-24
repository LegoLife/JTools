using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace JTail;

public partial class HighlightsWindow : Window
{
    private HighlightRulesStore? _rulesStore;
    private Action? _onRulesChanged;

    public HighlightsWindow()
    {
        InitializeComponent();
    }

    public HighlightsWindow(HighlightRulesStore rulesStore, Action onRulesChanged) : this()
    {
        _rulesStore = rulesStore;
        _onRulesChanged = onRulesChanged;

        RebuildRulesList();
    }

    private void RebuildRulesList()
    {
        RulesList.Children.Clear();

        for (var i = 0; i < _rulesStore!.Rules.Count; i++)
        {
            var rule = _rulesStore.Rules[i];
            var index = i;

            var colorPicker = new ColorPicker { Color = rule.Color };
            colorPicker.ColorChanged += (_, args) =>
            {
                _rulesStore.SetColorAt(index, args.NewColor);
                _onRulesChanged!();
            };

            var wordBox = new TextBox
            {
                Text = rule.Word,
                VerticalAlignment = VerticalAlignment.Center,
                Width = 150
            };
            wordBox.LostFocus += (_, _) =>
            {
                var word = wordBox.Text?.Trim();
                if (string.IsNullOrEmpty(word))
                {
                    wordBox.Text = _rulesStore.Rules[index].Word;
                    return;
                }

                _rulesStore.SetWordAt(index, word);
                _onRulesChanged!();
            };

            var removeButton = new Button { Content = "Remove" };
            removeButton.Click += (_, _) =>
            {
                _rulesStore.RemoveAt(index);
                RebuildRulesList();
                _onRulesChanged!();
            };

            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            row.Children.Add(colorPicker);
            row.Children.Add(wordBox);
            row.Children.Add(removeButton);

            RulesList.Children.Add(row);
        }
    }

    private void OnAddClick(object? sender, RoutedEventArgs e)
    {
        var word = NewWordTextBox.Text?.Trim();
        if (string.IsNullOrEmpty(word))
        {
            return;
        }

        _rulesStore!.Add(word, NewColorPicker.Color);
        NewWordTextBox.Text = string.Empty;
        RebuildRulesList();
        _onRulesChanged!();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
