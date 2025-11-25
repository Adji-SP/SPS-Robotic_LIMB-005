using System;
using System.Collections.Generic;
using System.Drawing; // Required for Color
using System.Linq;
using System.Numerics; // Required for Complex numbers
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using MathNet.Numerics;
using MathNet.Numerics.IntegralTransforms;
using Series = System.Windows.Forms.DataVisualization.Charting.Series; // Required for Fourier

namespace PakDwi
{
    public partial class Form2 : Form
    {
        // --- Global State ---
        private Timer _simTimer;
        private double _t = 0.0;
        private const double _dt = 0.02; // OPTIMIZATION: 50Hz is sufficient for smooth UI
        private const double WINDOW_DURATION = 70.0;

        // === STEP 1: Dynamic Actuator State Variables ===
        private double _theta = 0.0;           // joint angle (deg)
        private double _thetaDot = 0.0;        // joint angular velocity (deg/s)
        private double _thetaDesired = 0.0;    // desired joint angle (deg)

        // Actuator parameters (fixed)
        private const double ACT_WN = 4.0;     // natural frequency (rad/s)
        private const double ACT_ZETA = 0.7;   // damping ratio

        // === TWO-LEVEL MODE SYSTEM ===
        private enum SimulationMode { Playground, DynamicEmg }
        private SimulationMode _simMode = SimulationMode.Playground;

        private enum MotionMode { Realtime, Windowed }
        private MotionMode _currentMode = MotionMode.Realtime;

        // Counters & Buffers
        private int _tickCounter = 0;
        private const int FFT_DECIMATION = 10; // OPTIMIZATION: Heavy UI runs every 10 ticks (5Hz)
        private const int TABLE_MAX_ROWS = 329;
        private int _logRowCount = 0;

        private const int _fftWindowSize = 128;
        private Queue<double> _emgBuffer = new Queue<double>();
        private Queue<double> _imuMagBuffer = new Queue<double>();
        private Queue<double> _forceMagBuffer = new Queue<double>();
        private Queue<double> _encBuffer = new Queue<double>();
        private Queue<double> _flexBuffer = new Queue<double>();

        public Form2()
        {
            InitializeComponent();
            InitSimulator();
        }

        // ---------------------------------------------------------
        // 1. Initialization
        // ---------------------------------------------------------
        private void InitSimulator()
        {
            _simTimer = new Timer();
            _simTimer.Interval = (int)(_dt * 1000);
            _simTimer.Tick += SimTimer_Tick;

            // === Setup Simulation Mode Combo (Top-Left Panel) ===
            SimuBox.Items.Clear();
            SimuBox.Items.Add("Playground");
            SimuBox.Items.Add("Dynamic EMG");
            SimuBox.SelectedIndex = 0; // Default: Playground
            SimuBox.SelectedIndexChanged += SimulationModeCombo_SelectedIndexChanged;

            // === Setup Motion Mode Combo (Middle Panel) ===
            comboMode.Items.Clear();
            comboMode.Items.Add("Realtime");
            comboMode.Items.Add("Windowed (0-70s)");
            comboMode.SelectedIndex = 0;

            // === Setup EMG Interval Bar (Cycle Duration Control) ===
            // Range 0..25 → factor 0.5..3.0 → cycle 1.5s..9s
            EMGIntervalBar.Minimum = 0;
            EMGIntervalBar.Maximum = 25;
            EMGIntervalBar.Value = 10; // Default: factor=1.5, cycle=4.5s
            EMGIntervalBar.TickFrequency = 5;

            // OPTIMIZED CHARTS: No AntiAliasing, No Grids
            SetupChart(EMGTChart, "Time (s)", "mV");
            SetupChart(ENC_T_Chart, "Time (s)", "Deg");
            SetupChart(Flex_T_Domain, "Time (s)", "Deg");
            SetupMultiChart(IMU_T_Chart, "Time (s)", "Deg", new[] { "Roll", "Pitch", "Yaw" });
            SetupMultiChart(Force_T_Chart, "Time (s)", "N", new[] { "Fx", "Fy", "Fz" });

            SetupChart(EMG_Frequency_Chart, "Hz", "Mag", SeriesChartType.Area);
            SetupChart(IMU_Frequency_Chart, "Hz", "Mag", SeriesChartType.Area);
            SetupChart(Force_Frequency_Chart, "Hz", "Mag", SeriesChartType.Area);
            SetupChart(ENC_Frequency_Chart, "Hz", "Mag", SeriesChartType.Area);
            SetupChart(Flex_Frequency_Chart, "Hz", "Mag", SeriesChartType.Area);

            SetupFixedScatterChart(LaplaceDomainChart, "Real", "Imag", "S-Plane");
            SetupFixedScatterChart(ZdomainChart, "Real", "Imag", "Z-Plane");
            DrawUnitCircle(ZdomainChart);

            SetupRadarChart(RadarXImu, "Roll");
            SetupRadarChart(RadarYImu, "Pitch");
            SetupRadarChart(RadarZImu, "Yaw");

            tableData.GrowStyle = TableLayoutPanelGrowStyle.AddRows;
            tableData.AutoScroll = true;
            tableData.RowStyles.Clear();

            // === Button Event Handlers ===
            ResetSim.Click += ResetSim_Click;
            ResetPlayGround.Click += ResetSim_Click;
            ModeApply.Click += ModeApply_Click;
            SimApply.Click += SimApply_Click; // Apply Simulation Mode

            EMGGenerate.Click += (s, e) => HandleGenerateClick();
            IMU_GenerateBtn.Click += (s, e) => HandleGenerateClick();
            Force_ApplyBtn.Click += (s, e) => HandleGenerateClick();
            button10.Click += (s, e) => HandleGenerateClick();
            Enc_ApplyBtn.Click += (s, e) => HandleGenerateClick();
            button2.Click += (s, e) => UpdatePoles();

            StartBtn.Click += (s, e) => { };
            StopBtn.Click += (s, e) => { };

            UpdateFormulas();
            UpdatePoles(); // Initialize S/Z plane on startup
            ApplySimulationModeUI(); // Initialize UI state based on mode
        }

