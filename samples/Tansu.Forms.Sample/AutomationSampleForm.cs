using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Tansu.Forms.Sample;

/// <summary>A deterministic native UI target for the external UI Automation tests.</summary>
internal sealed class AutomationSampleForm : Form
{
    private readonly ModelForm<AutomationRecord> _editor = new(new AutomationRecord { PartNumber = "P-INITIAL", GoodCount = 1 }) { ValidateOnChange = true };
    private readonly Label _state = Elements.Label("");
    private readonly Label _errors = Elements.Label("");
    private readonly Label _status = Elements.Label("待機中");
    private readonly AsyncButton _save;
    private TaskCompletionSource? _completion;

    internal AutomationSampleForm()
    {
        Text = "Tansu.Forms UI Automation"; Name = "automation.sample";
        _state.Name = "form.state"; _errors.Name = "form.errors"; _status.Name = "operation.status";
        ShowInTaskbar = false; StartPosition = FormStartPosition.Manual; Location = new Point(-10000, -10000);
        var cancel = Elements.Button("取り消す", () => _save!.Cancel()); cancel.Name = "production.cancel";
        var finish = Elements.Button("処理を完了", () => _completion?.TrySetResult()); finish.Name = "production.complete";
        _save = Elements.ButtonAsync("登録", async token =>
        {
            if (!_editor.TryRead(out var record)) return;
            _completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _editor.Enabled = false; _status.Text = "処理中";
            try
            {
                await _completion.Task.WaitAsync(token);
                _editor.AcceptChanges(); _status.Text = $"登録済み: {record!.PartNumber}/{record.GoodCount}";
            }
            finally { _completion = null; if (!IsDisposed) _editor.Enabled = true; }
        }, () => _editor.IsDirty && !_editor.HasErrors);
        _save.Name = "production.save";
        _save.ExecutionFailed += (_, e) => _status.Text = "失敗: " + e.Exception.Message;
        _save.AsyncCommand.StateChanged += (_, _) =>
        {
            if (IsDisposed || Disposing) return;
            cancel.Enabled = _save.AsyncCommand.CanBeCanceled; finish.Enabled = _save.AsyncCommand.IsRunning;
            if (_save.AsyncCommand.IsCanceled && !_save.AsyncCommand.IsRunning) _status.Text = "取り消しました";
        };
        cancel.Enabled = false; finish.Enabled = false;
        _editor.StateChanged += (_, _) =>
        {
            _save.AsyncCommand.NotifyCanExecuteChanged(); UpdateState();
        };
        var failure = Elements.ButtonAsync("失敗を確認", _ => Task.FromException(new InvalidOperationException("テストエラー"))); failure.Name = "production.fail";
        failure.ExecutionFailed += (_, e) => _status.Text = "失敗: " + e.Exception.Message;
        var reset = Elements.Button("リセット", _editor.Reset); reset.Name = "form.reset";
        Tansu.Forms.Layout.Layout.Root(this, RootSize.Standard, _editor,
            Tansu.Forms.Layout.Layout.Wrap(_save, cancel, finish, reset, failure), _state, _errors, _status);
        UpdateState();
    }

    private void UpdateState()
    {
        _state.Text = _editor.IsDirty ? "未保存の変更があります" : "変更なし";
        _errors.Text = _editor.HasErrors ? string.Join(" / ", _editor.ErrorsByField.Keys) : "エラーなし";
    }

    internal sealed class AutomationRecord
    {
        [Required, DisplayName("品番")] public string PartNumber { get; set; } = "";
        [Range(0, 1000), DisplayName("良品数")] public int GoodCount { get; set; }
    }
}
