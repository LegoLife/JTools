# aBreevy8 feature inventory

A list of everything aBreevy8 (a commercial text expander by 16 Software LLC) can do, compared with JTools, to help decide what to build into JTools' Expand tool next.

- **Version examined:** 2024.04.10, installed at `C:\Program Files (x86)\aBreevy8`. It is a native app built with GTK 3 and MinGW.
- **How it was examined:** its bundled help pages (`help\index.html`, `help\keys.html`), `changelog.txt`, the included abbreviation sets (`sets\*.bvy`), and the interface text (labels, tooltips, messages) stored in `aBreevy8.exe`. No code was decompiled, and the app wasn't run.
- **Written:** 2026-10-01, against JTools 0.10.0.

**JTools column:** **Yes** means JTools has it, **Partly** means a narrower version, **No** means JTools doesn't have it, and **n/a** means it doesn't apply.

In aBreevy8, an abbreviation is called an *aBreevy8tion*, and the text it expands to is the *Replacement Text*.

## Fill-in variables: `%(input)` / `%(fill)`

This is the feature that "remembers variables as you type". It's the one aBreevy8 feature examined in detail here.

### How it behaves (from the help file)

1. **The expansion pauses inline.** When the expansion reaches `%(input name)`, aBreevy8 types the identifier itself (`name`) into the target app, selects it, and stops. No popup or dialog appears; you stay in the app you were typing in.
2. **You type the value.** Typing replaces the selected `name`.
3. **Tab continues.** Pressing **Tab** finishes the input, and aBreevy8 carries on typing the rest of the replacement text until the next `%(input)` or `%(fill)`. The help stresses: "Don't forget to press Tab when you're done typing your input!"
4. **Same identifier, same value.** Any later `%(input name)` or `%(fill name)` in the same replacement text outputs what you typed the first time, without pausing again.
5. **Values last only for one expansion.** "The text you input for an identifier will not be 'remembered' after aBreevy8 finishes outputting the replacement text."
6. **Escape cancels** at any time.
7. **The identifier is optional.** A bare `%(input)` pauses for a one-off value that can't be reused.
8. **The two names are aliases.** `%(fill …)` and `%(input …)` are interchangeable, and the names aren't case-sensitive: the exe also recognizes `%(Input`, `%(Fill`.

Example from the help file:

```
Hello, my name is %(input name) and I live in %(input country). I repeat, my name is %(input name).
```

This pauses twice, for `name` and then `country`, and fills in the second `name` automatically.

### What the documentation doesn't say

Running the app would answer these:

- Is the Tab that ends an input swallowed? Presumably yes.
- Can Backspace move back past the start of the selected placeholder?
- What happens if you click elsewhere or switch windows while it's paused? aBreevy8 normally stops typing if the active window changes, unless *Continue outputting replacement text if active window changes* is on.
- Does a value typed into `%(input)` get used inside an embedded `%(aBreevy8tion other)`?
- How does it work with the clipboard paste output method? It probably requires the simulated-keypress method.

### Relevance to JTools

JTools' first roadmap ruled out `{input}` because a popup asking for the value would take focus from the target app. aBreevy8 shows it can be done **inline**, with the placeholder typed and selected and Tab to continue, so that objection may not apply. JTools pastes its expansions in one go, so pausing partway would need the paste split into a part before the input and a part after it.

## Abbreviations and matching

| Feature | What it does | JTools |
| --- | --- | --- |
| Text abbreviations | Typed abbreviation is replaced by replacement text, in any app | Yes |
| Multi-line replacement text | Replacement can be any length, including paragraphs | Yes |
| Description | Optional short description shown in the abbreviation list | No |
| Case sensitive match | Only the exact case matches | Yes (the only mode) |
| Case insensitive match | `ab`, `Ab`, `AB` all match | No |
| Adapt case | `Ab` gives "A boy", `AB` gives "A BOY", `ab` unchanged | No |
| Per-abbreviation match mode | Each abbreviation picks its own case mode; a default applies to new ones | No |
| Several abbreviations, one replacement | `who\|what\|where` all expand to the same text | No |
| Vary8tions | Several abbreviations with the same name and different replacements; one is picked at random each time (e.g. rotating email sign-offs) | No (duplicates are rejected) |
| Within-a-Word Mode (was "Anywhere Mode") | Abbreviation expands even when typed straight after a letter or digit | No (needs a word boundary) |
| Apostrophe option | *Don't consider apostrophe a delimiter*, so one-letter abbreviations don't fire inside contractions | n/a |
| Embed another abbreviation | `%(aBreevy8tion myname)` / `%(abbrev myname)` inserts another abbreviation's replacement | No |