        // ---------------------------------------------------------
        // 2. Optimized Logic Engine
        // ---------------------------------------------------------

        private void HandleGenerateClick()
        {
            if (_currentMode == MotionMode.Realtime)
            {
                if (!_simTimer.Enabled)
                {
                    ResetState(true);
                    _simTimer.Start();
                }
            }
            else
            {
                GenerateWindowed();
            }
        }

        private void SimTimer_Tick(object sender, EventArgs e)
        {
            if (_currentMode != MotionMode.Realtime) return;

            _t += _dt;
            _tickCounter++;

            // Realtime: Do heavy UI updates periodically
            ProcessFrame(_t, true);

            double window = 10.0;
            ScrollChart(EMGTChart, _t, window);
            ScrollChart(IMU_T_Chart, _t, window);
            ScrollChart(Force_T_Chart, _t, window);
            ScrollChart(ENC_T_Chart, _t, window);
            ScrollChart(Flex_T_Domain, _t, window);
        }

        private void GenerateWindowed()
        {
            if (_currentMode != MotionMode.Windowed) return;

            _simTimer.Stop();
            ResetState(true);

            // OPTIMIZATION: Fast loop - Skip Heavy UI updates!
            for (double t = 0.0; t <= WINDOW_DURATION; t += _dt)
            {
                _t = t;
                _tickCounter++;
                ProcessFrame(_t, false); // false = Only plot time domain points
            }

            // Final update to show FFT/Table for the last state
            ProcessFrame(_t, true);
        }

