# PakDwi

## Overview

PakDwi is a biomechanical simulation and signal processing application designed for analyzing electromyography (EMG) and multi-sensor data from controlled joint systems. The application provides a comprehensive platform for simulating, visualizing, and analyzing sensor signals in the context of prosthetic limbs, exoskeletons, or robotic actuator systems.

This software implements advanced signal processing techniques, including real-time frequency domain analysis, control theory visualization, and EMG-driven actuator dynamics modeling, providing researchers and developers with a sophisticated toolset for biomechanical system analysis.

## Key Features

### Simulation Modes

- **Playground Mode**: Manual sensor control through interactive sliders for quasi-static analysis
- **Dynamic EMG Mode**: Full biomechanical actuator dynamics driven by simulated electromyography signals

### Multi-Sensor Simulation

The application simulates five distinct sensor systems:

1. **EMG (Electromyography)**: Envelope-modulated sinusoidal signals with configurable amplitude and cycle duration
2. **IMU (Inertial Measurement Unit)**: 6-axis orientation data including Roll, Pitch, and Yaw angles
3. **Force/Torque Sensor**: 3-axis force measurements (Fx, Fy, Fz)
4. **Encoder**: Angular position tracking for joint rotation
5. **Flex Sensor**: Bend angle measurement

### Signal Analysis and Visualization

- **Time-Domain Visualization**: Real-time waveform display for all sensor channels
- **Frequency-Domain Analysis**: 128-point Fast Fourier Transform (FFT) spectral analysis
- **Control Theory Planes**:
  - S-Plane (Laplace domain): Continuous-time pole/zero visualization
  - Z-Plane (Digital domain): Discrete-time pole/zero plot with unit circle reference
- **Radar Charts**: Polar visualization of IMU orientation angles
- **Data Terminal**: Real-time scrolling log displaying simulation state and measurements

### Actuator Dynamics

The application implements a second-order dynamic system model with the following characteristics:

- Natural frequency (ωₙ): 4.0 rad/s
- Damping ratio (ζ): 0.7
- Bilinear transform support for discrete-time control analysis
- Real-time and windowed processing modes

## Mathematical Model and Equations

This section presents the complete mathematical formulation of the biomechanical simulation model. The equations differ based on the selected simulation mode.

### Core Actuator Dynamics

The joint actuator is modeled as a **second-order linear system** governed by the following differential equation:

```
θ̈ + 2ζωₙθ̇ + ωₙ²θ = ωₙ²θd
```

Where:
- **θ** = joint angle (degrees)
- **θ̇** = joint angular velocity (deg/s)
- **θ̈** = joint angular acceleration (deg/s²)
- **θd** = desired joint angle (degrees)
- **ωₙ** = natural frequency = 4.0 rad/s
- **ζ** = damping ratio = 0.7

#### Numerical Integration (Euler Method)

```
error = θd - θ
accel = ωₙ² · error - 2ζωₙ · θ̇
θ̇(t+Δt) = θ̇(t) + accel · Δt
θ(t+Δt) = θ(t) + θ̇(t) · Δt
```

Where **Δt** = 0.02 s (50 Hz sampling rate)

### Sensor Models

#### 1. EMG (Electromyography) Sensor

The EMG signal is generated independently in both modes using an **envelope-modulated sinusoidal carrier**:

**Time-Domain Formula:**
```
EMG(t) = A · Env(t) · sin(2π · fc · t)
```

Where:
- **A** = (ampPercent / 100) × 5.0 mV
- **fc** = 80 Hz (carrier frequency)
- **Env(t)** = envelope function (pulsed activation pattern)

**Envelope Function:**
```
Tcycle = Tbase · (0.5 + EMGIntervalBar/10)
Ton = Tcycle / 3
phase = t mod Tcycle

Env(t) = { 1.0  if phase < Ton (active phase)
         { 0.0  otherwise (rest phase)
```

Parameters:
- **Tbase** = 3.0 s (base cycle duration)
- **EMGIntervalBar** = 0 to 25 (trackbar range)
- **Tcycle** = 1.5 to 9.0 s (resulting cycle range)

**Frequency-Domain:**
```
EMG(f) = |FFT[EMG(t)]|
```

#### 2. IMU (Inertial Measurement Unit) Sensor

**Dynamic EMG Mode:**

The IMU outputs are derived from the actuator joint angle θ:

```
Roll(t) = θ(t)
Pitch(t) = 0.5 · θ(t)
Yaw(t) = 0.2 · θ(t)
```

**Playground Mode:**

```
Roll(t) = IMU_XSlider value (quasi-static)
Pitch(t) = IMU_YSlider value (quasi-static)
Yaw(t) = IMU_ZSlider value (quasi-static)
```

