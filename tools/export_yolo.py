"""
Export a trained Ultralytics YOLO model (YOLOv8 / YOLOv9 / YOLO11) to ONNX for the
Quest app, and write the matching labels file.

    pip install ultralytics onnx onnxslim
    python export_yolo.py path/to/best.pt --imgsz 640 --out ./export

Then copy <name>.onnx and <name>_labels.txt into your Unity project
(e.g. Assets/QuestApp/Models/), convert the ONNX with the converter button on
SentisInferenceRunManager, and assign both to a Detection Model Profile.
"""

import argparse
import shutil
from pathlib import Path

from ultralytics import YOLO


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("weights", type=Path, help="Trained .pt file (e.g. runs/detect/train/weights/best.pt)")
    parser.add_argument("--imgsz", type=int, default=640, help="Square input size. Smaller (e.g. 320/416) runs faster on Quest.")
    parser.add_argument("--opset", type=int, default=15, help="ONNX opset. Unity Inference Engine supports up to 15.")
    parser.add_argument("--name", default=None, help="Output base name (default: the weights file's parent run name).")
    parser.add_argument("--out", type=Path, default=Path("export"))
    args = parser.parse_args()

    model = YOLO(str(args.weights))
    names = model.names  # {class_id: name}
    print(f"Model has {len(names)} classes: {', '.join(names[i] for i in sorted(names))}")

    # Raw head output (1, 4 + classes, N). NMS is NOT baked in here:
    # the Unity-side converter adds box decoding and the app runs NMS itself.
    onnx_path = Path(model.export(format="onnx", imgsz=args.imgsz, opset=args.opset,
                                  dynamic=False, simplify=True, nms=False))

    base = args.name or args.weights.resolve().parent.parent.name or args.weights.stem
    args.out.mkdir(parents=True, exist_ok=True)
    final_onnx = args.out / f"{base}.onnx"
    shutil.copy(onnx_path, final_onnx)

    labels_path = args.out / f"{base}_labels.txt"
    labels_path.write_text("\n".join(names[i] for i in sorted(names)) + "\n", encoding="utf-8")

    print(f"\nWrote {final_onnx}\nWrote {labels_path}")


if __name__ == "__main__":
    main()