        // === STEP 4: MODIFIED ProcessFrame - CORE PROCESSING FUNCTION ===
        private void ProcessFrame(double t, bool doHeavyUI)
        {
            // 1. EMG always generated (for FFT, etc.)
            double emg = GenerateEmg(t);

            // === FIX #3: Branch based on Simulation Mode ===
            (double Roll, double Pitch, double Yaw) imu;
            (double Fx, double Fy, double Fz) force;
            double enc;
            double flex;

            if (_simMode == SimulationMode.Playground)
            {
                // ========================================
                // PLAYGROUND MODE: NO DYNAMIC ACTUATOR
                // Sensors read directly from sliders (quasi-static)
                // Only change when user moves sliders
                // ========================================

                imu = GenerateImuPlayground();
                force = GenerateForcePlayground();
                enc = GenerateEncoderPlayground(t);
                flex = GenerateFlexPlayground();

                // For consistent logging, set theta to flex slider value
                _thetaDesired = flex;
                _theta = flex;
                _thetaDot = 0.0;
            }
            else // SimulationMode.DynamicEmg
            {
                // ========================================
                // DYNAMIC EMG MODE: FULL ACTUATOR MODEL
                // EMG → θ_d → Actuator (2nd order) → θ → All sensors
                // ========================================

                // Update actuator state (joint angle dynamics)
                UpdateActuator(t);

                // Sensors depending on _theta (from actuator dynamics)
                imu = GenerateImuFromTheta(_theta);
                force = GenerateForceFromError(_thetaDesired, _theta);
                enc = GenerateEncoderFromTheta(_theta);
                flex = GenerateFlexFromTheta(_theta);
            }

            // 2. Time Plots (Always update)
            AddPoint(EMGTChart, t, emg);
            AddMultiPoint(IMU_T_Chart, t, imu.Roll, imu.Pitch, imu.Yaw);
            AddMultiPoint(Force_T_Chart, t, force.Fx, force.Fy, force.Fz);
            AddPoint(ENC_T_Chart, t, enc);
            AddPoint(Flex_T_Domain, t, flex);

            // 3. Heavy UI (FFT, Radar, Table) - Only if requested AND decimated
            if (!doHeavyUI) return;

            if (_tickCounter % FFT_DECIMATION == 0)
            {
                double imuMag = Math.Sqrt(imu.Roll * imu.Roll + imu.Pitch * imu.Pitch + imu.Yaw * imu.Yaw);
                double forceMag = Math.Sqrt(force.Fx * force.Fx + force.Fy * force.Fy + force.Fz * force.Fz);

                UpdateFft(EMG_Frequency_Chart, emg, _emgBuffer);
                UpdateFft(IMU_Frequency_Chart, imuMag, _imuMagBuffer);
                UpdateFft(Force_Frequency_Chart, forceMag, _forceMagBuffer);
                UpdateFft(ENC_Frequency_Chart, enc, _encBuffer);
                UpdateFft(Flex_Frequency_Chart, flex, _flexBuffer);

                UpdateRadar(RadarXImu, DegreeX, imu.Roll);
                UpdateRadar(RadarYImu, DegreeY, imu.Pitch);
                UpdateRadar(RadarZImu, DegreeZ, imu.Yaw);

                // === STEP 7: New log terminal message ===
                LogMovement(emg, imuMag, forceMag, enc, flex);
            }
        }

        private void ResetSim_Click(object sender, EventArgs e)
        {
            _simTimer.Stop();
            ResetState(true);
        }

        // === STEP 8: Modified ResetState - includes actuator reset ===
        private void ResetState(bool resetTimeAndBuffers)
        {
            foreach (var s in EMGTChart.Series) s.Points.Clear();
            foreach (var s in IMU_T_Chart.Series) s.Points.Clear();
            foreach (var s in Force_T_Chart.Series) s.Points.Clear();
            foreach (var s in ENC_T_Chart.Series) s.Points.Clear();
            foreach (var s in Flex_T_Domain.Series) s.Points.Clear();

            foreach (var s in EMG_Frequency_Chart.Series) s.Points.Clear();
            foreach (var s in IMU_Frequency_Chart.Series) s.Points.Clear();
            foreach (var s in Force_Frequency_Chart.Series) s.Points.Clear();
            foreach (var s in ENC_Frequency_Chart.Series) s.Points.Clear();
            foreach (var s in Flex_Frequency_Chart.Series) s.Points.Clear();

            tableData.Controls.Clear();
            tableData.RowCount = 0;
            _logRowCount = 0;

            if (resetTimeAndBuffers)
            {
                _t = 0.0;
                _tickCounter = 0;

                // Reset actuator state
                _theta = 0.0;
                _thetaDot = 0.0;
                _thetaDesired = 0.0;

                _emgBuffer.Clear();
                _imuMagBuffer.Clear();
                _forceMagBuffer.Clear();
                _encBuffer.Clear();
                _flexBuffer.Clear();
            }
        }

        private void ModeApply_Click(object sender, EventArgs e)
        {
            _currentMode = (comboMode.SelectedIndex == 0) ? MotionMode.Realtime : MotionMode.Windowed;
            ResetSim_Click(sender, e);
        }

        // === SIMULATION MODE EVENT HANDLERS ===
        private void SimulationModeCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Don't apply immediately, wait for Apply button
            // This gives user chance to configure before switching
        }

