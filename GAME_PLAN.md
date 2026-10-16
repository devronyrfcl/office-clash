# Office Clash — Game Plan

> Living design + roadmap doc. Decisions confirmed on 2026-10-17 are listed in §1. Anything marked **(proposal)** is a suggestion that still needs a yes/no.

## 1. Confirmed decisions

| Topic | Decision |
|---|---|
| Mode | **10-player free-for-all battle.** Everyone fights everyone; the last player alive wins |
| Elimination | **Health bar.** Hits do damage; at 0 HP you're out |
| Getting up | **Self-recover only.** A heavy hit knocks you into ragdoll and you get back up on your own |
| Items | Office objects (keyboard, mouse, paperweight, …) can be thrown. **1 item per hand (2 total) + 2 pocket slots** |
| Character | **Office workers** (replaces the KayKit Knight) |
| Play | **Online only** |
| Platform | **Mobile: Google Play Store + Apple App Store** |
| Match hosting | **Client-host** (one phone hosts) for the prototype, **Fusion dedicated server** before launch |
| Backend | **Supabase** for accounts and player data (one company account across all our games), **Photon Fusion** for realtime gameplay |
| Monetization | **Free + cosmetics + ads** |
| Falling off the map | **Always goes through HP.** Heavy damage + respawn, never an instant elimination |
| Shrinking office | **Yes** |
| Healing pickups | **Yes** (Coffee) |

## 2. Pitch

**Office Clash** is an online physics-ragdoll battle for 10 players on mobile. Office workers wobble around an open-plan office, grab keyboards, mice, paperweights and chairs, stuff spare ammo in their pockets, and hurl everything at each other. Each hit drains health, heavy hits knock you flat, and the last worker standing wins the round.

- **Genre:** physics party brawler / mini battle royale
- **Players:** up to 10 per match, online (Photon Fusion)
- **Round length:** about 3–4 minutes, enforced by the shrinking office
- **Controls:** touch first. Gamepad support is a nice-to-have later

## 3. Current state (what's already in the repo)

| System | Where | Status |
|---|---|---|
| Networking (Fusion, AutoHostOrClient, one hard-coded session "TestSeasom") | `Scripts/Network/NetworkRunnerHandler.cs`, `Spawner.cs` | Works. No lobby/menu, and everyone spawns at `Vector3.zero` |
| Active-ragdoll character (joint-driven, limbs synced to the animator) | `NetworkPlayer.cs`, `Character/SyncPhysicsObject.cs`, `ConfigurableJointExtensions.cs` | Works: move, jump, extra gravity, respawn after falling out of the world |
| Knockdown | `Character/DetectCollision.cs` | A body part hit by a `CauseDamage` collider with impulse > 15 goes full ragdoll and gets knocked back |
| Revive | `NetworkPlayer.cs` | Self-revive with a button after 3 s, which already matches "self-recover only" |
| Grab & throw (hand joints, charged throw, arm swing, aim preview) | `Character/HandGrabHandler.cs`, `NetworkPlayer.UpdateGrab`, `Utils/ThrowAimUI.cs` | Works. Host-authoritative, **but holds only one item at a time** |
| Health / energy / sprint | `TopDownController.cs` | **Offline prototype only.** Can be ported for HP and stamina |
| Projectile ammo (big/mid/small, inventory counts, trajectory line) | `ShootSystem.cs`, `Prefab/big|Mid|small.prefab` | **Offline only.** Its inventory idea gets folded into the pocket system |
| Team props | `Prefab/powerupBlock_teamRed.prefab`, `swiper_teamBlue.prefab` | Not needed for free-for-all. Repurpose as power-up boxes or remove |
| Art | SimpleOfficeInteriors, KayKit Mini-Game pack, KayKit Adventurers (Knight), GUI Kit Yellow Kid | Imported |
| Scenes | `Regdoll Character.unity` (the only scene in the build), `Scenes/Office Scene 1`, `Multiuplayer Scene`, `Starting level`, `SampleScene` | Office Scene 1 isn't connected to networking yet |

