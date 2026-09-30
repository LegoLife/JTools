#include "SnippetStore.h"

#include "../Common/Json.h"
#include "../Common/Util.h"

static std::wstring SnippetsPath()
{
    return AppDataFile(L"TextExpander", L"snippets.json");
}

SnippetStore::Entry SnippetStore::MakeEntry(uint32_t id, std::string abbreviation, std::string expansion)
{
    Entry entry;
    entry.abbreviation = Widen(abbreviation);
    entry.expansion = Widen(expansion);
    entry.snippet = { id, std::move(abbreviation), std::move(expansion) };
    return entry;
}

bool SnippetStore::Load()
{
    AcquireSRWLockExclusive(&lock_);
    entries_.clear();

    bool firstRun = false;
    std::string text;
    if (!FileExists(SnippetsPath()))
    {
        entries_.push_back(MakeEntry(nextId_++, "brb", "be right back"));
        entries_.push_back(MakeEntry(nextId_++, ";sig", "Best regards,\r\nYour Name"));
        entries_.push_back(MakeEntry(nextId_++, ";addr", "123 Main Street\r\nAnytown, USA 12345"));
        SaveLocked();
        firstRun = true;
    }
    else if (Json json; ReadTextFile(SnippetsPath(), text) && Json::Parse(text, json))
    {
        for (const Json& item : json.Items())
        {
            const Json* abbreviation = item.Get("Abbreviation");
            const Json* expansion = item.Get("Expansion");
            entries_.push_back(MakeEntry(nextId_++, abbreviation ? abbreviation->AsString() : "",
                                         expansion ? expansion->AsString() : ""));
        }
    }

    ReleaseSRWLockExclusive(&lock_);
    return firstRun;
}

bool SnippetStore::SaveLocked() const
{
    Json json = Json::Array();
    for (const Entry& entry : entries_)
    {
        Json item = Json::Object();
        item.Set("Abbreviation", Json::String(entry.snippet.abbreviation));
        item.Set("Expansion", Json::String(entry.snippet.expansion));
        json.Push(std::move(item));
    }
    return WriteTextFile(SnippetsPath(), json.Dump());
}

std::vector<Snippet> SnippetStore::Snapshot() const
{
    AcquireSRWLockShared(&lock_);
    std::vector<Snippet> result;
    result.reserve(entries_.size());
    for (const Entry& entry : entries_)
        result.push_back(entry.snippet);
    ReleaseSRWLockShared(&lock_);
    return result;
}

uint32_t SnippetStore::Upsert(uint32_t id, const std::string& abbreviation, const std::string& expansion)
{
    AcquireSRWLockExclusive(&lock_);
    Entry* existing = nullptr;
    for (Entry& entry : entries_)
        if (entry.snippet.id == id)
            existing = &entry;

    if (existing)
        *existing = MakeEntry(id, abbreviation, expansion);
    else
    {
        id = nextId_++;
        entries_.push_back(MakeEntry(id, abbreviation, expansion));
    }
    bool saved = SaveLocked();
    ReleaseSRWLockExclusive(&lock_);
    return saved ? id : 0;
}

bool SnippetStore::Remove(uint32_t id)
{
    AcquireSRWLockExclusive(&lock_);
    std::erase_if(entries_, [id](const Entry& entry) { return entry.snippet.id == id; });
    bool saved = SaveLocked();
    ReleaseSRWLockExclusive(&lock_);
    return saved;
}

bool SnippetStore::FindMatch(std::wstring_view typedWord, SnippetMatch& match) const
{
    AcquireSRWLockShared(&lock_);
    const Entry* best = nullptr;
    for (const Entry& entry : entries_)
    {
        const std::wstring& abbreviation = entry.abbreviation;
        if (abbreviation.empty() || !typedWord.ends_with(abbreviation))
            continue;

        size_t start = typedWord.size() - abbreviation.size();
        if (start > 0 && IsCharAlphaNumericW(typedWord[start - 1]))
            continue;

        if (!best || abbreviation.size() > best->abbreviation.size())
            best = &entry;
    }
    if (best)
    {
        match.abbreviationLength = best->abbreviation.size();
        match.expansion = best->expansion;
    }
    ReleaseSRWLockShared(&lock_);
    return best != nullptr;
}