**Magnitude Calculation:**
```
IMU_Mag = √(Roll² + Pitch² + Yaw²)
```

#### 3. Force/Torque Sensor

**Dynamic EMG Mode:**

Force is proportional to the tracking error between desired and actual joint angle:

```
Fx(t) = 0.0 N
Fy(t) = 0.0 N
Fz(t) = Kf · (θd - θ)
```

Where:
- **Kf** = 0.1 (force-error gain, N/degree)

**Playground Mode:**

```
Fx(t) = Force_FxSlider value (quasi-static)
Fy(t) = Force_FySlider value (quasi-static)
Fz(t) = Force_FzSlider value (quasi-static)
```

**Magnitude Calculation:**
```
F_Mag = √(Fx² + Fy² + Fz²)
```

#### 4. Encoder (Angular Position Sensor)

**Dynamic EMG Mode:**

```
Encoder(t) = θ(t) mod 360°
```

**Playground Mode:**

```
Encoder(t) = (θ₀ + ω · t) mod 360°
```

Where:
- **θ₀** = Enc_AngleSlider value (initial angle, degrees)
- **ω** = Enc_SpeedSlider value (angular velocity, deg/s)

#### 5. Flex Sensor

**Dynamic EMG Mode:**

```
Flex(t) = kflex · θ(t)
```

Where:
- **kflex** = 1.0 (scaling factor)

**Playground Mode:**

```
Flex(t) = Flex_BendSlider value (quasi-static)
```

### Desired Angle Generation (θd)

#### Playground Mode

```
θd(t) = θmax · sin(2π · fosc · t)
```

Where:
- **θmax** = Flex_BendSlider value (0 to 90 degrees)
- **fosc** = max(0.1, Flex_OscFreqSlider / 10) Hz

#### Dynamic EMG Mode

```
θd(t) = k · ampEMG · Env(t)
```

Where:
- **k** = 60° / 100% = 0.6 (scaling factor)
- **ampEMG** = EMG amplitude percentage (0-100%)
- **Env(t)** = EMG envelope function (same as EMG sensor)

### Control Theory Analysis

#### S-Plane (Laplace Domain)

**Transfer Function:**
```
G(s) = ωₙ² / (s² + 2ζωₙs + ωₙ²)
```

**Continuous-Time Poles:**
```
s₁,₂ = -ζωₙ ± j·ωₙ·√(1 - ζ²)
s₁,₂ = -2.8 ± j·2.857 rad/s
```

#### Z-Plane (Digital Domain)

**Discrete-Time Poles:**
```
z₁,₂ = exp(s₁,₂ · Ts)
```

Where:
- **Ts** = 0.02 s (sampling period)

**Bilinear Transformation:**
```
s = (2/Ts) · (z - 1)/(z + 1)
```

### Frequency Domain Analysis

**Fast Fourier Transform (FFT):**

Applied to all sensor signals with the following parameters:

```
FFT_Mag(f) = |FFT[Signal(t)]|
```

Parameters:
- **Window size**: 128 samples
- **Sampling frequency**: fs = 1/Δt = 50 Hz
- **Frequency resolution**: Δf = fs / N = 50/128 ≈ 0.39 Hz
- **FFT update rate**: 5 Hz (every 10 simulation ticks)
- **FFT library**: MathNet.Numerics (MATLAB-compatible normalization)

**Frequency Bins:**
```
f(i) = i · fs / N    for i = 0, 1, ..., N/2
```

### Radar Chart Visualization

**IMU Angle Mapping:**
```
θradar = (θ mod 360 + 360) mod 360
r = 100 (fixed radius)
```

Displays Roll, Pitch, and Yaw angles in polar coordinates.

### Simulation Parameters Summary

| Parameter | Symbol | Value | Unit |
|-----------|--------|-------|------|
| Sampling period | Δt | 0.02 | s |
| Sampling frequency | fs | 50 | Hz |
| Natural frequency | ωₙ | 4.0 | rad/s |
| Damping ratio | ζ | 0.7 | - |
| EMG carrier frequency | fc | 80 | Hz |
| FFT window size | N | 128 | samples |
| FFT update decimation | - | 10 | ticks |
| Force-error gain | Kf | 0.1 | N/deg |
| Windowed simulation duration | - | 70 | s |

## Technical Specifications

### Target Framework

- **.NET Framework 4.7.2**
- Windows Forms Application
- C# Programming Language

### Dependencies

The following NuGet packages are required:

