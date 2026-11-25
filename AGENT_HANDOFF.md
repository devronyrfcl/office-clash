# Office Clash — agent handoff

Updated 2026-11-25. This is a practical summary of the conversation, not a verbatim transcript. Read the current code and scene before changing them: some work may have changed after this note was written.

## User and project

- Unity mobile multiplayer game, Office Clash, using Photon Fusion and PlayFab. The user wants to finish quickly and is currently focused on menu UI and backend, with step-by-step collaboration.
- The user prefers concrete implementation in Unity, including scene objects and button connections, rather than being asked to wire everything manually. Explain any PlayFab dashboard setup they must perform.
- UI style: `Assets/Layer Lab/GUI Pro-CasualGame`, uGUI and TextMeshPro. Main menu scene: `Assets/Scenes/MainMenu.unity`. Gameplay scene: `Assets/Scenes/Office.unity`.
- `README.md` describes the game and `TASKS.md` is the broader phase plan. The task plan may be stale relative to subsequent menu/backend work; verify the code instead of relying on checkboxes alone. Phase 5 is menu/lobby/matchmaking; mobile camera framing and real-device tests are among earlier outstanding items.
- The user selected **PlayFab legacy** features, legacy CloudScript revisions, and the free PlayFab tier. PlayFab title ID supplied by the user: `A3EB7`.

## Important working-tree / transfer note

As of this handoff the repository has extensive modified and untracked user work, including `MainMenu.unity`, scripts, PlayFab SDK, UI assets and `Backend/`. Do not reset or clean it. Untracked files and uncommitted changes do **not** travel to another device through Git automatically. The user needs to commit/push the desired files or otherwise transfer them. Also save the open Unity scene before transferring it; an editor can hold unsaved changes beyond what is on disk. Do not commit or push without the user's request.

## Main menu and authentication

- Main login/signup and startup flow: `Assets/Scripts/Network/Lobby.cs`; secure stored credentials: `Assets/Scripts/Network/SecureCredentials.cs`. User asked for remembered login at startup, a Tap to Start action, and showing login and the reason if automatic login fails. Recheck actual runtime behavior if debugging it.
- Title intro uses sequential slow DOTween animations with 0.5-second gaps. `Button_Start` moves along **Y**, then pulses scale 1 → 1.2 → 1. Decorative Flying Document objects float nearby via `Assets/Scripts/UI/FloatingDocument.cs`.
- General notifications use `Popup_Notify` and its `Text_Title` / `Text_Info`.
- Do not copy secrets or credentials into documentation. The user shared a Discord webhook in earlier CloudScript; treat it as exposed and avoid reproducing it. Rotation should be considered by the owner.

## Player data, profile and leaderboard

- `Assets/Scripts/UI/LobbyPlayerStats.cs` shows level, XP, energy, gold, diamonds, and display name. `Backend/PlayFab/complete-cloudscript.js` contains `getProgression`. The user wanted level 1 onward, growing XP targets and energy regeneration. Check implementation constants before describing exact balancing.
- Legacy virtual currency codes used by current code: `GD` for Gold and `DM` for Diamonds. Energy is stored in player read-only data and computed server-side. The user asked for Gold, Diamonds and Energy as reward types.
- `PlayerInfoPopup.cs` populates the user info popup; stats without implemented backend are shown as zero. `ChangeNamePopup.cs` calls the legacy CloudScript `changeDisplayName`; target cost is 50 Gold. A previous implementation reported name changes unavailable, so live deployment and actual result need checking when relevant.
- `ProfileAvatarPicker.cs` uses the sliced profile picture sheet, assigns a random avatar if none is assigned, and presents an avatar selection/confirmation popup. The image is under `Bg_Mask/Character`.
- `LeaderboardPanel.cs` connects the Ranking UI to a legacy PlayFab leaderboard and includes a fallback for the player's own entry even with zero score. Country/location was discussed, with the user reporting they completed a requested PlayFab setup; the precise dashboard state and leaderboard statistic settings are not confirmed here. Match statistics still need trustworthy match-end integration; see `TASKS.md`.

## Missions

`Assets/Scripts/UI/MissionsPanel.cs` and `getDailyMissions` in the CloudScript implement current mission UI/data. The user asked for Quest button access; Campaign and Clan should be visible but locked. Rewards must be limited to Gold, Diamonds and Energy. The user asked to move beyond placeholders, but this note does not assert every mission or reward is complete or connected to trusted match results: inspect and test before claiming so.

## Daily login reward calendar

- `Assets/Scripts/UI/DailyLoginPopup.cs` controls `Popup_RewardMonth`; scene has a `Button_Collect`, a lobby gift icon and `Button_Rewards` wired to open the calendar. The popup appears on the first login each UTC day; it can also be reopened manually. The user chose **calendar dates**, meaning missed days are skipped.
- Current pattern repeats **100 Gold, 5 Diamonds, 10 Energy** for days 1, 2, 3, and so on. Energy is capped at 60; an energy claim requires 10 free capacity. Date reset is midnight UTC.
- Backend is in `Backend/PlayFab/complete-cloudscript.js`: `getDailyLoginPopup`, `ackDailyLoginPopup`, `claimDailyLoginReward`. The client refreshes lobby stats after a successful claim. Scene setup and script compiled, and local mocked tests covered the three reward types and duplicate/error scenarios. No end-to-end live PlayFab claim was verified in the conversation.
- **Prototype risk:** legacy currency and player-data operations are not atomic. Current code attempts an at-most-once daily claim lock by creating a server-side shared group per player/day. This requires denying `/Client/CreateSharedGroup` in the title's API Access Policy to prevent clients pre-creating other players' locks. Groups accumulate, and a failure after acquiring the lock can leave a claim uncertain and require manual reconciliation. It is not production-ready exactly-once processing; concurrent energy data updates also merit review. Confirm this design, API limits, cleanup and live behavior before release.
- The user was instructed to upload `Backend/PlayFab/complete-cloudscript.js` as a new legacy CloudScript revision and set it live, and to configure the policy above. Completion is **not confirmed**. Preserve existing live handlers when deploying.

## Notice panel — latest work

- `Assets/Scripts/UI/NoticePanel.cs` opens a scrollable `Popup_Notice` in the MainMenu scene from `Button_Notice`. It loads current content from PlayFab client `GetTitleData`; the popup has close buttons and loading, empty and error states.
- Two **Primary Title Data** keys (not internal title data): `NoticeTitle` and `NoticeBody`. `NoticeBody` is required for a notice to appear; `NoticeTitle` is optional and defaults to NOTICE. In the latest screenshot PlayFab showed one primary key, `NoticeTitle`; the user was told to add `NoticeBody`, but success is not yet confirmed. PlayFab Title Data changes may take up to 15 minutes to propagate.
- Unity script compiled and scene connections were inspected; there was no live PlayFab end-to-end test. Scene file currently contains `Popup_Notice` and its references, but confirm that any newer editor changes were saved.
- Docs: https://learn.microsoft.com/en-us/gaming/playfab/live-service-management/game-configuration/titledata/quickstart

## Suggested next-agent workflow

1. Read `git status --short`, `README.md`, relevant parts of `TASKS.md`, and inspect the Unity scene and affected scripts. Preserve unrelated changes.
2. Ask what the user wants to work on next. If continuing notice setup, verify both Primary Title Data keys and test opening the notice after signing in; do not claim a live test happened unless it did.
3. Verify CloudScript revision and API policy before exercising actual reward claims; use a test account and do not grant real rewards merely to inspect UI.
4. Keep scene changes saved or explicitly tell the user when editor changes remain unsaved. Report actual tests and limitations.
