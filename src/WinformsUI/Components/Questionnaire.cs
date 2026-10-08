using System.Collections.ObjectModel;
using System.Text.Json;

namespace WinformsUI;

public sealed record QuestionChoice(string Key, string Text, string Description = "");
public sealed record QuestionAnswer(IReadOnlyList<string> Keys, string Text = "", bool Skipped = false);
public sealed record QuestionDefinition(string Key, string Prompt, IReadOnlyList<QuestionChoice>? Choices = null,
    bool Required = true, bool Multiple = false, bool AllowText = false, string Description = "",
    Func<IReadOnlyDictionary<string, QuestionAnswer>, bool>? When = null, Func<QuestionAnswer, string?>? Validate = null);
public sealed class QuestionnaireSubmittedEventArgs(IReadOnlyDictionary<string, QuestionAnswer> answers) : EventArgs
{
    public IReadOnlyDictionary<string, QuestionAnswer> Answers { get; } = answers;
}

/// <summary>Conditional multi-step questions with native choices, text, validation, resume and submission.</summary>
public sealed class Questionnaire : UserControl
{
    private readonly QuestionDefinition[] _questions;
    private readonly Dictionary<string, QuestionAnswer> _answers = new(StringComparer.Ordinal);
    private readonly Label _progress = new() { AutoSize = true };
    private readonly Label _prompt = Ui.Label("");
    private readonly Label _description = Ui.Label("");
    private readonly Label _error = Ui.Label("").WithRole("danger");
    private readonly StackPanel _choices = WinformsUI.Layout.Stack();
    private readonly TextBox _text = new() { PlaceholderText = "回答を入力" };
    private readonly StackPanel _body;
    private string? _currentKey;
    private bool _rendering;
    public Button PreviousButton { get; }
    public Button NextButton { get; }
    public Button SkipButton { get; }
    public Button SubmitButton { get; }
    public string? CurrentKey => _currentKey;
    public IReadOnlyDictionary<string, QuestionAnswer> Answers => new ReadOnlyDictionary<string, QuestionAnswer>(new Dictionary<string, QuestionAnswer>(_answers));
    public event EventHandler<QuestionnaireSubmittedEventArgs>? Submitted;

    public Questionnaire(IEnumerable<QuestionDefinition> questions)
    {
        _questions = questions.Select(question => question with { Choices = Array.AsReadOnly((question.Choices ?? []).ToArray()) }).ToArray();
        if (_questions.Select(question => question.Key).Distinct(StringComparer.Ordinal).Count() != _questions.Length || _questions.Any(question => string.IsNullOrWhiteSpace(question.Key)))
            throw new ArgumentException("質問キーは空でない一意の値にしてください。", nameof(questions));
        if (_questions.Any(question => question.Choices!.Select(choice => choice.Key).Distinct(StringComparer.Ordinal).Count() != question.Choices!.Count))
            throw new ArgumentException("選択肢のキーが重複しています。", nameof(questions));
        PreviousButton = Ui.Button("前へ", () => Previous()); NextButton = Ui.Button("次へ", () => Next());
        SkipButton = Ui.Button("スキップ", () => Skip()); SubmitButton = Ui.Button("送信", () => Submit());
        _body = WinformsUI.Layout.Stack(_progress, _prompt, _description, _choices, _text, _error,
            WinformsUI.Layout.Wrap(PreviousButton, SkipButton, NextButton, SubmitButton));
        _body.Dock = DockStyle.Fill; Controls.Add(_body); Margin = Padding.Empty;
        _text.TextChanged += (_, _) => { if (!_rendering) SaveCurrent(); };
        _currentKey = VisibleQuestions().FirstOrDefault()?.Key; Render();
    }
    private QuestionDefinition[] VisibleQuestions() => _questions.Where(question => question.When?.Invoke(Answers) ?? true).ToArray();
    private QuestionDefinition? Current => _questions.FirstOrDefault(question => question.Key == _currentKey);