        private void SimApply_Click(object sender, EventArgs e)
        {
            // Update simulation mode from combo box
            _simMode = (SimuBox.SelectedIndex == 0) ? SimulationMode.Playground : SimulationMode.DynamicEmg;

            // Apply UI changes
            ApplySimulationModeUI();

            // Reset to clean state
            ResetSim_Click(sender, e);

            // Update formulas to reflect current mode
            UpdateFormulas();
        }

        /// <summary>
        /// Enable/Disable UI controls based on Simulation Mode
        /// Playground: All controls enabled
        /// Dynamic EMG: Only EMG controls enabled, others disabled
        /// </summary>
        private void ApplySimulationModeUI()
        {
            bool isPlayground = (_simMode == SimulationMode.Playground);

            // === EMG Controls (Always Enabled) ===
            EMGAmplitudeImput.Enabled = true;
            EMGIntervalBar.Enabled = true;
            EMGGenerate.Enabled = true;

            // === IMU Controls ===
            IMU_XSlider.Enabled = isPlayground;
            IMU_YSlider.Enabled = isPlayground;
            IMU_ZSlider.Enabled = isPlayground;
            IMU_GenerateBtn.Enabled = isPlayground;

            // === Force Controls ===
            Force_FxSlider.Enabled = isPlayground;
            Force_FySlider.Enabled = isPlayground;
            Force_FzSlider.Enabled = isPlayground;
            Force_ApplyBtn.Enabled = isPlayground;

            // === Flex Controls ===
            Flex_BendSlider.Enabled = isPlayground;
            Flex_OscFreqSlider.Enabled = isPlayground;
            button10.Enabled = isPlayground;

            // === Encoder Controls ===
            Enc_AngleSlider.Enabled = isPlayground;
            Enc_SpeedSlider.Enabled = isPlayground;
            Enc_ApplyBtn.Enabled = isPlayground;
            Enc_ZeroBtn.Enabled = isPlayground;

            // Update panel backgrounds to indicate mode
            if (isPlayground)
            {
                panel5.BackColor = SystemColors.ControlLight; // EMG panel
                panel15.BackColor = SystemColors.ControlLight; // IMU panel
                panel17.BackColor = SystemColors.ControlLight; // Force panel
                panel21.BackColor = SystemColors.ControlLight; // Flex panel
                panel19.BackColor = SystemColors.ControlLight; // Encoder panel
            }
            else
            {
                // Dynamic EMG mode: Highlight EMG panel, gray out others
                panel5.BackColor = Color.LightGreen; // EMG panel (active)
                panel15.BackColor = Color.LightGray; // IMU panel (disabled)
                panel17.BackColor = Color.LightGray; // Force panel (disabled)
                panel21.BackColor = Color.LightGray; // Flex panel (disabled)
                panel19.BackColor = Color.LightGray; // Encoder panel (disabled)
            }
        }

        // ---------------------------------------------------------
        // 3. Dynamic Actuator & Signal Generators
        // ---------------------------------------------------------

        // === STEP 2: Desired Angle Generator (Mode-Dependent) ===
        private double GenerateThetaDesired(double t)
        {
            if (_simMode == SimulationMode.Playground)
            {
                // PLAYGROUND MODE: Use Flex sliders for trajectory
                // Use Flex_BendSlider as max amplitude (deg)
                double maxAngle = Flex_BendSlider.Value; // e.g. 0..90
                // Use Flex_OscFreqSlider as frequency (0.1..2 Hz)
                double f = Math.Max(0.1, Flex_OscFreqSlider.Value / 10.0);

                // Simple sine trajectory
                return maxAngle * Math.Sin(2.0 * Math.PI * f * t);
            }
            else // SimulationMode.DynamicEmg
            {
                // DYNAMIC EMG MODE: θ_d driven by EMG amplitude

                // Get EMG amplitude (0-100%)
                double.TryParse(EMGAmplitudeImput.Text, out double ampPercent);

                // Scaling factor: 100% EMG → 60° joint angle
                double maxAngleDeg = 60.0;
                double scaleFactor = maxAngleDeg / 100.0;

                // === FIX #2: Use same interval as GenerateEmg ===
                double baseCycle = 3.0;
                double factor = 0.5 + (EMGIntervalBar.Value / 10.0);
                double cycle = baseCycle * factor;
                double onDuration = cycle / 3.0;
                double phase = t % cycle;
                double envelope = (phase < onDuration) ? 1.0 : 0.0;

                // Desired angle based on EMG amplitude and envelope
                // θ_d(t) = k * ampEMG * envelope(t)
                double thetaDesired = scaleFactor * ampPercent * envelope;

                return thetaDesired;
            }
        }

