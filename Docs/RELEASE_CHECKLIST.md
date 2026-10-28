# Shadow Steps — release checklist (Android first, iOS second)

Nothing on this list is done until a human checks it on a real device. Items marked ☐ require a Unity session or credentials.

## Configuration (applied in ProjectSettings and `BuildMenu.ApplyAndroidPlayerSettings`)
- [x] Product name "Shadow Steps", company "Triple Tap Games", bundle id `com.tripletapgames.shadowsteps` (Android/iOS)
- [x] Portrait only (`defaultScreenOrientation: 0`)
- [x] IL2CPP + ARM64 for Android; IL2CPP ARM64 for iOS (set at build time)
- [x] Min SDK 22, target SDK auto (highest installed); no INTERNET permission forced
- [x] Scenes: Bootstrap → Home → Game
- ☐ App icon: assign `Assets/_ShadowSteps/Art/AppIcon_placeholder_512.png` (or the final icon) in Player Settings ▸ Icon; adaptive icon foreground/background for Android
- ☐ Splash: Unity default is acceptable for personal/plus licences; customise if required

## Build
- ☐ Close the Unity Editor, then `Tools/build_android.sh apk` for a development APK, or export `SS_KEYSTORE_PATH`, `SS_KEYSTORE_PASS`, `SS_KEY_ALIAS`, `SS_KEY_PASS` and run `Tools/build_android.sh aab` for a Play-uploadable bundle. Output in `Builds/`.
- ☐ Create the upload keystore once with `keytool -genkeypair -v -keystore shadowsteps-upload.jks -alias upload -keyalg RSA -keysize 2048 -validity 10000`; keep it outside the repo and back it up.
- ☐ iOS: Shadow Steps ▸ Build ▸ iOS Xcode project, open `Builds/iOS` in Xcode, set the team, archive, validate. No privacy usage descriptions are needed (no camera/mic/location/tracking SDKs).

## Automated checks before any build
- [x] `Tools/run_core_tests.sh` → 191 EditMode tests green under dotnet
- [x] `python3 -I Tools/unity_compile_check.py` → 0 errors
- ☐ `Tools/run_unity_tests.sh All` (editor closed) → EditMode + PlayMode green inside Unity
- ☐ Console clean when pressing Play in Bootstrap.unity

## Device QA (record results in Docs/PROGRESS.md)
- ☐ Fresh install: Bootstrap → Home → 101 plays; onboarding card shows once
- ☐ Update install over previous version keeps progress (save version 1)
- ☐ Corrupt save: replace `shadowsteps_save.json` with garbage → game starts with defaults, `.corrupt` copy present
- ☐ Suspend/resume mid-level: no accidental move on resume; music resumes per setting
- ☐ Fast tapping and swipe spam: one action per gesture, no double turns
- ☐ Airplane mode: everything works (offline only)
- ☐ Portrait lock, safe area on notch devices, HUD readable at 1080×1920 and 1170×2532
- ☐ All 25 levels winnable on device without debug keys
- ☐ Audio toggles, haptics toggle, reduced motion, high-contrast cones persist across restarts

## Store
- ☐ Replace placeholders in Docs/Store, publish a privacy policy, fill Play data-safety form (no data collected)
- ☐ Internal testing track upload, tester feedback, then production — human approval required