    private void Render()
    {
        _rendering = true;
        try
        {
            foreach (Control control in _choices.Controls.Cast<Control>().ToArray()) control.Dispose();
            var visible = VisibleQuestions(); var question = Current;
            int index = Array.FindIndex(visible, item => item.Key == _currentKey);
            _progress.Text = $"質問 {(index < 0 ? 0 : index + 1)} / {visible.Length}";
            _prompt.Text = question?.Prompt ?? "質問はありません。"; _description.Text = question?.Description ?? ""; _error.Text = "";
            _text.Visible = question?.AllowText == true; _text.Text = question is not null && _answers.TryGetValue(question.Key, out var answer) ? answer.Text : "";
            _answers.TryGetValue(_currentKey ?? "", out var saved);
            if (question is not null)
            {
                foreach (var choice in question.Choices!)
                {
                    ButtonBase control = question.Multiple
                        ? new CheckBox { Text = choice.Text, AutoSize = true, Checked = saved?.Keys.Contains(choice.Key) == true }
                        : new RadioButton { Text = choice.Text, AutoSize = true, Checked = saved?.Keys.Contains(choice.Key) == true };
                    control.Tag = choice.Key; control.AccessibleDescription = choice.Description;
                    if (control is CheckBox check) check.CheckedChanged += ChoiceChanged;
                    if (control is RadioButton radio) radio.CheckedChanged += ChoiceChanged;
                    _choices.Add(control);
                    if (choice.Description.Length > 0) _choices.Add(Ui.Label(choice.Description).WithRole("muted"));
                }
            }
            PreviousButton.Enabled = index > 0; NextButton.Visible = index >= 0 && index + 1 < visible.Length;
            SkipButton.Enabled = question is { Required: false }; SubmitButton.Visible = index + 1 >= visible.Length;
        }
        finally { _rendering = false; }
        LayoutInvalidation.NotifyAncestors(this);
    }
    private void ChoiceChanged(object? sender, EventArgs e) { if (!_rendering) SaveCurrent(); }
    private QuestionAnswer ReadCurrent()
    {
        var keys = _choices.Controls.Cast<Control>().Where(control => control is CheckBox { Checked: true } or RadioButton { Checked: true }).Select(control => (string)control.Tag!).ToArray();
        return new QuestionAnswer(Array.AsReadOnly(keys), Current?.AllowText == true ? _text.Text : "");
    }
    private void SaveCurrent()
    {
        if (_currentKey is null) return;
        var answer = ReadCurrent();
        if (answer.Keys.Count == 0 && answer.Text.Length == 0 && _answers.GetValueOrDefault(_currentKey)?.Skipped == true) answer = answer with { Skipped = true };
        _answers[_currentKey] = answer;
    }
    private static string? ValidateAnswer(QuestionDefinition question, QuestionAnswer answer)
    {
        if (answer.Skipped) return question.Required ? "この質問への回答が必要です。" : null;
        if (question.Required && answer.Keys.Count == 0 && string.IsNullOrWhiteSpace(answer.Text)) return "回答を選択または入力してください。";
        return question.Validate?.Invoke(answer);
    }
    public bool Next()
    {
        SaveCurrent();
        if (Current is { } question && _answers.TryGetValue(question.Key, out var answer) && ValidateAnswer(question, answer) is { } error)
        { _error.Text = error; LayoutInvalidation.NotifyAncestors(this); return false; }
        var visible = VisibleQuestions(); int index = Array.FindIndex(visible, question => question.Key == _currentKey);
        if (index < 0 || index + 1 >= visible.Length) return false;
        _currentKey = visible[index + 1].Key; Render(); return true;
    }
    public bool Previous()
    {
        SaveCurrent(); var visible = VisibleQuestions(); int index = Array.FindIndex(visible, question => question.Key == _currentKey);
        if (index <= 0) return false; _currentKey = visible[index - 1].Key; Render(); return true;
    }
    public bool Skip()
    {
        if (Current is not { Required: false } question) return false;
        _answers[question.Key] = new QuestionAnswer(Array.Empty<string>(), "", true);
        var visible = VisibleQuestions(); int index = Array.FindIndex(visible, item => item.Key == _currentKey);
        if (index + 1 >= visible.Length) return Submit(saveCurrent: false);
        _currentKey = visible[index + 1].Key; Render(); return true;
    }
    public bool Submit() => Submit(true);
    private bool Submit(bool saveCurrent)
    {
        if (saveCurrent) SaveCurrent(); var visible = VisibleQuestions();
        foreach (var question in visible)
        {
            var answer = _answers.GetValueOrDefault(question.Key) ?? new QuestionAnswer(Array.Empty<string>());
            if (ValidateAnswer(question, answer) is not { } error) continue;
            _currentKey = question.Key; Render(); _error.Text = error; LayoutInvalidation.NotifyAncestors(this); return false;
        }
        var result = visible.ToDictionary(question => question.Key, question => _answers.GetValueOrDefault(question.Key) ?? new QuestionAnswer(Array.Empty<string>()));
        Submitted?.Invoke(this, new QuestionnaireSubmittedEventArgs(new ReadOnlyDictionary<string, QuestionAnswer>(result))); return true;
    }
    public void SaveState(string path)
    {
        SaveCurrent(); File.WriteAllText(path, JsonSerializer.Serialize(new SavedState(_currentKey, _answers), new JsonSerializerOptions { WriteIndented = true }));
    }
    public void RestoreState(string path)
    {
        var state = JsonSerializer.Deserialize<SavedState>(File.ReadAllText(path)) ?? throw new InvalidDataException("回答データが空です。");
        foreach (var (key, answer) in state.Answers)
        {
            var question = _questions.FirstOrDefault(question => question.Key == key) ?? throw new InvalidDataException($"不明な質問キーです: {key}");
            if (answer.Keys is null || answer.Keys.Any(value => !question.Choices!.Any(choice => choice.Key == value)) || (!question.Multiple && answer.Keys.Count > 1)) throw new InvalidDataException("保存された選択肢が無効です。");
        }
        _answers.Clear(); foreach (var (key, answer) in state.Answers) _answers[key] = answer with { Keys = Array.AsReadOnly(answer.Keys.ToArray()) };
        var visible = VisibleQuestions(); _currentKey = visible.Any(question => question.Key == state.CurrentKey) ? state.CurrentKey : visible.FirstOrDefault()?.Key; Render();
    }
    public bool SelectAnswer(string key, params string[] choices)
    {
        if (_currentKey != key || Current is not { } question || choices.Any(value => !question.Choices!.Any(choice => choice.Key == value)) || (!question.Multiple && choices.Length > 1)) return false;
        _answers[key] = new QuestionAnswer(Array.AsReadOnly(choices.ToArray()), _text.Text); Render(); return true;
    }
    public void SetTextAnswer(string text) => _text.Text = text;
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.Enter)) return Submit();
        if (keyData == (Keys.Alt | Keys.Right)) return Next();
        if (keyData == (Keys.Alt | Keys.Left)) return Previous();
        return base.ProcessCmdKey(ref msg, keyData);
    }
    public override Size GetPreferredSize(Size proposedSize) => _body is null ? base.GetPreferredSize(proposedSize) : _body.GetPreferredSize(proposedSize);
    private sealed record SavedState(string? CurrentKey, Dictionary<string, QuestionAnswer> Answers);
}