        // === STEP 3: Actuator Update (Second-Order Dynamics) ===
        private void UpdateActuator(double t)
        {
            double dt = _dt;

            // Desired angle from playground rule
            _thetaDesired = GenerateThetaDesired(t);

            // 2nd-order actuator dynamics:
            // θ¨ + 2ζωn θ˙ + ωn² θ = ωn² θd
            double error = _thetaDesired - _theta;
            double accel = ACT_WN * ACT_WN * error
                           - 2.0 * ACT_ZETA * ACT_WN * _thetaDot;

            // Euler integration
            _thetaDot += accel * dt;
            _theta += _thetaDot * dt;
        }

        // EMG stays independent (ideal muscle activation)
        private double GenerateEmg(double t)
        {
            double.TryParse(EMGAmplitudeImput.Text, out double ampPercent);
            double A = (ampPercent / 100.0) * 5.0;

            // === FIX #1: Use EMGIntervalBar to control cycle duration ===
            // baseCycle = 3s, EMGIntervalBar scales 0.5x .. 3x
            double baseCycle = 3.0;
            double factor = 0.5 + (EMGIntervalBar.Value / 10.0); // trackbar 0..25 → 0.5..3.0
            double cycle = baseCycle * factor;

            // duty cycle: active 1/3 of cycle
            double onDuration = cycle / 3.0;
            double phase = t % cycle;
            double env = (phase < onDuration) ? 1.0 : 0.0;

            return A * env * Math.Sin(2 * Math.PI * 80.0 * t);
        }

        // === STEP 5: Sensor Generators Based on θ ===

        // 5.1: IMU from θ
        private (double Roll, double Pitch, double Yaw) GenerateImuFromTheta(double thetaDeg)
        {
            // Simple mapping: 1 joint → 3 axes with scaling
            double roll = thetaDeg;
            double pitch = 0.5 * thetaDeg;
            double yaw = 0.2 * thetaDeg;
            return (roll, pitch, yaw);
        }

        // 5.2: Force from error
        private (double Fx, double Fy, double Fz) GenerateForceFromError(double thetaDesired, double theta)
        {
            double Kf = 0.1; // stiffness-like factor
            double err = thetaDesired - theta;

            // For simplicity, put all force in Fz
            double Fz = Kf * err;
            return (0.0, 0.0, Fz);
        }

        // 5.3: Encoder from θ
        private double GenerateEncoderFromTheta(double thetaDeg)
        {
            double angle = thetaDeg % 360.0;
            if (angle < 0) angle += 360.0;
            return angle;
        }

        // 5.4: Flex from θ
        private double GenerateFlexFromTheta(double thetaDeg)
        {
            double kFlex = 1.0; // scaling; can tweak
            return kFlex * thetaDeg;
        }

        // === FIX #3: PLAYGROUND SENSOR GENERATORS (Direct from Sliders) ===

        /// <summary>
        /// Playground: IMU reads directly from slider values (quasi-static)
        /// </summary>
        private (double Roll, double Pitch, double Yaw) GenerateImuPlayground()
        {
            return (IMU_XSlider.Value, IMU_YSlider.Value, IMU_ZSlider.Value);
        }

        /// <summary>
        /// Playground: Force reads directly from slider values (quasi-static)
        /// </summary>
        private (double Fx, double Fy, double Fz) GenerateForcePlayground()
        {
            return (Force_FxSlider.Value, Force_FySlider.Value, Force_FzSlider.Value);
        }

        /// <summary>
        /// Playground: Encoder integrates speed slider over time
        /// </summary>
        private double GenerateEncoderPlayground(double t)
        {
            // Start angle from slider + speed integration
            double start = Enc_AngleSlider.Value;
            double speed = Enc_SpeedSlider.Value; // deg/s
            double angle = (start + speed * t) % 360.0;
            if (angle < 0) angle += 360.0;
            return angle;
        }

