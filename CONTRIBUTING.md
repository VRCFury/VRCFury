# Contributing to VRCFury

## READ THIS FIRST

**I do not frequently accept pull requests**. I often find that reviewing changesets is more difficult than
simply rewriting the entire feature from scratch myself. To ensure your work does not go to waste, I would
highly suggest joining the discord and discussing your idea with us before proceeding.

## How to make code changes

* Review the important `License Requirements` section below.
* Fork this repository, and checkout into a new directory (outside your unity project).
* If you installed using the VCC, remove the package using the VCC.
* If you installed using the installer, remove the package from unity's Package Manager "In Project" section.
* In unity's Package Manager tab, click +, `Add Package From Disk`, then select the com.vrcfury.vrcfury/package.json file from this repo.
* Make changes using either Jetbrains Rider, VSCode, or a text editor (if you're brave).
* Commit, and submit a Pull request with your changes.
  * Don't forget to include the snippet described in the `License Requirements`.
  * Don't forget to add your name to the bottom of this page if you feel your change was substantial!
* When you're finished, you can return to the main version by removing your package from the Package Manager tab, then reinstalling normally using your preferred method.

## License Requirements

For source code contributions to be accepted, you must have full, 100% ownership of the contribution, and you must release your contribution into the public domain. This is to ensure that the license may be adjusted in the future if needed, to prevent unintended commercial, illegal, or immoral use. Pasting `The Unlicense` into your merge request is sufficient for this purpose.

```
The changes made in this contribution are free and unencumbered software
released into the public domain.

Anyone is free to copy, modify, publish, use, compile, sell, or
distribute this software, either in source code form or as a compiled
binary, for any purpose, commercial or non-commercial, and by any
means.

In jurisdictions that recognize copyright laws, the author or authors
of this software dedicate any and all copyright interest in the
software to the public domain. We make this dedication for the benefit
of the public at large and to the detriment of our heirs and
successors. We intend this dedication to be an overt act of
relinquishment in perpetuity of all present and future rights to this
software under copyright law.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
IN NO EVENT SHALL THE AUTHORS BE LIABLE FOR ANY CLAIM, DAMAGES OR
OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE,
ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
OTHER DEALINGS IN THE SOFTWARE.

For more information, please refer to <https://unlicense.org>
```

## Special Thanks to the contributors!

* anatawa12
  * Improvements to VRCFury/Av3Emu compatibility
* babo4d
  * Added bone radius to SPS autorig physbone
* CobaltSpace
  * Ensured that gogoloco params are always global
* GameGeek720
  * Added Toggle enhancements for transitions and the local player
  * Fixed Toggle sliders always adding a menu option, even if the menu path was empty
  * Made toggles usable by specifying only a transition in and no main clip
  * Made toggles support driving multiple parameters
  * Made toggles not add a synchronized parameter when using vrchat built-in parameters
  * Fixed toggles possibly being in the wrong state for one frame upon loading
  * Added ability for toggles to "play to completion"
  * Added transition time support to Toggles
  * Added popup warning when you're in android mode but don't have any valid mats
  * Provided first version of reorderable menu items (unused)
* Happyrobot33
  * Added support for excluded global params in Full Controller
* KaelanDuck
  * Removed default vrchat additive layer, which resolves many cases of the 3x blendshape unity bug
  * Made colliders merge properly within Armature Link (unused)
* lyuma
  * Improvements to VRCFury/Av3Emu compatibility
* Morghus
  * Created numerous setup prefabs
  * Fix DirectTreeOptimizer for clips with partial keyframes
  * Added parameter smoothing to Full Controller
  * Debugged and found the solution to VRC stations breaking smoothing
  * Added smoothing of negative values
* nullstalgia
  * Added option for Toggles to use momentary push buttons
  * Prevented SPS toggle from disabling SPS autorig physbone
  * Set the Editor Test Copy's Animator controller to the generated FX layer
  * Added drop-in box for quickly setting bones in Armature Link
* Raphiiko
  * Added global parameters for Toggles
* sentfromspacevr
  * Added pointer-based float syncing
* TayouVR
  * Added Logging to Blendshape Optimizer
  * Improved log outputs from exception handling
  * Contributed SPS support for legacy DPS channel 1 (unused)
  * Added EditorOnly handling and fixed Builder Crash with ForceObjectState Delete
  * Added version number to vrcfury components
  * Checking for transform path on specified bone in Armature Link
* TheLastRar
  * Contributed attempts to fix light slot 4 breakage for DPS tip lights (unused)
  * Added scaling of legacy DPS tip light intensity
  * Add option for using local space for socket units
  * Add option for exact matching in BlendShapeLink
  * Allow configuring whether ring sockets are bidirectional
* Toys0125
  * Added Poiyomi UV Tile action type
* wholesomevr
  * Made Armature Link work with Dynamic Bone Contacts
  * Made plug length and width available as global parameters for sockets
  * Added public api for SPS Socket creation
* Ximmer-VR
  * Made Toggle sliders work with global parameters
* ZornTaov
  * Made the 2Axis Puppet feature