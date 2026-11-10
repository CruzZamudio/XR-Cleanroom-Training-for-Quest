# Quest 3 Step Guide with Switchable Object Detection

A Meta Quest 3 mixed-reality app built on Meta's **Passthrough Camera API Samples**. It adds two things:

1. **Switchable detection model.** The user picks the stock COCO YOLOv9 (80 everyday classes) or your own trained YOLO from an in-headset menu. Switching happens live, and the choice is remembered between sessions.
2. **Click-through step panel.** A floating instruction panel with title, text, optional image, progress, and Back / Next / Finish / Restart. The steps are entirely user-driven and not tied to detection.

## What's in this repo

This is a complete Unity project. It contains only the passthrough camera and object detection parts of Meta's [Passthrough Camera API Samples](https://github.com/oculus-samples/Unity-PassthroughCameraApiSamples) (MIT licensed, see `Assets/PassthroughCameraApiSamples/LICENSE.txt`). Meta's other demos and its start menu were removed, so the app opens straight into the detection scene.

```
Assets/
  PassthroughCameraApiSamples/
    PassthroughCamera/                      Meta's camera access + permission request
    MultiObjectDetection/                   Meta's detection scene, YOLO model and inference
      SentisInference/Scripts/SentisInferenceRunManager.cs   ← changed: adds model switching
      SentisInference/Editor/SentisModelEditorConverter.cs   ← changed: converts any class count, no overwrite
  QuestApp/
    Models/COCO.asset                            profile for the stock COCO model
    Scripts/Detection/DetectionModelProfile.cs   one asset per selectable model
    Scripts/Detection/ModelSelectorPanel.cs      in-headset model picker
    Scripts/Steps/StepSequence.cs                the list of steps (Inspector or JSON)
    Scripts/Steps/StepPanelController.cs         drives the step panel UI
    Scripts/Steps/PanelLazyFollow.cs             keeps panels comfortably in view
    Steps/example_steps.json                     sample steps in JSON form
tools/
  export_yolo.py                                 exports your trained YOLO to ONNX + labels
```

## Requirements

- Quest 3 or 3S on Horizon OS v74 or later.
- Unity **6000.0.66f2** (the version this project was saved with) with Android Build Support.
- Packages are listed in `Packages/manifest.json` and download automatically the first time you open the project.
- For pointing and clicking on panels: **Meta XR Interaction SDK** (`com.meta.xr.sdk.interaction.ovr`). It handles both controllers and hands.

## 1. Open the project and build it once

1. Clone this repo and open the folder in Unity Hub (*Add → Add project from disk*). The first open takes a while because Unity downloads packages and builds its `Library/` cache.
2. Turn on Developer Mode for your Quest in the Meta Horizon phone app. Connect the headset over USB-C and accept *Allow USB debugging* in the headset.
3. *File → Build Profiles*: switch to **Android**, pick your Quest under *Run Device*, and press **Build and Run**. The only scene in the build is `MultiObjectDetection`.
4. On first launch the app asks for camera and spatial data permission. Accept both.

Do this once before changing anything. It confirms that camera permission, the build pipeline and the stock model all work.

## 2. Create the model profiles

Each selectable model is a **Detection Model Profile** asset. To create one, go to *Assets → Create → Quest App → Detection Model Profile*.

**COCO (stock)**

| Field | Value |
|---|---|
| Display Name | `COCO (80 everyday objects)` |
| Sentis Model | `MultiObjectDetection/SentisInference/Model/yolov9sentis.sentis` |
| Labels | `MultiObjectDetection/SentisInference/Model/SentisYoloClasses.txt` |

**Your custom YOLO**

1. Export it. This works for Ultralytics YOLOv8, YOLOv9 and YOLO11.
   ```bash
   pip install ultralytics onnx onnxslim
   python tools/export_yolo.py runs/detect/train/weights/best.pt --name my_parts --imgsz 640
   ```
   This writes `my_parts.onnx` and `my_parts_labels.txt`. The labels come straight from the model, so the class order always matches.
2. Copy both files into e.g. `Assets/QuestApp/Models/`.
3. Select the **SentisInferenceManagerPrefab** and find **Convert ONNX to Sentis** at the bottom of its Inspector. Drag `my_parts.onnx` into *Onnx Model*, set *Converted Model Name* to `my_parts`, and press **Convert ONNX → Sentis**. You get `my_parts.sentis` next to the ONNX.
4. Make a profile that uses `my_parts.sentis` and `my_parts_labels.txt`.