## When expansion happens

| Feature | What it does | JTools |
| --- | --- | --- |
| Immediately | Expands as soon as the last character is typed | Yes |
| After the trigger key | A chosen key (e.g. Tab) must follow; the key is discarded | Partly (Space, Tab and/or Enter; keeping or dropping the key is one global setting) |
| After a word-ending character | Any of a configurable set of characters (e.g. `.`) must follow; the character is kept | Partly (no custom character set) |
| Per-abbreviation replacement mode | Each abbreviation picks its own mode; a default applies to new ones | No (one global mode) |
| Preview tooltip | While you type an abbreviation that still needs its trigger, a tooltip near the caret shows what it will expand to; can be skipped when the caret position can't be detected | No |
| "Pop" sound | Plays a sound when an abbreviation expands | No |

## Insertion macros

The editor's blue-arrow menu inserts these, and the help pages document them.

| Macro | What it does | JTools |
| --- | --- | --- |
| `%(input id)` / `%(fill id)` | Inline fill-in with reusable variables (see above) | Yes (`{input:name}`, also finished by Enter) |
| `%(clipboard)` | Inserts the clipboard text | Yes (`{clipboard}`) |
| `%(cursor)` | Leaves the caret here after expanding | Yes (`{cursor}`) |
| Date and time parts | `%(Year)`, `%(YearShort)`, `%(Month)`, `%(MonthName)`, `%(MonthNameShort)`, `%(Day)`, `%(WeekdayName)`, `%(WeekdayNameShort)`, `%(DayOfWeek)`, `%(DayOfWeekMon)`, `%(DayOfYear)`, `%(WeekOfYear)`, `%(Hour)`, `%(Hour24)`, `%(Minute)`, `%(Second)`, `%(AMorPM)`; a `#` prefix (`%(#Day)`) drops leading zeros | Yes, through .NET formats (`{date:dddd, MMMM d}`); week of year and day of year have no .NET format code |
| Ready-made date layouts | Menu presets such as `%(MonthName) %(Day), %(Year)` | Partly (`{date}`/`{time}` use your Windows format) |
| Date and time math | `%(AddDays 3)`, `%(SubtractHours 1)` (also `%(SubDays …)`) for years, months, days, hours, minutes and seconds, placed before the date macro it changes | No |
| `%(Key …)` | Presses a key or combination, optionally repeated: `%(Key Shift+Tab)`, `%(Key Space 10)` | No |
| `%(KeyDown …)` / `%(KeyUp …)` | Holds a key down until released later in the text | No |
| Key names | Ctrl/LCtrl/RCtrl, Alt, Shift, Win/LWin/RWin, Menu/App, Enter, Backspace/BkSp, Insert, Delete, Space, Tab, PageUp/PgUp, PageDown/PgDown, Home, End, arrows, Escape, PrintScreen, Pause, CapsLock, NumLock, ScrollLock, F1–F24, A–Z, 0–9, Plus/Add, Comma, Minus, Period, Numpad0–9, NumpadDivide/Multiply/Decimal/Add | No |
| `%(B)` `%(I)` `%(U)` | Toggle bold, italic and underline (sends Ctrl+B/I/U) | No |
| Formatted text and pictures | Rich replacement text with a B/I/U formatting bar; tuned for Word, Outlook and Gmail | No |
| `%(Delay 500ms)` | Waits before continuing (`1s500ms`, or a bare number of milliseconds) | No |
| `%(Launch "path" …)` | Launches a program, file or folder from inside a text expansion; options `-parameters`, `-run-in`, `-show-minimized`/`-show-maximized`, `-run-as-admin` | No |
| Emoji picker | Inserts an emoji into the replacement text (Ctrl+.) | No (Windows' own Win+. works in the editor) |

## Launchers

| Feature | What it does | JTools |
| --- | --- | --- |
| Program, file or folder launcher | Typing the abbreviation deletes it and opens a path, with parameters, run-in folder, normal/minimized/maximized window | No |
| Website launcher | Typing the abbreviation deletes it and opens a URL in the default or a chosen browser | No |
| LaunchPad | Ctrl+Space opens a small box for typing launcher abbreviations when the current app doesn't accept text | No |

## How the text is typed, and compatibility

| Feature | What it does | JTools |
| --- | --- | --- |
| Simulated key presses ("Normal" mode) | Types the replacement as fast keystrokes; the recommended default | No |
| Clipboard paste ("Turbo" mode) | Pastes through the clipboard; faster for long text | Yes (JTools always pastes) |
| Auto-switch to Turbo | Picks keystrokes or paste per expansion, whichever is faster | No |
| Slow mode | Keystrokes with a delay after each one, for apps that can't keep up | No |
| Continue if the active window changes | Normally output stops if another window takes focus; this keeps typing (needed for `%(Key Win+R)`-style macros) | n/a (JTools pastes in one step) |
| Run as administrator option | Lets it expand in elevated apps | No (documented as a Windows limitation) |
| Alt Compatible Mode | Fixes stray characters after Alt key combinations in some apps | No |
| Ultra Compatible Mode | Fallback for apps where expansion doesn't work | No |
| Troubleshooting tab | Groups the compatibility options above in Preferences | No |

## Hotkeys and quick access

| Feature | What it does | JTools |
| --- | --- | --- |
| Show main window | Hotkey to open the editor | Yes (Ctrl+Alt+J) |
| Toggle on/off hotkey | Turns expansion on or off | Partly (tray menu and Expand switch, no hotkey) |
| QuickAdd Selection | Alt+Shift+S: makes a new abbreviation from the text selected in another app | No |
| QuickAdd Clipboard | Hotkey: makes a new abbreviation from the clipboard text | No |
| Edit last typed abbreviation | Alt+Shift+L: opens the editor on the abbreviation that just expanded | No |
| Pop-up menu of everything | Hotkey shows a menu of all folders and abbreviations at the caret or mouse; click one to insert it | No |
| Per-folder pop-up hotkey | Right-click a folder to give it its own pop-up menu hotkey | No |
| LaunchPad hotkey | Ctrl+Space by default (see Launchers) | No |
| Hotkey conflict warning | Warns when a chosen hotkey is already used by another of its hotkeys | n/a (JTools' one hotkey isn't configurable) |

## Organizing and editing

| Feature | What it does | JTools |
| --- | --- | --- |
| Folders | Abbreviations live in folders; an "Everything" view shows all | No |
| Disable a folder | Right-click to stop a folder's abbreviations expanding without deleting them | No |
| Drag and drop | Move abbreviations and folders by dragging | No |
| Duplicate / Create Vary8tion | Copies an abbreviation as a starting point or as a random alternative | No |
| Bulk edit | Select several abbreviations, then change their case mode, replacement mode or Within-a-Word Mode at once, or copy, move or delete them | No |
| Search | Searches abbreviations, descriptions and replacement text | Partly (abbreviation and text only) |
| Boolean search | `x AND y NOT z`, `a OR b` | No |
| Search options | Only within the selected folder; include disabled folders | No |
| List columns | Optional created time, modified time and folder columns | No |
| Normal / Advanced editor view | Advanced shows extra per-abbreviation options (e.g. Within-a-Word Mode) | n/a |
| Undo / redo in the editor | Ctrl+Z / Ctrl+Y in the replacement text box | Yes (text box undo) |
| Save / Cancel | Edits apply only on Save; Cancel discards all of them | Partly (each snippet is saved individually) |
| Keyboard shortcuts | Ctrl+N new abbreviation, Ctrl+O new folder, Ctrl+L new launcher, Ctrl+W new website launcher, Ctrl+F search, Ctrl+P preferences, Ctrl+T team sharing, F1 help, Esc minimizes | Partly (Ctrl+N new, Ctrl+S save) |
| Statistics counter | Shows how many keystrokes it has saved you, with a reset button | No |
| Abbreviation counter | Shows how many abbreviations you have | No |

## Import, export and included sets

| Feature | What it does | JTools |
| --- | --- | --- |
| Export all / export a folder | Saves abbreviations to a `.bvy` file; double-clicking a `.bvy` file imports it | No |
| Export to HTML | A printable list | No |
| Import from Excel / spreadsheet | Copy columns A (abbreviations) and B (replacements) from a spreadsheet and paste them into a box | No |
| Import Microsoft Word AutoCorrect | Reads Word's AutoCorrect entries | No |
| Import TextExpander snippets | `.textexpander` files and TextExpander XML, with optional preference import | No |
| Import other tools | Shorthand dictionaries (`.spf`), ShortCut tables, SureType shortcut CSVs | No |
| Import from Breevy | Offers to import from its predecessor on first run | n/a |
| Typo AutoCorrections set | About 6,500 common misspellings corrected as you type | No |
| Medical Transcription set | About 13,500 medical abbreviations | No |
| Examples set | A guided tour written as abbreviations | No |

## Sync, sharing and backups

| Feature | What it does | JTools |
| --- | --- | --- |
| Cloud Sync | Point it at a Dropbox, OneDrive, Google Drive or iCloud folder on each PC and abbreviations stay in sync | No |
| TextExpander Dropbox sync | Two-way sync with TextExpander on Macs through Dropbox | No |
| TeamShare read-only folders | Subscribe to a shared `.bvy` or TextExpander file **or URL**; it reloads on a set interval and can't be edited locally | No |
| Automatic backups | A backup on every save, kept up to a maximum count in a chosen folder | No (saves are atomic but there's no history) |
| Backup Manager | Lists backups (latest first) and restores one | No |

## Interface and app

| Feature | What it does | JTools |
| --- | --- | --- |
| Tray app | Runs in the system tray; closing hides it | Yes |
| Start with Windows | Starts minimized to the tray | Yes |
| Dark / light mode | Theme choice | Yes (plus "use Windows setting") |
| Custom theme colors | Pick the abbreviation, replacement and accent colors | No (Tail's log colors only) |
| Interface font and size | Font for the whole window | No |
| Replacement editor font | Font for the replacement text box | No |
| Restart from the menu | File → Restart | No |
| Portable version | Keeps all data next to the exe on a USB drive | No (data is in `%APPDATA%`) |
| Update checker | Checks for and lists new versions | No (offline by design) |
| Help file and support form | Built-in help (F1) and a contact form | No |
| Free / paid modes | Free "Limited Mode" allows 11 abbreviations; an unlock code removes the limit | n/a |

## File format notes (`.bvy`)

These come from the included sets. They're useful if JTools ever imports aBreevy8 files. The meanings of the short attribute names are inferred, not documented.

```xml
<aBreevy8 n="Examples" t="0" d="0" w="0" e="1" h="17 16 69">
  <a a=".wlc" d="description" e="replacement text with %(B)macros%(B) and &#10; line breaks"
     c="2" t="3" r="0" x="071c07e70e1f19" y="011707e8023107"/>
  <p a="/calc" d="description" p="C:\Windows\System32\calc.exe"
     c="1" t="3" r="0" m="" i="" s="0" n="0" x="…" y="…"/>
</aBreevy8>
```

- `<aBreevy8>` (`<Breevy>` in older sets) is a folder: `n` is its name. The other attributes are probably flags such as disabled (`e`) and a pop-up hotkey (`h`).
- `<a>` is a text abbreviation: `a` the abbreviation, `d` the description, `e` the replacement text (XML-escaped, newlines as `&#10;`). `c` and `t` look like the case and replacement modes, `r` possibly Within-a-Word Mode, and `x`/`y` created and modified timestamps in hex.
- `<p>` is a program, file or folder launcher: `p` the path. `m`, `i`, `s`, `n` probably hold parameters, run-in folder, show mode and run as admin.
