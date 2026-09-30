#include "ExpanderHost.h"

#include "TextSender.h"

ExpanderHost::ExpanderHost()
{
    store.Load();
    settings.Load();

    lock_ = CreateMutexW(nullptr, TRUE, L"Local\\TextExpander.SingleInstance");
    if (lock_ && GetLastError() == ERROR_ALREADY_EXISTS)
    {
        CloseHandle(lock_);
        lock_ = nullptr;
    }
    if (!lock_)
        return;

    TextSender::Initialize();
    hook_ = std::make_unique<KeyboardHook>(store, settings);
    if (!hook_->Start())
        hook_.reset();
}

ExpanderHost::~ExpanderHost()
{
    hook_.reset();
    if (lock_)
    {
        ReleaseMutex(lock_);
        CloseHandle(lock_);
    }
}

void ExpanderHost::SetEnabled(bool enabled)
{
    if (hook_)
        hook_->enabled = enabled;
}