> **Model format:** the converter expects the Ultralytics output layout `(1, 4 + classes, N)`. YOLOv5 and YOLOv7 use a different layout and won't convert correctly. Retrain or export with YOLOv8, YOLOv9 or YOLO11.

## 3. Hook up the inference manager

Select **SentisInferenceManagerPrefab** in the scene. The old single *Sentis Model / Labels* fields are gone and replaced by:

- **Model Profiles**: add the COCO profile and your custom profile. Element 0 is the default.
- **Default Profile Index**: which model loads first.
- **Remember Last Choice**: reopens on whatever the user picked last.

Tune the IoU and score thresholds per model on each profile. A custom model often needs a different score threshold than COCO.

## 4. Build the model picker panel

1. Create a **Canvas** (Render Mode: World Space, scale ≈ `0.001`). Inside it, add:
   - a TMP text for the active model name, plus an optional second one for its description
   - an empty object with a **Vertical Layout Group**, used as the button container
   - one **Button – TextMeshPro** inside the container, which acts as the template (the script disables it and clones it)
   - an optional "Loading…" object
2. Add **ModelSelectorPanel** to the canvas and assign the run manager and the UI pieces above.
3. Optional: set **Cycle Button** (for example `Button.Two` / B) to switch models without pointing.

When a switch starts, the panel greys out its buttons and shows the loading state. The first inference on a new model takes a moment, and the app does that warm-up during the switch instead of mid-task.

## 5. Build the step panel

1. Create your steps in one of two ways:
   - **In Unity:** *Assets → Create → Quest App → Step Sequence*. Fill in title, body and an optional image per step.
   - **From JSON:** put a file shaped like `example_steps.json` on the sequence's *Json Source*. JSON images are paths under a `Resources/` folder without the extension (e.g. `"Steps/wrench"`).
2. Create another world-space Canvas with:
   - TMP texts for the sequence title, step title, body and "Step X of Y"
   - an optional Image for the step picture
   - an optional Image (Type: Filled, Horizontal) as a progress bar
   - Back, Next and Restart buttons, plus a reference to Next's label so it can read "Finish"
3. Add **StepPanelController** and assign everything, including the Step Sequence.
4. Add **PanelLazyFollow** to the canvas root (and to the model picker if you like). The panel stays still while you work and drifts back into view when you turn away. The default places it slightly right and below eye level so it doesn't cover what you're looking at.

To react elsewhere in the app, use the controller's events: `OnStepChanged(int)` and `OnSequenceCompleted`. Call `LoadSequence()` to switch procedures at runtime.

## 6. Make the panels clickable (controllers + hands)

1. Install **Meta XR Interaction SDK** from the Package Manager.
2. In the scene, add the Interaction SDK rig to your camera rig. The quickest way is *Meta XR Building Blocks*: add **Ray Interaction**, and **Poke Interaction** too if you want to tap with a finger.
3. For each canvas, right-click it and choose *Interaction SDK → Add Ray Interaction to Canvas* (and *Add Poke Interaction to Canvas* if using poke). This makes standard Unity UI buttons respond to controller rays, hand rays and finger pokes.

## 7. Build and run

- Make sure the camera permission (`horizonos.permission.HEADSET_CAMERA`) is still in the manifest, which the sample already sets up.
- On first launch the sample asks for camera permission and shows its start panel, where you press A to begin. Detection stays paused until then; the step panel works regardless.

## Performance tips

- **Smaller input = faster.** Exporting your model at `--imgsz 416` or `320` can noticeably raise the detection rate on Quest 3, with some loss on small or distant objects.
- **Prefer small model variants** (n or s sizes) for on-headset use.
- Compare **CPU vs GPUCompute** per model with the *Backend* field on the profile and measure on the device. The editor isn't representative.

## Troubleshooting

| Symptom | Fix |
|---|---|
| Console says the model has 1 output, expected 3 | You assigned the raw `.onnx`. Convert it and assign the `.sentis`. |
| Labels are wrong or shuffled | The labels file order doesn't match training. Regenerate it with `export_yolo.py`. |
| Warning about class IDs beyond labels | The labels file has fewer lines than the model has classes. |
| Boxes everywhere or none at all | Adjust the profile's Score Threshold (raise it to reduce false positives). |
| Buttons don't respond | The canvas is missing the Interaction SDK ray or poke setup (step 6). |