### Known issues / tech debt to fix early
1. `isActiveRagdoll` and `lastTimeBecameRagdoll` in `NetworkPlayer` are plain fields, not `[Networked]`. Clients can disagree with the host about who is knocked down, and the get-up timer isn't synced.
2. Jump and revive presses are cleared inside `FixedUpdateNetwork`, which runs again on clients when they re-simulate. Switch to `NetworkButtons` + `WasPressed` so presses aren't lost or doubled.
3. Everyone spawns at the origin. 10 players need spread-out spawn points.
4. The session name is hard-coded, so there's no matchmaking.
5. `Fusion.Addons.Physics` must stay in `NetworkProjectConfig → AssembliesToWeave` (fixed 2026-10-17).
6. Input comes from the keyboard. Mobile needs a touch input layer feeding `NetworkInputData`.

## 4. Core loop

```
Quick Match → lobby fills (up to 10, short countdown) → drop into the office
  → scavenge: grab items into your hands, stash extras in your pockets
  → fight: throw items, hits drain HP, heavy hits knock you down
  → knocked down: ragdoll, then get up on your own
  → 0 HP: eliminated → spectate
  → the office shrinks over time to force fights
→ last worker alive wins → results screen → play again
```

## 5. Mechanics

### 5.1 Movement (exists)
Walk, jump, wobble. To add:
- **Sprint** with a stamina bar (port `TopDownController.HandleEnergy`)
- **Dive / tackle** **(proposal)**: a short lunge that does light damage and can knock someone down

### 5.2 Hands + pocket inventory (new)
- **Two hands.** Each hand holds one small or medium item. A big item (chair, printer) needs **both hands** and can't go in a pocket.
- **Two pocket slots.** Only small and medium items fit.
- **Actions:**
  - *Grab*: pick up the nearest item into a free hand. If both hands are full, the item goes into a free pocket slot
  - *Throw*: hold to charge, release to throw from the selected hand (the charge and arc preview already exist)
  - *Swap*: move a pocket item into an empty hand. Tap the pocket icon on the HUD
  - *Drop*: let go without throwing
- Getting knocked down **drops everything in your hands**. Pocket items are kept.
- On elimination, all of the player's items drop as loot **(proposal)**.
- Network state: replace `heldObjectId` with `[Networked]` slots: `leftHand`, `rightHand`, `pocket[2]` (`NetworkId`), all host-authoritative.

### 5.3 Items

| Class | Examples | Fits in pocket | Throw speed | Damage **(proposal)** | Knockdown |
|---|---|---|---|---|---|
| Small | mouse, stapler, mug, paperweight | yes | fast | 8–12 | only at full charge |
| Medium | keyboard, monitor, plant, phone | yes | medium | 15–20 | yes, above a speed threshold |
| Big (two hands) | office chair, printer, water cooler | no | slow | 30–40 | always, with big knockback |

- Damage = base damage × impact speed factor, so a weak lob barely hurts.
- Every throwable needs a `NetworkObject` + `NetworkRigidbody3D`, the `CauseDamage` tag and an item-data ScriptableObject (class, damage, icon).
- Items respawn at fixed spawn points on a timer, so the office never runs dry.
- Thrown items should register **who threw them**, so damage and eliminations can be credited.

### 5.4 Health, knockdown, get up
- **100 HP**, shown above your character and on your HUD. No health regen **(proposal)**. Health comes back from pickups only.
- **Hit:** damage is applied. If the impact is above the knockdown threshold, you also go into full ragdoll (the current `DetectCollision` logic, extended with damage).
- **Get up:** automatic after about 3 s. Tapping the Get Up button speeds it up (button mashing) **(proposal)**.
- **Brief invulnerability** (about 1 s) after getting up, so you can't be knocked down over and over.
- **Falling off the map / out of bounds:** take 30 damage (tunable) and respawn at a random point. Never an instant elimination.
- **0 HP:** eliminated. The character stays as a limp ragdoll, then disappears. The player switches to spectate camera.

### 5.5 Pickups / power-ups
Coffee (healing) is confirmed. The rest are proposals.
They spawn on a timer from the existing powerup block prefabs (recolored to neutral):
- **Coffee:** +30 HP
- **Energy drink:** speed + unlimited stamina for 8 s
- **Super Grip:** your next throw starts fully charged
- **Bubble Wrap:** blocks the next knockdown

