# MediaPipe Selfie Multiclass (256×256)

Source: https://storage.googleapis.com/mediapipe-models/image_segmenter/selfie_multiclass_256x256/float32/latest/selfie_multiclass_256x256.tflite
Model card: https://ai.google.dev/edge/mediapipe/solutions/vision/image_segmenter (Apache License 2.0).

Classes: 0 background, 1 hair, 2 body skin, 3 face skin, 4 clothes, 5 others (accessories).

The app does not ship this float32 file: `AvatarScreenBuilder` writes a float16 copy
(`selfie_multiclass_fp16.sentis`) next to it, and only that copy is referenced by the
avatar screen. Used on the phone to cut the child's head out of a photo; nothing is sent anywhere.
