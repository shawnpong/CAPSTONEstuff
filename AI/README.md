# AI Module – README

This directory contains the full software and hardware implementation of the gesture-recognition AI deployed on the Ultra96-V2. The system performs real-time inference on IMU data streamed from a wearable glove using a highly optimized 1D CNN implemented in FPGA logic.

---

## Overview

The AI pipeline consists of the following major stages:

1. **IMU Data Acquisition**  
   Five fingertip IMUs stream accelerometer and gyroscope data (30 features/frame) via MQTT to the Ultra96.

2. **Sliding Window Formation**  
   Data is buffered into windows of **128 frames** (~2.56 s at ~50 Hz).

3. **Preprocessing**  
   Performed on the Ultra96 PS in Python:
   - Segment window into 8 equal temporal segments  
   - Extract summary statistics (mean, std, peak-to-peak, energy)  
   - Flatten into 960-element feature vector  
   - Apply StandardScaler normalization  
   - Apply PCA reduction to **34 components**

   The final PCA vector (34 floats) is streamed to the FPGA accelerator.

4. **Neural Network (FPGA)**  
   A compact 1D CNN is implemented using Vitis HLS:
   - Input (34)
   - Conv1D (kernel = 3, ReLU)
   - Flatten
   - Dense
   The Conv1D and Dense operations are fused into a single hardware module for low latency and reduced DSP usage.

5. **Post-Processing**  
Executed on the Ultra96 PS:
- Softmax  
- Argmax  
- 50% confidence threshold  
- Two-consecutive confirmation (debounce)  
- 3-second cooldown  
- Timestamping and latency measurement  
- MQTT publishing of confirmed gestures

---

## Training Workflow

1. Data collected through the full pipeline (glove → MQTT → Ultra96 → CSV).  
2. Data manually labeled using Excel heatmaps.  
3. Dataset windowing and class-0 augmentation.  
4. Training with TensorFlow/Keras using:
- Categorical cross-entropy  
- Adam optimizer  
- GaussianNoise regularization  
- StandardScaler + PCA (fitted on training data)
5. Export of Conv1D/Dense weights and preprocessing parameters (`.h` and `.npz`) for hardware deployment.

---

## FPGA Implementation

- Fused Conv1D-Dense network implemented in C++ with Vitis HLS  
- Weights stored in BRAM-backed ROM structures  
- AXI-Stream interface for data input  
- AXI DMA for high-throughput communication  
- Vivado integration and generation of `.xsa` overlay  
- PYNQ runtime for invoking the accelerator and handling data flow

This design enables real-time gesture inference with low latency and minimal FPGA resource usage.

---

## Key Features

- Custom FPGA CNN accelerator  
- Full real-time end-to-end pipeline  
- Low latency via AXI DMA  
- Robust post-processing (thresholding, debounce, cooldown)  
- PCA-based dimensionality reduction (113× compression)  
- Easily retrainable model architecture  
- Resource-efficient HLS implementation  