### 5.6 Shrinking office (confirmed)
With 10 players, rounds need a push toward the end. After 90 s, sections of the office "close" (lights go off, sprinklers on, doors lock) and slowly deal damage to anyone inside, until only the center area is left.

## 6. Mobile-specific requirements

### Controls (touch)
```
 ┌───────────────────────────────────────────────┐
 │ HP ███████░░   Players left: 7    ⏱ 2:41      │
 │                                  [pocket1][pocket2]│
 │                                               │
 │   (  joystick  )                 [Grab/Drop]  │
 │                          [Jump]  [THROW L][THROW R]│
 └───────────────────────────────────────────────┘
```
- Left: floating virtual joystick for movement.
- Right: Jump, Grab/Drop, and one Throw button per hand. Holding Throw charges; dragging off the button aims the arc **(proposal)**.
- When knocked down, Jump turns into the **Get Up** button.
- Pocket slots are tappable icons at the top right.
- Light auto-aim toward the nearest player in the throw direction, because aiming with touch is hard **(proposal)**.
- Build this on the Unity Input System (on-screen stick/buttons) so gamepad support comes nearly for free later.

### Performance budget
10 active ragdolls (about 11 rigidbodies each), plus 40+ props, is heavy for phones. Targets:
- **30 fps minimum on mid-range phones** (e.g. 3–4 year old Android), 60 fps on recent iPhones
- Use the existing URP Performant renderer on mobile, baked lighting, low-poly office set
- Ragdoll LOD: remote players far away sync fewer joints, or use simpler colliders
- Cap the number of active physics props, and put them to sleep when idle
- Test early with 10 clients. This is the biggest technical risk

### Networking
- The current code is **client-hosted** (one phone acts as the host). With 10 mobile players, the host phone does all the physics and is a single point of failure (if it backgrounds or drops, the match dies).
- **Decided:** client-host for the prototype (Milestones 0–3), then a **Fusion dedicated server** (headless Linux build in the cloud) for launch. Supabase handles accounts and data; it doesn't run the match.
- Keep gameplay code authority-agnostic (`HasStateAuthority`, not "is a player") so the switch to a server is mostly a build/config change.
- Matchmaking: a Quick Match that joins any open session with fewer than 10 players, or creates one. A private room code for playing with friends.

### Store requirements
- Apple Developer account ($99/yr), Google Play Developer account ($25 one-time)
- App icons, screenshots, privacy policy, age rating (cartoon violence)
- iOS: handle the app being backgrounded mid-match (reconnect or forfeit)
- Ads and accounts mean extra store work: Apple App Tracking Transparency prompt, Google Play Data Safety form, account deletion inside the app (required by both stores)

### Backend: Supabase + Fusion
Supabase is the company-wide backend: **one account works in every game we release.**

| Supabase | Used for |
|---|---|
| Auth | Sign in with Apple, Google, email, plus guest accounts that can be upgraded later. Required: Sign in with Apple on iOS if any other social login is offered |
| Postgres (shared schema) | `profiles` (shared across games: username, avatar, level) |
| Postgres (per-game schema, e.g. `office_clash`) | owned cosmetics, equipped loadout, match history, stats (wins, eliminations, damage) |
| Edge Functions | Trusted writes: grant match rewards, process purchases (verify App Store / Play receipts), verify ad rewards |
| Row Level Security | Players can read their own data but never write currency, cosmetics or stats directly |

How they connect:
1. Player signs in to Supabase in the app and gets a JWT.
2. The app passes that JWT to Photon via **Fusion custom authentication**. A Supabase Edge Function validates it and returns the user ID, so every Fusion player is a verified Supabase user.
3. In the match, the host knows each player's Supabase user ID and equipped cosmetics.
4. At match end, the host (later: the dedicated server) sends the results to an Edge Function, which writes stats and rewards. A client can't award itself.

### Monetization: free + cosmetics + ads
- **Cosmetics:** outfits, ties, hats, glasses, mug skins, throw trails, victory emotes. Purely visual, no pay-to-win.
- **Currency:** soft currency earned by playing; premium currency or direct purchases through Unity IAP. Receipts verified in an Edge Function before granting items.
- **Ads:** rewarded ads (double match rewards, free daily cosmetic spin) **(proposal)**. Light interstitials between matches at most, never during a match.
- Possibly shared across games: an account-level currency or cosmetics usable in several of our games **(to decide later)**.

