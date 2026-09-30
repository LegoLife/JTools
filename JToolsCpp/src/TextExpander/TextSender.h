#pragma once

#include <windows.h>

#include <string>

/// Inserts text into whatever window has focus.
namespace TextSender
{
    /// Tags keystrokes we inject so the keyboard hook doesn't treat them as user typing.
    constexpr ULONG_PTR InjectedMarker = 0x54455850;

    /// Creates the hidden window that owns the clipboard while an expansion is pasted.
    void Initialize();

    /// On a background thread: deletes the typed abbreviation, pastes the expansion, then optionally
    /// presses trailingVk (the trigger key, when the user wants to keep it; 0 for none).
    ///
    /// Pasting is used instead of typing the expansion one keystroke at a time because typed
    /// newlines become Enter presses (which send messages in chat apps) and long text gets
    /// dropped or garbled by apps that can't keep up with a burst of injected keystrokes.
    void ReplaceAsync(int backspaces, std::wstring expansion, WORD trailingVk);
}
