ThatFatKidsStuff Avatar Scale Using VRCFury
==

## How to install
* **IMPORTANT**: Please consider using [VRCFury "GogoLoco Scale Only"](https://vrcfury.com/gogoloco) instead for a more updated scaling experience (although this one still works)
* Download and import [VRCFury](https://vrcfury.com/download)
* Import [Avatar Scale by ThatFatKidsStuff](https://thatfatkidsmom.gumroad.com/l/dbezuo)
* Find the `Packages/VRCFury Prefabs/ThatFatKidsStuff/TFKS Avatar Scaling (VRCFury)` file in the unity folder browser
* Drag the file onto the root object of your avatar
* You're done! None of the additional install instructions are needed. Upload!

# Troubleshooting

* Avatar scale slider stays at 0% in the menu all the time?

Ensure vrcfury and the avatar scale prefab are both updated to the latest version

* Avatar doesn't change scale when I change the value in the menu?

Unfortunately, you must turn `Options -> OSC -> Enable` off and back on to make the scale change take effect. This is a limitation of the way the scaling prefab works.

* Avatar doesn't change scale after toggling OSC?

Ensure you are doing Build & Upload. Build & Test won't work because vrchat doesn't save parameters on test builds.
