[English](./doc/README.en.md)

## basic-pitch-dotnet

[basic-pitch](https://basicpitch.spotify.com/) 是 Spotify 的一个音频转 MIDI 的项目。</br>
它本身由 Python 开发，本项目为其 .net 移植版本。

## 使用方法

使用 nuget 引入：https://www.nuget.org/packages/BasicPitch/

代码层面如何使用可以参考 **ToMidiSample** 项目

## 技术基本原理

**basic-pitch** 本身由 3 个步骤来完成工作:

1. 使用 **nmp** 模型推理音频输入
2. 使用 **后处理** 算法处理 **nmp** 模型的输出
3. 将上一步的数据转换为 MIDI 文件

本项目也是遵循这个原理:

1. 使用 **nmp** 的 **onnx** 模型版本结合微软的 **Windows.AI.MachineLearning** 框架进行推理
2. 使用微软的 **System.Numerics.Tensors** 框架移植 Python 版中由 Numpy/scipy 实现的后处理算法
3. 使用 **NAudio.Midi** 生成 MIDI 文件

其中，第 1 步中输入到 **nmp** 模型的音频数据会和 Python 版本有一些不同:

- Python 版本使用到的 downmix 算法为求多声道平均，用到的重采样算法为 **soxr_hq**
- 本项目使用 **NAudio.MediaFoundationResampler** 来实现这两步，输出结果不能与 Python 版本严格一致，但是最终结果基本相同。