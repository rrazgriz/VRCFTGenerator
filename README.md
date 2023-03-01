# VRCFTGenerator

> Generate VRChat systems for animating facial blendshapes that use Direct Blend Trees, with a fixed amount of layers to drive an unlimited amount of blendshapes with complex parameter configurations.
> Licensed as MIT.

🛑 **Warning**: The configurations generated do *not* appear to be stable with Write Defaults OFF when added to animators with more complex systems. Please only use it with WD On, and note that this means the normalization is not necessary, but will not affect the workings of the system. You can leave the layers as WD On regardless of the other layers' WD settings.

Please see the VRCFT wiki for more information on [Blendshapes](https://github.com/benaclejames/VRCFaceTracking/wiki/Blend-Shapes-Setup) and [Parameter Setup](https://github.com/benaclejames/VRCFaceTracking/wiki/Parameters).

![M7HZqf71Ua](https://user-images.githubusercontent.com/47901762/182271879-e2adf52c-196f-4381-9848-98d6d03c82c7.png)

⚠**This tool is not intended to be plug-and-play**, I built it for my own reference and as a direct blendtree study, with help from Lyuma and Hai. I am releasing it as-is for others to reference, study, and maybe use. The code quality is not particularly good, and may contain bad practices, unused code, and be difficult to read.

### Changelog

- 2022-08-12: Improved code quality, removed redundant code, added setup verification (will not generate if duplicate params/shapes are present), won't create cast/decode layer if unneeded, won't create unnecessary decode entries, fixed Direct/Averaged Parameters
- 2022-08-18: Automatically add/remove parameters from VRChat SDK Parameters Object
- 2023-02-28: Change to WD ON, add support for implicit parameter casting (use [GestureManager](https://github.com/BlackStartx/VRC-Gesture-Manager) or [this Av3Emulator fork](https://github.com/jellejurre/Av3Emulator)), UI improvements, and more

### Improvements To Make

- Support Eye tracking parameters (cases are more complex)
- Casting layer currently transitions to self every frame, could be improved

## Usage

- Make a backup of your FX layer.
- Add the `VRCFTGenerator` folder to your unity project.
- Add the script `VRCFTGenerator.cs` to any object.
- Configure the script with:
  - Your Avatar's root gameobject
  - An asset container (generate a new Animation controller for this)
  - A Skinned Mesh Renderer for the blendshape animations to target
  - Configuration for Write Defaults and the desired smoothing parameter (`0.6` - `0.7` recommended)
  - The parameters you wish to control in the system, and their precision
- Click generate. The script should pop up a confirmation informing you of the parameter cost.
- Add the associated parameters to your Synced Parameters project. A `FaceTracking` bool is generated for easy toggling on and off; when off, all face tracking blendshapes and smoothed parameters will be driven to 0.

## Details

This tool uses direct blend trees to convert boolean-encoded decimals ([Binary Parameters](https://github.com/benaclejames/VRCFaceTracking/wiki/Parameters#binary-parameters)) to animator float parameters, drive and smooth animator float parameters, and drive blendshapes with the smoothed value. The generated system works as follows:

- A "BinaryCast" layer is created with a single state with a do-nothing animation. Using the VRC Parameter Driver state behavior's "Copy" behavior, all binary parameters are cast to a float parameter with their equivalent decimal value, and any "Negative" boolean flags are cast to floats for later use in blendtrees. This value is dependent on the selected precision of the parameter. This layer's state transitions to itself every frame, so the values are continuously updated.
- A "Decode" layer is created with a single direct blend tree, which does the following things:
  - Sum binary parameter float values into their final value
  - Decode **[Combined Parameters](https://github.com/benaclejames/VRCFaceTracking/wiki/Parameters#combined-lip-parameters)** into their constituent "raw" parameters (corresponding directly to SRAnipal Blendshapes)
  - Decode **Averaged Parameters** (parameters that drive two symmetric values to the same value, e.g. CheekPuffRight/CheekPuffLeft -> CheekPuff) into their constituent combined and raw parameters.
- A "Smoothing" layer is created with a direct blend tree and an "Off" state. The off state drives all blendshapes and smoothed parameters to 0. The direct blend tree does the following things:
  - Smooths the decoded parameters using discrete exponential smoothing
  - Drives blendshapes based on the smoothed values

~~There are a number of rules that have to be followed for these direct blend trees to work correctly with Write Defaults On or Off:~~

~~- Direct Blendtree Motions should not have an animation as a first-level child motion - another blend tree (1d, 2d, or direct) should be used instead.~~
~~- At the top level of the direct blendtree, a single normalizing parameter is used, with a value of `1/numberOfChildren` (for a 20-child blendtree, this would be `1/20 = 0.05`). This normalizes the blendtree's outputs correctly.~~
  ~~- In blendtrees that are not the top level direct blendtrees (even direct ones), this value needs to be 1.0 instead.~~
  ~~- Child motions can nest blendtrees as deep as is needed.~~
~~- Output animations (at the end of the blendtrees) need to have their keyframe values scaled by a factor of the number of children at the top level blendtree. For a 20-child blendtree, output animator parameters would need to be scaled to `20` instead of `1`, and blendshapes would need to be `2000` instead of `100`. Zero is unaffected.~~

~~When these rules are followed, direct blend trees appear to work as expected whether Write Defaults is on or off.~~ **Please note that this has been found to still interfere with other layers.**

## Extras

Thanks very much to:

- [Hai](https://github.com/hai-vr/) and [Lyuma](https://github.com/lyuma) for their immense expertise and help in figuring out a stable configuration for using direct blend trees.
- The [VRCFT](https://github.com/benaclejames/VRCFaceTracking) developers for their wonderful application.
- The [VRCFT Discord](https://discord.gg/Fh4FNehzKn) and for their encouragement to push this much further than I otherwise would've.
