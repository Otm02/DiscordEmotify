using System;
using System.Collections.Generic;
using DiscordEmotify.Gui.Utils.Extensions;

namespace DiscordEmotify.Gui.Utils;

internal class DisposableCollector : IDisposable
{
    private readonly object _lock = new();
    private readonly List<IDisposable> _items = [];

    public void Add(IDisposable item)
    {
        lock (_lock)
        {
            _items.Add(item);
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _items.DisposeAll();
            _items.Clear();
        }
    }
}
