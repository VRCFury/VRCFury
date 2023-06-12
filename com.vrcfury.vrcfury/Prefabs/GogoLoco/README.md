Gogo Loco Using VRCFury
==

## How to install
* Download and import [VRCFury](https://vrcfury.com/download)
* Import [Gogo Loco by franada](https://franadavrc.gumroad.com/l/gogoloco)
* Find the `Packages/VRCFury Prefabs/GogoLoco` folder in the unity folder browser
* Choose a version: All, Beyond, Broke, or Scale Only
  * `All` is the basic Gogo install
  * `Beyond` is All + Flying
  * `Broke` is Beyond + Scale, Horizon Adjust
  * `Scale only` contains ONLY scaling support
* Drag the applicable VRCFury prefab file from the folder onto the root of your avatar
* You're done! None of the additional Gogo Loco instructions are needed.

# Troubleshooting

* Avatar doesn't change scale when I change the scale value in the Broke menu?

Unfortunately, you must turn `Options -> OSC -> Enable` off and back on to make the scale change take effect. This is a limitation of the way the scaling prefab works.

* Avatar doesn't change scale after toggling OSC?

Ensure you are doing Build & Upload. Build & Test won't work because vrchat doesn't save parameters on test builds.