        /// <summary>
        /// Playground: Flex reads directly from slider value (quasi-static)
        /// </summary>
        private double GenerateFlexPlayground()
        {
            return Flex_BendSlider.Value;
        }

        // ---------------------------------------------------------
        // 4. Optimized Chart & Table Helpers
        // ---------------------------------------------------------

        // === STEP 7: Log Terminal in tableData ===
        private void LogMovement(double emg, double imuMag, double forceMag, double enc, double flex)
        {
            // Limit size
            if (_logRowCount >= TABLE_MAX_ROWS)
            {
                tableData.Controls.Clear();
                tableData.RowCount = 0;
                _logRowCount = 0;
            }

            tableData.SuspendLayout();

            int row = _logRowCount++;
            tableData.RowCount = row + 1;

            // Build descriptive message
            string phase =
                Math.Abs(_thetaDesired) < 1.0 ? "Rest" :
                (_thetaDesired > 0 ? "Flexion" : "Extension");

            // Show mode indicator
            string modeIndicator = _simMode == SimulationMode.Playground ? "[PG]" : "[EMG]";

            string msg =
                $"{modeIndicator} t={_t:F2}s | θ={_theta:F1}° (θd={_thetaDesired:F1}°) | " +
                $"EMG={emg:F2}, IMU|θ|={imuMag:F2}, F={forceMag:F2}N | Phase={phase}";

            var lblMsg = new Label
            {
                Text = msg,
                AutoSize = true,
                Dock = DockStyle.Fill,
                ForeColor = _simMode == SimulationMode.DynamicEmg ? Color.DarkGreen : Color.Black
            };

            // Put message in first column
            tableData.Controls.Add(lblMsg, 0, row);

            // Span across multiple columns if available (safer check)
            int colSpan = Math.Max(1, tableData.ColumnCount);
            if (colSpan > 1)
            {
                tableData.SetColumnSpan(lblMsg, colSpan);
            }

            tableData.ScrollControlIntoView(lblMsg);
            tableData.ResumeLayout(true);
            tableData.PerformLayout();
        }

        private void SetupChart(Chart chart, string xLabel, string yLabel, SeriesChartType type = SeriesChartType.FastLine)
        {
            chart.Series.Clear();
            if (chart.ChartAreas.Count > 0)
            {
                chart.ChartAreas[0].AxisX.Title = xLabel;
                chart.ChartAreas[0].AxisY.Title = yLabel;
                // OPTIMIZATION: Disable heavy grids
                chart.ChartAreas[0].AxisX.MajorGrid.Enabled = false;
                chart.ChartAreas[0].AxisY.MajorGrid.Enabled = false;
            }
            // OPTIMIZATION: Disable AntiAliasing
            chart.AntiAliasing = AntiAliasingStyles.None;
            chart.Series.Add(new Series("Signal") { ChartType = type, BorderWidth = 2, Color = Color.Blue });
        }

        private void SetupMultiChart(Chart chart, string xLabel, string yLabel, string[] names)
        {
            chart.Series.Clear();
            if (chart.ChartAreas.Count > 0)
            {
                chart.ChartAreas[0].AxisX.Title = xLabel;
                chart.ChartAreas[0].AxisY.Title = yLabel;
                chart.ChartAreas[0].AxisX.MajorGrid.Enabled = false;
                chart.ChartAreas[0].AxisY.MajorGrid.Enabled = false;
            }
            chart.AntiAliasing = AntiAliasingStyles.None;

            Color[] c = { Color.Red, Color.Green, Color.Blue };
            for (int i = 0; i < names.Length; i++)
                chart.Series.Add(new Series(names[i]) { ChartType = SeriesChartType.FastLine, BorderWidth = 2, Color = c[i] });
            if (chart.Legends.Count == 0) chart.Legends.Add(new Legend("Default"));
        }

