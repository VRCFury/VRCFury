# Unity Compile Test Project

Only use this workflow when the user explicitly asks for a Unity compile test. Do not create or run the test project by default.

## Location

Use a disposable Unity host project under `test-compile/` in the repo root.

Recommended path:

- `<repo-root>/test-compile/unity-host`

This folder is for temporary local compile validation only. It should not be treated as source content for the package itself.

## Setup

When asked to create or refresh the test project:

1. Create `test-compile/unity-host/`.
2. Create at least these folders/files in the host project:
   - `Assets/`
   - `Packages/`
   - `ProjectSettings/`
3. Mirror a known-good VRChat Unity project's `Packages/` and `ProjectSettings/` into `test-compile/unity-host/`.
4. Ensure the host project's `Packages/manifest.json` references this package by local file path:
   - `"com.vrcfury.vrcfury": "file:<repo-root>/com.vrcfury.vrcfury"`

This host project exists only to compile the package in a realistic Unity environment with the needed VRChat dependencies present.

## Finding Required Paths

You must discover the needed local paths instead of hardcoding machine-specific values.

Needed paths:

- Repo root:
  - Use the current workspace root.
- Unity editor executable:
  - Check common Unity Hub install locations.
  - Confirm the version from the source Unity project's `ProjectSettings/ProjectVersion.txt`.
  - Use the matching editor version when available.
- Source Unity project to mirror from:
  - Use the project the user points you at.
  - Copy only the `Packages/` and `ProjectSettings/` folders needed to make the disposable host project compile.

Do not document or assume a specific user's filesystem layout in this file.

## Invocation

Use Unity batchmode against the disposable host project.

Example command:

```powershell
& "<unity-editor-path>" `
  -batchmode `
  -quit `
  -projectPath "<repo-root>/test-compile/unity-host" `
  -logFile "<repo-root>/test-compile/unity-host/compile.log"
```

After the run:

1. Read `test-compile/unity-host/compile.log`.
2. Check for compiler errors and the final batchmode exit status.
3. Report the important results back to the user.

## Escalation Requirement

You must request escalation before running Unity for this compile test.

Reason:

- Unity batchmode did not work reliably in the sandbox for this repo.
- The unrestricted run was required to get licensing/IPC and batch compilation working correctly.

Do not attempt to rely on the sandboxed Unity invocation for the real compile result. Request escalation for the Unity batchmode command.

## Scope Rule

Do not use this compile-test workflow unless the user specifically asks for it.

Examples that qualify:

- "test compile this"
- "run Unity compile"
- "verify this in the test project"

If the user did not explicitly ask for a Unity compile test, do not create `test-compile/`, do not run Unity, and do not request escalation for Unity.
