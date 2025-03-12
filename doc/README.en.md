> The content in this document was translated by AI.

## basic-pitch-dotnet

[basic-pitch](https://basicpitch.spotify.com/) is a project by Spotify that converts audio into MIDI.
It is developed in Python itself, and this project is its ported version to .net.

## How to use

Introduce it using NuGet: https://www.nuget.org/packages/BasicPitch/

You can refer to the **ToMidiSample** project for how to use it at the code level.

## Basic principles of technology

**basic-pitch** itself completes its work in 3 steps:

1. Use the **nmp** model to infer the audio input.
2. Use the **postprocessing** algorithm to process the output of the **nmp** model.
3. Convert the data from the previous step into a MIDI file.

This project also follows this principle:

1. Use the **onnx** model version of **nmp** in conjunction with Microsoft's **Windows.AI.MachineLearning** framework for inference.
2. Port the post-processing algorithms implemented in Python by Numpy/scipy using Microsoft's **System.Numerics.Tensors** framework.
3. Use **NAudio.Midi** to generate MIDI files.

Among them, the audio data input to the **nmp** model in the first step will be slightly different in the Python version:

- The downmix algorithm used in the Python version is to calculate the average of multiple channels, and the resampling algorithm used is **soxr_hq**.
- This project uses **NAudio.MediaFoundationResampler** to implement these two steps. The output results may not be exactly the same as those of the Python version, but the final results are basically the same.