        private void SetupFixedScatterChart(Chart chart, string xLabel, string yLabel, string title)
        {
            chart.Series.Clear(); chart.ChartAreas.Clear(); chart.Titles.Clear();
            var area = new ChartArea("Main");
            area.AxisX.Title = xLabel; area.AxisY.Title = yLabel;
            area.AxisX.Crossing = 0; area.AxisY.Crossing = 0;
            area.AxisX.Minimum = -4; area.AxisX.Maximum = 4;
            area.AxisY.Minimum = -4; area.AxisY.Maximum = 4;
            area.AxisX.Interval = 1; area.AxisY.Interval = 1;
            area.AxisX.MajorGrid.LineColor = Color.LightGray;
            area.AxisY.MajorGrid.LineColor = Color.LightGray;

            chart.ChartAreas.Add(area); chart.Titles.Add(title);
            chart.Series.Add(new Series("Poles") { ChartType = SeriesChartType.Point, MarkerStyle = MarkerStyle.Cross, MarkerSize = 12, Color = Color.Red });
            chart.Series.Add(new Series("Zeros") { ChartType = SeriesChartType.Point, MarkerStyle = MarkerStyle.Circle, MarkerSize = 10, Color = Color.Blue });
        }

        private void AddPoint(Chart chart, double x, double y) => chart.Series[0].Points.AddXY(x, y);
        private void AddMultiPoint(Chart chart, double x, double v1, double v2, double v3)
        {
            chart.Series[0].Points.AddXY(x, v1);
            chart.Series[1].Points.AddXY(x, v2);
            chart.Series[2].Points.AddXY(x, v3);
        }

        private void ScrollChart(Chart chart, double t, double window)
        {
            if (t > window)
            {
                chart.ChartAreas[0].AxisX.Minimum = t - window;
                chart.ChartAreas[0].AxisX.Maximum = t;
            }
        }

        private void UpdateFft(Chart chart, double val, Queue<double> buffer)
        {
            buffer.Enqueue(val);
            if (buffer.Count > _fftWindowSize) buffer.Dequeue();
            if (buffer.Count < _fftWindowSize) return;

            System.Numerics.Complex[] cpx = new System.Numerics.Complex[_fftWindowSize];
            var arr = buffer.ToArray();
            for (int i = 0; i < _fftWindowSize; i++) cpx[i] = new System.Numerics.Complex(arr[i], 0);
            Fourier.Forward(cpx, FourierOptions.Matlab);

            chart.Series[0].Points.Clear();
            for (int i = 0; i < _fftWindowSize / 2; i++)
                chart.Series[0].Points.AddXY((double)i / (_dt * _fftWindowSize), cpx[i].Magnitude);
        }

        private void UpdateFormulas()
        {
            // EMG formula (independent of mode)
            EMGFormula.Text = "Time: A * Env * sin(160πt) | Cycle controlled by Interval slider";

            // Mode-dependent formulas
            if (_simMode == SimulationMode.Playground)
            {
                // Playground mode: STATIC - sensors from sliders directly
                IMU_Formula.Text = "Time: Direct from sliders (Roll=X, Pitch=Y, Yaw=Z) [STATIC]";
                lbl_ForceFormulaLabel.Text = "Time: Direct from sliders (Fx, Fy, Fz) [STATIC]";
                Enc_Formula.Text = "Time: θ0 + ωt (Angle + Speed × t) [QUASI-STATIC]";
                Flex_Formula.Text = "Time: Direct from Bend slider [STATIC]";
            }
            else // DynamicEmg
            {
                // Dynamic EMG mode: DYNAMIC - θ driven by EMG through actuator
                IMU_Formula.Text = "Time: θ-based (Roll=θ, Pitch=0.5θ, Yaw=0.2θ) [EMG→θ DYNAMIC]";
                lbl_ForceFormulaLabel.Text = "Time: F = Kf*(θd - θ) [EMG-driven DYNAMIC]";
                Enc_Formula.Text = "Time: θ mod 360° [EMG-driven DYNAMIC]";
                Flex_Formula.Text = "Time: θd = k*EMG*Env(t) [EMG amplitude→angle DYNAMIC]";
            }

            // Frequency domain formulas (same for both modes)
            EMG_F_FormulaLabel.Text = "Freq: |FFT(EMG)|";
            IMU_F_FormulaLabel.Text = "Freq: |FFT(IMU_Mag)|";
            Force_F_FormulaLabel.Text = "Freq: |FFT(F_Mag)|";
            Enc_F_FormulaLabel.Text = "Freq: |FFT(Enc)|";
            Flex_F_FormulaLabel.Text = "Freq: |FFT(Flex)|";
        }

        // ---------------------------------------------------------
        // 5. System ID & S/Z Plane Poles
        // ---------------------------------------------------------

