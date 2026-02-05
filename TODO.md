# TODO (Phase 1 Completion)

- [x] Add gateway unreachable banner + message in UI
- [x] Add last action summary (connect/disconnect) with timestamp
- [x] Add app log shortcut and last error summary in Logs tab
- [x] Add diagnostics summary (index file) + copy path button
- [x] Add CLI tests for theme + tray notification flags
- [x] Update README: theme section + "What’s next" list

# TODO (Phase 2 Completion)

- [x] Exec approvals UI with allow/deny workflow
- [x] Approval history log + UI surface
- [x] System notifications bridge + rate limiting toggle
- [x] Exec policy control (prompt/allow/deny)
- [x] CLI flags + tests for policy and notifications

# TODO (Phase 3 In Progress)

- [ ] Confirm scope choices (Canvas tab vs window, WebView2 dependency, initial focus)
- [ ] Add WebView2 dependency + runtime availability check + guidance UI
- [ ] Canvas UI surface (tab + loading/empty states)
- [ ] Canvas service contracts (`canvas.eval`, `canvas.snapshot`)
- [ ] A2UI render pipeline into canvas
- [ ] Snapshot implementation (WebView capture)
- [ ] Screen capture service (multi-monitor + visible indicator)
- [ ] Browser relay UX upgrades (status + verify + guidance)
- [ ] Add tests for canvas/capture logic (agent-testable)
- [ ] Run full `dotnet test`
