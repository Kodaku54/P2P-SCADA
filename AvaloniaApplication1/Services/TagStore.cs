using System;
using System.Collections.Generic;
using Avalonia.Threading;

namespace AvaloniaApplication1.Services;

public enum TagQuality
{
    Good,
    Stale // зв'язок втрачено, значення застаріле
}

public readonly record struct TagValue(double Value, DateTimeOffset Timestamp, TagQuality Quality = TagQuality.Good);

/// <summary>
/// Сховище поточних значень тегів. Будь-який потік може викликати Update/MarkStale,
/// підписники завжди отримують значення в UI-потоці.
/// TagId має вигляд "{deviceId}/{sensorId}".
/// </summary>
public sealed class TagStore
{
    public static TagStore Default { get; } = new();

    private readonly object _lock = new();
    private readonly Dictionary<string, TagValue> _values = new();
    private readonly Dictionary<string, List<Action<TagValue>>> _handlers = new();

    /// <summary>Записує нове значення і сповіщає підписників (з будь-якого потоку).</summary>
    public void Update(string tagId, double value, DateTimeOffset? timestamp = null)
    {
        var tagValue = new TagValue(value, timestamp ?? DateTimeOffset.UtcNow);
        Action<TagValue>[] handlers;

        lock (_lock)
        {
            _values[tagId] = tagValue;
            handlers = SnapshotHandlers(tagId);
        }

        Notify(handlers, tagValue);
    }

    /// <summary>Позначає всі теги, що починаються з префікса (наприклад "rpi1/"), застарілими.</summary>
    public void MarkStale(string tagPrefix)
    {
        var pending = new List<(Action<TagValue>[] Handlers, TagValue Value)>();

        lock (_lock)
        {
            foreach (var key in new List<string>(_values.Keys))
            {
                if (!key.StartsWith(tagPrefix, StringComparison.Ordinal)) continue;

                var stale = _values[key] with { Quality = TagQuality.Stale };
                _values[key] = stale;
                pending.Add((SnapshotHandlers(key), stale));
            }
        }

        foreach (var (handlers, value) in pending)
            Notify(handlers, value);
    }

    public bool TryGet(string tagId, out TagValue value)
    {
        lock (_lock)
            return _values.TryGetValue(tagId, out value);
    }

    /// <summary>
    /// Підписка на тег. Якщо значення вже є, обробник одразу викликається з ним
    /// (у потоці виклику, тому підписуйтесь з UI-потоку, наприклад у конструкторі віджета).
    /// Далі обробник викликається в UI-потоці. Dispose скасовує підписку.
    /// </summary>
    public IDisposable Subscribe(string tagId, Action<TagValue> handler)
    {
        TagValue current;
        bool hasCurrent;

        lock (_lock)
        {
            if (!_handlers.TryGetValue(tagId, out var list))
                _handlers[tagId] = list = new List<Action<TagValue>>();

            list.Add(handler);
            hasCurrent = _values.TryGetValue(tagId, out current);
        }

        if (hasCurrent)
            Invoke(handler, current);

        return new Subscription(this, tagId, handler);
    }

    private Action<TagValue>[] SnapshotHandlers(string tagId) =>
        _handlers.TryGetValue(tagId, out var list) ? list.ToArray() : Array.Empty<Action<TagValue>>();

    private static void Notify(Action<TagValue>[] handlers, TagValue value)
    {
        if (handlers.Length == 0) return;

        Dispatcher.UIThread.Post(() =>
        {
            foreach (var handler in handlers)
                Invoke(handler, value);
        });
    }

    private static void Invoke(Action<TagValue> handler, TagValue value)
    {
        try
        {
            handler(value);
        }
        catch (Exception ex)
        {
            // Один зламаний віджет не повинен зупиняти оновлення інших
            System.Diagnostics.Debug.WriteLine($"[TagStore] Помилка обробника: {ex.Message}");
        }
    }

    private void Unsubscribe(string tagId, Action<TagValue> handler)
    {
        lock (_lock)
        {
            if (_handlers.TryGetValue(tagId, out var list))
                list.Remove(handler);
        }
    }

    private sealed class Subscription : IDisposable
    {
        private TagStore? _store;
        private readonly string _tagId;
        private readonly Action<TagValue> _handler;

        public Subscription(TagStore store, string tagId, Action<TagValue> handler)
        {
            _store = store;
            _tagId = tagId;
            _handler = handler;
        }

        public void Dispose()
        {
            _store?.Unsubscribe(_tagId, _handler);
            _store = null;
        }
    }
}