        // === STEP 6: S-Plane and Z-Plane Pole Calculation ===
        private void UpdatePoles()
        {
            double wn = ACT_WN;
            double zeta = ACT_ZETA;
            double Ts = _dt;  // sampling time

            // Continuous poles: s = -ζωn ± jωn√(1-ζ²)
            double real = -zeta * wn;
            double imag = wn * Math.Sqrt(1.0 - zeta * zeta);

            var s1 = new Complex(real, imag);
            var s2 = new Complex(real, -imag);

            // S-Plane chart
            LaplaceDomainChart.Series["Poles"].Points.Clear();
            LaplaceDomainChart.Series["Poles"].Points.AddXY(s1.Real, s1.Imaginary);
            LaplaceDomainChart.Series["Poles"].Points.AddXY(s2.Real, s2.Imaginary);

            LaplaceDomainEquation.Text =
                $"G(s) = ωₙ² / (s² + 2ζωₙs + ωₙ²)  (ωₙ={wn:F1}, ζ={zeta:F2})";

            // Discrete poles: z = exp(s*Ts)
            var z1 = Complex.Exp(s1 * Ts);
            var z2 = Complex.Exp(s2 * Ts);

            ZdomainChart.Series["Poles"].Points.Clear();
            ZdomainChart.Series["Poles"].Points.AddXY(z1.Real, z1.Imaginary);
            ZdomainChart.Series["Poles"].Points.AddXY(z2.Real, z2.Imaginary);

            ZDomainEquation.Text = $"z₁={z1.Real:F2}+j{z1.Imaginary:F2}, z₂={z2.Real:F2}+j{z2.Imaginary:F2}";
        }

        private void SetupRadarChart(Chart chart, string title)
        {
            chart.Series.Clear(); chart.Titles.Clear(); chart.Titles.Add(title);
            chart.Series.Add(new Series("Mag") { ChartType = SeriesChartType.Polar, Color = Color.Blue });
        }

        private void UpdateRadar(Chart chart, Label lbl, double val)
        {
            chart.Series[0].Points.Clear(); double ang = (val % 360 + 360) % 360;
            chart.Series[0].Points.AddXY(0, 0); chart.Series[0].Points.AddXY(ang, 100);
            lbl.Text = $"{ang:F0}°";
        }

        private void DrawUnitCircle(Chart chart)
        {
            Series s = new Series("UnitCircle") { ChartType = SeriesChartType.Spline, Color = Color.Green, BorderDashStyle = ChartDashStyle.Dash, ChartArea = "Main" };
            for (double a = 0; a <= 2 * Math.PI + 0.1; a += 0.1) s.Points.AddXY(Math.Cos(a), Math.Sin(a));
            chart.Series.Add(s);
        }

        // ---------------------------------------------------------
        // 6. Event Handlers (Legacy/Empty)
        // ---------------------------------------------------------
        private void Enc_ZeroBtn_Click(object sender, EventArgs e) { _t = 0; }
        private void ZoomIn_Click(object sender, EventArgs e) { pictureBox1.Width = (int)(pictureBox1.Width * 1.2); pictureBox1.Height = (int)(pictureBox1.Height * 1.2); }
        private void ZoomOut_Click(object sender, EventArgs e) { pictureBox1.Width = (int)(pictureBox1.Width * 0.8); pictureBox1.Height = (int)(pictureBox1.Height * 0.8); }
        private void trackBar4_Scroll(object sender, EventArgs e) { }
        private void Fdomain2Chart_Click(object sender, EventArgs e) { }
        private void EMG_Frequency_Chart_Click(object sender, EventArgs e) { }
        private void Fdomain1Chart_Click(object sender, EventArgs e) { }
        private void RadarXImu_Click(object sender, EventArgs e) { }
        private void Degree_Click(object sender, EventArgs e) { }
        private void label28_Click(object sender, EventArgs e) { }
        private void label8_Click(object sender, EventArgs e) { }
        private void label4_Click(object sender, EventArgs e) { }
        private void Tdomain1Panel_Paint(object sender, PaintEventArgs e) { }
        private void Fdomain1Panel_Paint(object sender, PaintEventArgs e) { }
        private void ZDomainPanel_Paint(object sender, PaintEventArgs e) { }
        private void ZdomainChart_Click(object sender, EventArgs e) { }
        private void chart3_Click(object sender, EventArgs e) { }
    }
}