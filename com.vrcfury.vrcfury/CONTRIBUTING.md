# Contributing to VRCFury

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

* Morghus
  * Created numerous setup prefabs
* GameGeek720
  * Added Toggle enhancements for transitions and the local player
* Raphiiko
  * Added global parameters for Toggles
* nullstalgia
  * Added option for Toggles to use momentary push buttons
* Ximmer-VR
  * Made Toggle sliders work with global parameters
* wholesomevr
  * Made Armature Link work with Dynamic Bone Contacts
* anatawa12, lyuma, anatawa12
  * Special thanks for continuing efforts to make VRCFury and Av3Emu more compatible with each other
* CobaltSpace
  * Ensured that gogoloco params are always global
* TayouVR
  * added Logging to Blendshape Optimizer
  * Added Channel Support for Haptic Socket and Plugs