| Package | Version | Purpose |
|---------|---------|---------|
| MathNet.Numerics | 5.0.0 | Advanced numerical computing and FFT operations |
| HelixToolkit.Wpf | 2.27.3 | 3D visualization toolkit for WPF components |
| System.ValueTuple | 4.4.0 | Tuple support for .NET Framework 4.7.2 |

### System Requirements

- **Operating System**: Windows 7 or later
- **Runtime**: .NET Framework 4.7.2 or higher
- **Processor**: Multi-core processor recommended for real-time processing
- **Memory**: Minimum 2 GB RAM

## Project Structure

```
PakDwi/
├── Program.cs                     # Application entry point
├── Form2.cs                       # Primary simulation interface (802 lines)
├── Form2.Designer.cs              # UI component definitions
├── Form2.resx                     # Form resources
├── Form1.cs                       # Legacy simulation implementation
├── Form1.Designer.cs              # Legacy UI definitions
├── Form1.resx                     # Legacy resources
├── PakDwi.csproj                  # Project configuration file
├── packages.config                # NuGet package manifest
├── App.config                     # Application configuration
├── Properties/                    # Assembly metadata and settings
├── bin/Debug/                     # Compiled output directory
├── obj/Debug/                     # Build artifacts
└── Resource/                      # Image and media assets
```

## Installation and Setup

### Prerequisites

1. Install [Visual Studio](https://visualstudio.microsoft.com/) (2017 or later)
2. Ensure .NET Framework 4.7.2 SDK is installed

### Building the Application

1. Clone the repository:
   ```
   git clone <repository-url>
   cd PakDwi
   ```

2. Restore NuGet packages:
   ```
   nuget restore PakDwi.sln
   ```

3. Build the solution:
   ```
   msbuild PakDwi.sln /p:Configuration=Debug
   ```

4. Run the application:
   ```
   bin\Debug\PakDwi.exe
   ```

Alternatively, open `PakDwi.slnx` in Visual Studio and build using the IDE.

## Usage

### Starting a Simulation

1. Launch the application to display the main simulation interface (Form2)
2. Select the desired simulation mode:
   - **Playground**: Use sliders to manually control sensor values
   - **EMG**: Enable dynamic EMG-driven actuator simulation

### Configuring EMG Parameters

When in EMG mode:
- Adjust **EMG Amplitude** to control signal magnitude
- Modify **Cycle Duration** to change the EMG signal period
- The actuator will respond to EMG signals according to the configured second-order dynamics

### Monitoring Analysis

- **Time Charts**: Observe real-time sensor waveforms
- **FFT Charts**: Analyze frequency content of signals
- **S/Z-Plane**: Examine system poles in continuous and discrete domains
- **Radar Chart**: Monitor IMU orientation in polar coordinates
- **Data Terminal**: Review detailed numerical data and simulation state

### Processing Modes

- **Real-time Mode**: Continuous simulation with live visualization updates
- **Windowed Mode**: Process 70 seconds of data with final visualization (optimized for performance)

## Signal Processing Architecture

### Processing Pipeline

1. **Signal Generation**: Mode-dependent generation (manual sliders or EMG-driven)
2. **Actuator Dynamics**: Second-order system computes joint angle from desired angle
3. **Sensor Simulation**: All sensors derive readings from current joint state
4. **Buffering**: Circular queues maintain FFT-window-sized data buffers
5. **Analysis**: FFT computation performed at 5 Hz (every 10 simulation ticks)
6. **Visualization**: Chart updates with configurable scrolling windows

### Performance Optimization

- Simulation timer operates at 50 Hz (20ms intervals)
- Heavy UI updates decimated by factor of 10 for efficiency
- Chart anti-aliasing disabled to improve rendering performance
- Windowed mode minimizes UI updates during batch processing

## Control Theory Implementation

The application implements classical control theory concepts:

- **Continuous-time poles**: s = -ζωₙ ± jωₙ√(1-ζ²)
- **Discrete-time conversion**: z = exp(s·Ts)
- **Bilinear transformation**: Analog-to-digital filter design support
- **Stability analysis**: Visual pole placement relative to stability boundaries

## Development Notes

The codebase contains two forms:
- **Form1**: Legacy implementation (mostly commented out, ~1265 lines)
- **Form2**: Current primary interface (active, optimized, 802 lines)

The application entry point (`Program.cs`) launches Form2 as the main window.

## License

Please refer to the repository for licensing information.

## Contributing

For contributions, bug reports, or feature requests, please contact the repository maintainers.

## Acknowledgments

This application utilizes the following open-source libraries:
- **MathNet.Numerics** for mathematical computations
- **HelixToolkit.Wpf** for 3D visualization capabilities

---

**Note**: This application is intended for research and educational purposes in the field of biomechanical engineering and prosthetic device development.