## 7. Characters
- Office worker models, rigged to match the current ragdoll joint setup (the same bone layout as the Knight, so `SyncPhysicsObject` keeps working).
- 10 distinct looks per match (shirt color, tie, hair, glasses), so players can tell each other apart in a free-for-all.
- Later: cosmetics (mugs, hats, lanyards), which could also be the monetization.

## 8. Levels
- **Open-plan office** (`Office Scene 1`): the MVP map. Cubicles, desks, lots of throwables, 10+ spawn points.
- **Rooftop:** edges to fall off.
- **Server room / break room:** small, chaotic maps for fewer players.

Each map needs: 10+ spawn points, item spawn points, pickup spawn points, an out-of-bounds kill volume, and the shrinking-zone sections.

## 9. UI / UX
- **Main menu:** Quick Match, Play with Friends (room code), Customize, Settings (GUI Kit Yellow Kid)
- **Lobby:** player count "6/10", countdown, starts automatically when full or after a timer
- **HUD:** HP, players left, timer, hand items + charge arc, pocket slots, get-up prompt
- **Kill feed:** "Rony knocked out Sam with a keyboard"
- **Spectate:** cycle between the remaining players
- **Results:** placement, eliminations, damage dealt, favorite item

## 10. Audio & juice
Hit sounds per item type, cartoony "oof" voice lines, camera shake on big hits, phone haptics on taking damage, slow-mo on the final elimination, paper and confetti particles.

## 11. Roadmap

### Milestone 0: Foundation (1–2 weeks)
- [x] Fix tech debt 1–4 (network knockdown state, `NetworkButtons`, spawn points, session handling) — done 2026-10-17. `SpawnPoint` components still need placing in the scene
- [ ] Move gameplay to `Office Scene 1` and add it to build settings
- [ ] Make the office props networked throwables with item data
- [ ] Get a first Android build running with 2 phones online
- [ ] Supabase project setup: shared `profiles` schema + `office_clash` schema, guest sign-in from Unity

### Milestone 1: Playable MVP (2–3 weeks)
- [ ] Networked HP + damage from item hits (with attacker credit)
- [ ] Self-recover get-up with invulnerability window
- [ ] Elimination + spectate + last-player-wins round flow
- [ ] Hands (L/R) + 2 pocket slots inventory
- [ ] Touch controls (joystick + buttons)
- [ ] Basic HUD (HP, players left, timer, items)
- [ ] Quick Match lobby for up to 10
- [ ] Fusion custom auth through a Supabase Edge Function

### Milestone 2: Fun + performance (2–3 weeks)
- [ ] **10-player stress test on real phones.** Profile and add ragdoll/prop LOD
- [ ] Sprint + stamina, dive/tackle
- [ ] Pickups (Coffee, Energy drink, Super Grip, Bubble Wrap)
- [ ] Shrinking office
- [ ] SFX, haptics, camera shake, kill feed
- [ ] Tune damage numbers and knockdown threshold through playtests

### Milestone 3: Content (3 weeks)
- [ ] Office worker character models + color variants
- [ ] Second map (Rooftop)
- [ ] Results screen with stats, private room codes
- [ ] Settings (sensitivity, graphics quality, sound)
- [ ] Sign in with Apple / Google, guest-account upgrade
- [ ] Match results + stats written to Supabase via Edge Function
- [ ] Cosmetics: data model, equip screen, loadout synced into matches

### Milestone 4: Launch prep
- [ ] Move from client-host to a Fusion dedicated server (headless Linux build, cloud hosting, `GameMode.Server`)
- [ ] iOS build, TestFlight + Play Console closed testing
- [ ] Reconnect / backgrounding handling
- [ ] Store listings, privacy policy, age rating
- [ ] Cosmetics shop + Unity IAP with receipt verification in Supabase
- [ ] Rewarded ads integration (ATT prompt on iOS, consent in the EU)
- [ ] In-app account deletion, analytics, crash reporting
- [ ] Soft launch in one region, then global release

## 12. Still open
- Should currency or cosmetics be shared across all of our games, or separate per game?
- Which ad network (Unity Ads, AdMob, LevelPlay)?
