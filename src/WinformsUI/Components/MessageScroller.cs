using System.ComponentModel;

namespace WinformsUI;

public sealed record TranscriptPosition(string? MessageId, int Offset, bool Following);

/// <summary>Stable transcript scroll positions. No auto-follow until requested by the reader.</summary>
public sealed class MessageScroller : Panel
{
    private readonly StackPanel _stack = WinformsUI.Layout.Stack();
    private readonly List<ConversationMessage> _messages = [];
    private bool _arranging;
    private bool _mutating;
    private bool _programmaticScroll;
    private int _extraBottomSpace;
    private string? _turnAnchor;
    private readonly Dictionary<string, (int Top, int Height)> _positions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Panel> _placeholders = new(StringComparer.Ordinal);
    private bool _virtualize;
    [DefaultValue(false)]
    public bool Virtualize
    {
        get => _virtualize;
        set { _virtualize = value; PerformLayout(); }
    }
    public int RealizedMessageCount => _messages.Count(message => message.Parent == _stack);
    public bool FollowingLiveEdge { get; private set; }
    public bool IsLoadingHistory { get; private set; }
    public event EventHandler? StateChanged;
    public IReadOnlyList<ConversationMessage> Messages => _messages.AsReadOnly();
    public MessageScroller()
    {
        AutoScroll = true; Controls.Add(_stack); MinimumSize = new Size(0, 180); Height = 360; TabStop = true;
    }
    public void Append(ConversationMessage message, bool startTurn = false)
    {
        EnsureUnique([message]); var position = CapturePosition(); _mutating = true;
        try { _messages.Add(message); _stack.Add(message); BindReadingSignals(message); }
        finally { _mutating = false; }
        if (startTurn)
        {
            _turnAnchor = message.Id; _extraBottomSpace = ClientSize.Height;
            FollowingLiveEdge = true; PerformLayout(); JumpTo(message.Id, 24, resumeFollowing: true);
        }
        else { PerformLayout(); if (FollowingLiveEdge) FollowAfterMutation(); else RestorePosition(position); }
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
    public void PrependHistory(IEnumerable<ConversationMessage> messages)
    {
        var history = messages.ToArray(); EnsureUnique(history); var position = CapturePosition(); _mutating = true;
        try
        {
            _messages.InsertRange(0, history);
            foreach (var message in history) { _stack.Controls.Add(message); BindReadingSignals(message); }
            for (int i = 0; i < _messages.Count; i++)
            {
                Control row = _placeholders.TryGetValue(_messages[i].Id, out var placeholder) ? placeholder : _messages[i];
                _stack.Controls.SetChildIndex(row, i);
            }
        }
        finally { _mutating = false; }
        PerformLayout(); RestorePosition(position); StateChanged?.Invoke(this, EventArgs.Empty);
    }
    public void UpdateMessage(string id, Action<ConversationMessage> update)
    {
        var message = Find(id); var position = CapturePosition(); _mutating = true;
        try { update(message); }
        finally { _mutating = false; }
        PerformLayout(); if (FollowingLiveEdge) FollowAfterMutation(); else RestorePosition(position);
    }
    public void OpenTranscript(IEnumerable<ConversationMessage> messages, TranscriptPosition? position = null)
    {
        var loaded = messages.ToArray();
        if (loaded.Select(message => message.Id).Distinct(StringComparer.Ordinal).Count() != loaded.Length) throw new ArgumentException("メッセージIDが重複しています。");
        _mutating = true;
        try
        {
            foreach (var placeholder in _placeholders.Values) placeholder.Dispose(); _placeholders.Clear(); _positions.Clear();
            foreach (var message in _messages.ToArray()) message.Dispose(); _messages.Clear();
            foreach (var message in loaded) { _messages.Add(message); _stack.Controls.Add(message); BindReadingSignals(message); }
        }
        finally { _mutating = false; }
        FollowingLiveEdge = false; _turnAnchor = null; _extraBottomSpace = 0; PerformLayout();
        if (position is not null) RestorePosition(position);
        else if (_messages.LastOrDefault(message => message.IsUser) is { } user) JumpTo(user.Id);
        else if (_messages.FirstOrDefault() is { } first) JumpTo(first.Id);
    }
    public TranscriptPosition CapturePosition()
    {
        var message = _messages.FirstOrDefault(message => Position(message).Top + Position(message).Height + _stack.Top > 0);
        return new TranscriptPosition(message?.Id, message is null ? 0 : Position(message).Top + _stack.Top, FollowingLiveEdge);
    }
    public void RestorePosition(TranscriptPosition position)
    {
        if (position.MessageId is { } id && _messages.FirstOrDefault(message => message.Id == id) is { } message)
            SetScroll(Math.Max(0, Position(message).Top - position.Offset));
        FollowingLiveEdge = position.Following;
    }
    public void JumpTo(string id, int contextPixels = 0, bool resumeFollowing = false)
    {
        var message = Find(id); PerformLayout(); SetScroll(Math.Max(0, Position(message).Top - contextPixels));
        if (_virtualize) PerformLayout();
        FollowingLiveEdge = resumeFollowing; StateChanged?.Invoke(this, EventArgs.Empty);
    }
    public void FollowLatest() { _turnAnchor = null; _extraBottomSpace = 0; FollowingLiveEdge = true; PerformLayout(); SetScroll(Math.Max(0, _stack.Height - ClientSize.Height)); StateChanged?.Invoke(this, EventArgs.Empty); }
    public void PauseFollowing() { FollowingLiveEdge = false; StateChanged?.Invoke(this, EventArgs.Empty); }
    public void CompleteTurn() { _turnAnchor = null; _extraBottomSpace = 0; PerformLayout(); }
    public async Task LoadEarlierAsync(Func<Task<IEnumerable<ConversationMessage>>> load)
    {
        if (IsLoadingHistory) return;
        IsLoadingHistory = true; StateChanged?.Invoke(this, EventArgs.Empty);
        try { var history = await load(); if (!IsDisposed) PrependHistory(history); }
        finally { IsLoadingHistory = false; if (!IsDisposed) StateChanged?.Invoke(this, EventArgs.Empty); }
    }
    private void FollowAfterMutation()
    {
        if (_turnAnchor is { } id && _messages.FirstOrDefault(message => message.Id == id) is { } turn && _stack.Height - Position(turn).Top < ClientSize.Height)
            JumpTo(id, 24, true);
        else SetScroll(Math.Max(0, _stack.Height - ClientSize.Height));
    }
    private void SetScroll(int y)
    {
        _programmaticScroll = true;
        try { AutoScrollPosition = new Point(0, y); }
        finally { _programmaticScroll = false; }
        if (_virtualize && !_arranging && !_mutating) PerformLayout();
    }
    private ConversationMessage Find(string id) => _messages.FirstOrDefault(message => message.Id == id) ?? throw new ArgumentException("メッセージIDがありません。", nameof(id));
    private void EnsureUnique(ConversationMessage[] messages)
    {
        if (messages.Any(message => _messages.Any(existing => existing.Id == message.Id)) || messages.Select(message => message.Id).Distinct(StringComparer.Ordinal).Count() != messages.Length)
            throw new ArgumentException("メッセージIDが重複しています。");
    }
    private void BindReadingSignals(Control control)
    {
        control.MouseDown += (_, _) => PauseFollowing(); control.KeyDown += (_, _) => PauseFollowing();
        foreach (Control child in control.Controls) BindReadingSignals(child);
    }
    protected override void OnScroll(ScrollEventArgs se)
    {
        base.OnScroll(se); if (!_programmaticScroll && !_arranging) { PauseFollowing(); if (_virtualize) PerformLayout(); }
    }
    protected override void OnLayout(LayoutEventArgs e)
    {
        if (_arranging || _mutating || _stack is null) return;
        var anchor = CapturePosition();
        _arranging = true;
        try
        {
            base.OnLayout(e);
            for (int pass = 0; pass < 3; pass++)
            {
                Size viewport = ClientSize;
                RealizeRows(viewport);
                int height = _stack.GetPreferredSize(new Size(Math.Max(1, viewport.Width), 0)).Height;
                _stack.SetBounds(0, AutoScrollPosition.Y, viewport.Width, height);
                AutoScrollMinSize = new Size(0, height + _extraBottomSpace); AdjustFormScrollbars(true);
                if (viewport == ClientSize) break;
            }
            if (!FollowingLiveEdge && anchor.MessageId is not null)
            {
                RestorePosition(anchor);
                if (_virtualize) RealizeRows(ClientSize);
            }
        }
        finally { _arranging = false; }
    }
    private (int Top, int Height) Position(ConversationMessage message) => _positions.GetValueOrDefault(message.Id, (message.Top, message.Height));
    private void RealizeRows(Size viewport)
    {
        int y = 0, scroll = -AutoScrollPosition.Y, gap = (int)Math.Round(_stack.Gap * DeviceDpi / 96d);
        _stack.SuspendLayout();
        try
        {
            for (int i = 0; i < _messages.Count; i++)
            {
                var message = _messages[i];
                int height = message.GetPreferredSize(new Size(Math.Max(1, viewport.Width), 0)).Height;
                _positions[message.Id] = (y, height);
                // Only rows within a one-viewport overscan region need rich native controls attached.
                bool realize = !_virtualize || message.ContainsFocus || (y + height >= scroll - viewport.Height && y <= scroll + viewport.Height * 2);
                Control row;
                if (realize)
                {
                    if (_placeholders.Remove(message.Id, out var placeholder)) placeholder.Dispose();
                    if (message.Parent != _stack) _stack.Controls.Add(message);
                    row = message;
                }
                else
                {
                    if (message.Parent == _stack) _stack.Controls.Remove(message);
                    if (!_placeholders.TryGetValue(message.Id, out var placeholder))
                    {
                        placeholder = new Panel { TabStop = false, Margin = Padding.Empty };
                        _placeholders.Add(message.Id, placeholder); _stack.Controls.Add(placeholder);
                    }
                    placeholder.MinimumSize = new Size(0, height); row = placeholder;
                }
                _stack.Controls.SetChildIndex(row, i); y += height + gap;
            }
        }
        finally { _stack.ResumeLayout(true); }
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) foreach (var message in _messages) if (message.Parent is null) message.Dispose();
        base.Dispose(disposing);
    }
    public override Size GetPreferredSize(Size proposedSize) => new(proposedSize.Width > 0 ? proposedSize.Width : 480, Math.Max(180, Height));
}
