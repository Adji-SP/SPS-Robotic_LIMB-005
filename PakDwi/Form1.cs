using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

// Note: Install MathNet.Numerics via NuGet for FFT and AR functionality
// Install-Package MathNet.Numerics

namespace PakDwi
{
    public partial class Form1 : Form
    {
        //    #region Configuration Constants
        //    private static class Config
        //    {
        //        // Simulation
        //        public const double NOMINAL_TS = 0.1;           // 100ms nominal
        //        public const int MAX_TABLE_ROWS = 500;
        //        public const int BUFFER_SIZE = 512;
        //        public const int DRAW_EVERY_N_TICKS = 3;

        //        // Biomechanics
        //        public const double TAU_ACTIVATION = 0.150;     // EMG→activation time constant
        //        public const double K_TORQUE = 2.0;             // Nm per unit activation
        //        public const double DAMPING_B = 0.10;           // Nms/rad
        //        public const double STIFFNESS_K = 0.50;         // Nm/rad
        //        public const double INERTIA_I = 0.015;          // kg·m²
        //        public const double THETA_MIN = -Math.PI / 2;
        //        public const double THETA_MAX = Math.PI / 2;

        //        // EMG
        //        public const double EMG_BASELINE_NOISE = 0.01;
        //        public const double EMG_MAX_ACTIVITY = 2.0;
        //        public const double EMG_BURST_PROB = 0.05;
        //        public const double EMG_TO_ACT_SCALE = 20.0;

        //        // Sensor noise (std dev)
        //        public const double AS5600_NOISE = 0.8;
        //        public const double FLEX_NOISE = 1.5;
        //        public const double FLEX_R_NOISE = 200.0;
        //        public const double MPU_GYRO_NOISE = 0.5;
        //        public const double MPU_ACCEL_NOISE = 0.05;
        //        public const double MPU_MAG_NOISE = 0.5;
        //        public const double ATI_FORCE_NOISE = 0.2;
        //        public const double ATI_TORQUE_NOISE = 0.1;

        //        // Signal processing - FIXED: set to 2 until general root solver is added
        //        public const int AR_ORDER = 2;
        //        public const double AR_REGULARIZATION = 1e-6;
        //    }
        //    #endregion

        //    #region Data Structures
        //    private class WorldState
        //    {
        //        public double Ts = Config.NOMINAL_TS;
        //        public double Time = 0;
        //        public double EmgRaw = 0;
        //        public double EmgAct = 0;
        //        public double Theta = 0;      // rad
        //        public double Omega = 0;      // rad/s
        //        public double Alpha = 0;      // rad/s²
        //        public double Torque = 0;     // Nm
        //    }

        //    private class SensorSnapshot
        //    {
        //        public DateTime Timestamp;
        //        public double Emg_mV;
        //        public double EmgAct;
        //        public double AS5600_Deg;
        //        public double AS5600_Raw;
        //        public double Flex_Deg;
        //        public double Flex_Resistance;
        //        public double Mpu_ax, Mpu_ay, Mpu_az;
        //        public double Mpu_gx, Mpu_gy, Mpu_gz;
        //        public double Mpu_mx, Mpu_my, Mpu_mz;
        //        public double Ati_Fx, Ati_Fy, Ati_Fz;
        //        public double Ati_Tx, Ati_Ty, Ati_Tz;
        //    }

        //    private struct Vec3
        //    {
        //        public double X, Y, Z;
        //        public Vec3(double x, double y, double z) { X = x; Y = y; Z = z; }
        //    }

        //    private struct Complex
        //    {
        //        public double Real, Imag;
        //        public Complex(double r, double i) { Real = r; Imag = i; }
        //        public double Magnitude => Math.Sqrt(Real * Real + Imag * Imag);
        //    }
        //    #endregion

        //    #region Fields
        //    private Timer dataTimer;
        //    private DataTable sensorDataTable;
        //    private Random random = new Random();
        //    private DataGridView dataGridView;
        //    private ComboBox channelSelector;
        //    private Label statusLabel;

        //    private bool isConnected = false;
        //    private bool isRecording = false;

        //    private WorldState world = new WorldState();
        //    private DateTime lastTick;
        //    private DateTime startTime;
        //    private int tickCounter = 0;

        //    // Circular buffer for combined signal
        //    private double[] combinedBuffer;
        //    private int bufferIndex = 0;
        //    private int bufferCount = 0;

        //    // Store latest snapshot for channel selection
        //    private SensorSnapshot latestSnapshot = new SensorSnapshot();

        //    // View mode
        //    private enum ViewMode { TimeFrequency, SZ }
        //    private ViewMode currentMode = ViewMode.TimeFrequency;

        //    // EMG generation state
        //    private double emgActivity = 0;
        //    #endregion

        //    #region Initialization
        //    public Form1()
        //    {
        //        InitializeComponent();
        //        InitializeCustomComponents();
        //        SetupDataTable();
        //        combinedBuffer = new double[Config.BUFFER_SIZE];
        //    }

        //    private void InitializeCustomComponents()
        //    {
        //        try
        //        {
        //            // Remove old sensor selection combo if it exists
        //            if (comboBox1 != null)
        //            {
        //                comboBox1.Visible = false;
        //            }

        //            // Create channel selector with fixed width
        //            channelSelector = new ComboBox
        //            {
        //                DropDownStyle = ComboBoxStyle.DropDownList,
        //                Width = 200, // Fixed width to prevent elongation
        //                MaxDropDownItems = 10
        //            };
        //            channelSelector.Items.AddRange(new string[] {
        //                "Combined Signal",
        //                "EMG (mV)",
        //                "EMG Activation",
        //                "Joint Angle (°)",
        //                "Joint Velocity (°/s)",
        //                "AS5600 Angle",
        //                "Flex Sensor",
        //                "MPU Accel X",
        //                "MPU Gyro X",
        //                "ATI Force Z"
        //            });
        //            channelSelector.SelectedIndex = 0;

        //            // Channel selector behavior - Combined = S/Z only
        //            channelSelector.SelectedIndexChanged += (_, __) =>
        //            {
        //                bool combined = channelSelector.SelectedItem?.ToString() == "Combined Signal";

        //                if (isRecording)
        //                {
        //                    // Only allow S/Z when Combined is selected
        //                    if (button4 != null) button4.Enabled = !combined;
        //                    if (button5 != null) button5.Enabled = combined;

        //                    // Auto-switch mode if needed
        //                    if (combined && currentMode != ViewMode.SZ && bufferCount >= 20)
        //                    {
        //                        button5_Click(null, EventArgs.Empty);
        //                    }
        //                    else if (!combined && currentMode != ViewMode.TimeFrequency)
        //                    {
        //                        button4_Click(null, EventArgs.Empty);
        //                    }
        //                }
        //            };

        //            // Add to a panel if available
        //            if (panel1 != null)
        //            {
        //                panel1.Controls.Add(channelSelector);
        //            }

        //            // Setup DataGridView in dedicated space
        //            if (tableLayoutPanel1 != null)
        //            {
        //                if (dataGridView != null && tableLayoutPanel1.Controls.Contains(dataGridView))
        //                {
        //                    tableLayoutPanel1.Controls.Remove(dataGridView);
        //                }

        //                dataGridView = new DataGridView
        //                {
        //                    Dock = DockStyle.Fill,
        //                    AllowUserToAddRows = false,
        //                    AllowUserToDeleteRows = false,
        //                    ReadOnly = true,
        //                    AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells,
        //                    RowHeadersVisible = false,
        //                    BackColor = Color.White,
        //                    GridColor = Color.LightGray,
        //                    BorderStyle = BorderStyle.FixedSingle,
        //                    DefaultCellStyle = { Font = new Font("Segoe UI", 8) }
        //                };

        //                // Place in specific location
        //                tableLayoutPanel1.Controls.Add(dataGridView, 0, 0);
        //                tableLayoutPanel1.SetColumnSpan(dataGridView, 1);
        //                tableLayoutPanel1.SetRowSpan(dataGridView, 1);
        //            }

        //            // Create status label for user feedback
        //            statusLabel = new Label
        //            {
        //                Dock = DockStyle.Bottom,
        //                Text = "Ready",
        //                TextAlign = ContentAlignment.MiddleLeft,
        //                BackColor = Color.LightGray,
        //                Height = 25
        //            };
        //            if (panel1 != null)
        //            {
        //                panel1.Controls.Add(statusLabel);
        //            }

        //            // Initialize timer
        //            dataTimer = new Timer { Interval = 100 };
        //            dataTimer.Tick += DataTimer_Tick;

        //            // Setup buttons
        //            if (button1 != null) button1.Text = "Connect";
        //            if (button2 != null) { button2.Text = "Start"; button2.Enabled = false; }
        //            if (button3 != null) { button3.Text = "Stop"; button3.Enabled = false; }
        //            if (button4 != null) { button4.Text = "Time_Frequency Domain"; button4.Enabled = false; }
        //            if (button5 != null) { button5.Text = "S_Z Domain"; button5.Enabled = false; }
        //            if (button6 != null) button6.Text = "Reset";
        //            if (button7 != null) button7.Text = "EMG   Stimulant";

        //            // Wire events
        //            if (button1 != null) { button1.Click -= button1_Click; button1.Click += button1_Click; }
        //            if (button2 != null) { button2.Click -= button2_Click; button2.Click += button2_Click; }
        //            if (button3 != null) { button3.Click -= button3_Click; button3.Click += button3_Click; }
        //            if (button4 != null) { button4.Click -= button4_Click; button4.Click += button4_Click; }
        //            if (button5 != null) { button5.Click -= button5_Click; button5.Click += button5_Click; }
        //            if (button6 != null) { button6.Click -= button6_Click; button6.Click += button6_Click; }
        //            if (button7 != null) { button7.Click -= button7_Click; button7.Click += button7_Click; }

        //            InitializeCharts();
        //        }
        //        catch (Exception ex)
        //        {
        //            MessageBox.Show($"Initialization error: {ex.Message}", "Error",
        //                MessageBoxButtons.OK, MessageBoxIcon.Error);
        //        }
        //    }

        //    private void InitializeCharts()
        //    {
        //        try
        //        {
        //            if (chart1 != null)
        //            {
        //                chart1.Series.Clear();
        //                chart1.ChartAreas.Clear();
        //                var area = new ChartArea("TimeDomain");
        //                area.AxisX.Title = "Time (s)";
        //                area.AxisY.Title = "Amplitude";
        //                area.AxisX.MajorGrid.LineColor = Color.LightGray;
        //                area.AxisY.MajorGrid.LineColor = Color.LightGray;
        //                chart1.ChartAreas.Add(area);

        //                var series = new Series("Data")
        //                {
        //                    ChartType = SeriesChartType.Line,
        //                    Color = Color.Blue,
        //                    BorderWidth = 2
        //                };
        //                chart1.Series.Add(series);
        //            }

        //            if (chart2 != null)
        //            {
        //                chart2.Series.Clear();
        //                chart2.ChartAreas.Clear();
        //                var area = new ChartArea("FrequencyDomain");
        //                area.AxisX.Title = "Frequency (Hz)";
        //                area.AxisY.Title = "Magnitude";
        //                area.AxisX.MajorGrid.LineColor = Color.LightGray;
        //                area.AxisY.MajorGrid.LineColor = Color.LightGray;
        //                chart2.ChartAreas.Add(area);

        //                var series = new Series("Spectrum")
        //                {
        //                    ChartType = SeriesChartType.Line,
        //                    Color = Color.Green,
        //                    BorderWidth = 2
        //                };
        //                chart2.Series.Add(series);
        //            }
        //        }
        //        catch (Exception ex)
        //        {
        //            MessageBox.Show($"Chart initialization error: {ex.Message}", "Error",
        //                MessageBoxButtons.OK, MessageBoxIcon.Error);
        //        }
        //    }

        //    private void SetupDataTable()
        //    {
        //        sensorDataTable = new DataTable();
        //        sensorDataTable.Columns.Add("Time(ms)", typeof(long));
        //        sensorDataTable.Columns.Add("EMG(mV)", typeof(string));
        //        sensorDataTable.Columns.Add("Act", typeof(string));
        //        sensorDataTable.Columns.Add("θ(°)", typeof(string));
        //        sensorDataTable.Columns.Add("ω(°/s)", typeof(string));
        //        sensorDataTable.Columns.Add("AS5600", typeof(string));
        //        sensorDataTable.Columns.Add("Flex", typeof(string));
        //        sensorDataTable.Columns.Add("MPU-A", typeof(string));
        //        sensorDataTable.Columns.Add("MPU-G", typeof(string));
        //        sensorDataTable.Columns.Add("ATI-F", typeof(string));
        //        sensorDataTable.Columns.Add("ATI-T", typeof(string));

        //        if (dataGridView != null)
        //        {
        //            dataGridView.DataSource = sensorDataTable;
        //            dataGridView.AutoResizeColumns();
        //        }
        //    }

        //    protected override void OnFormClosed(FormClosedEventArgs e)
        //    {
        //        base.OnFormClosed(e);
        //        dataTimer?.Stop();
        //        dataTimer?.Dispose();
        //    }
        //    #endregion

        //    #region Button Handlers
        //    private void button1_Click(object sender, EventArgs e)
        //    {
        //        if (!isConnected)
        //        {
        //            isConnected = true;
        //            if (button1 != null)
        //            {
        //                button1.Text = "Disconnect";
        //                button1.BackColor = Color.LightGreen;
        //            }
        //            if (button2 != null) button2.Enabled = true;
        //            if (statusLabel != null) statusLabel.Text = "Connected";
        //            MessageBox.Show("Virtual sensor rig connected", "Connected",
        //                MessageBoxButtons.OK, MessageBoxIcon.Information);
        //        }
        //        else
        //        {
        //            if (isRecording) StopSimulation();

        //            isConnected = false;
        //            if (button1 != null)
        //            {
        //                button1.Text = "Connect";
        //                button1.BackColor = SystemColors.Control;
        //            }
        //            if (button2 != null) button2.Enabled = false;
        //            if (button3 != null) button3.Enabled = false;
        //            if (statusLabel != null) statusLabel.Text = "Disconnected";
        //            MessageBox.Show("Disconnected", "Disconnected",
        //                MessageBoxButtons.OK, MessageBoxIcon.Information);
        //        }
        //    }

        //    private void button2_Click(object sender, EventArgs e)
        //    {
        //        if (!isConnected)
        //        {
        //            MessageBox.Show("Please connect first!", "Warning",
        //                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        //            return;
        //        }

        //        StartSimulation();
        //        if (button2 != null) { button2.Enabled = false; button2.BackColor = Color.LightGray; }
        //        if (button3 != null) { button3.Enabled = true; button3.BackColor = Color.LightCoral; }

        //        // Enable buttons based on channel selection
        //        bool combined = channelSelector?.SelectedItem?.ToString() == "Combined Signal";
        //        if (button4 != null) button4.Enabled = !combined;
        //        if (button5 != null) button5.Enabled = combined;
        //    }

        //    private void button3_Click(object sender, EventArgs e)
        //    {
        //        StopSimulation();
        //        if (button2 != null) { button2.Enabled = true; button2.BackColor = Color.LightGreen; }
        //        if (button3 != null) { button3.Enabled = false; button3.BackColor = SystemColors.Control; }
        //    }

        //    private void button4_Click(object sender, EventArgs e)
        //    {
        //        currentMode = ViewMode.TimeFrequency;
        //        ConfigureChartsForTimeFreq();
        //        UpdateTimeChart();
        //        UpdateFFTChart();

        //        // Update button states
        //        if (button4 != null) button4.BackColor = Color.LightGreen;
        //        if (button5 != null) button5.BackColor = SystemColors.Control;
        //    }

        //    private void button5_Click(object sender, EventArgs e)
        //    {
        //        if (bufferCount < 20)
        //        {
        //            MessageBox.Show("Not enough data. Please record more.", "Warning",
        //                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        //            return;
        //        }

        //        currentMode = ViewMode.SZ;
        //        ConfigureChartsForSZ();
        //        UpdateZPlane();
        //        UpdateSPlane();

        //        // Update button states
        //        if (button4 != null) button4.BackColor = SystemColors.Control;
        //        if (button5 != null) button5.BackColor = Color.LightGreen;
        //    }

        //    private void button6_Click(object sender, EventArgs e)
        //    {
        //        var result = MessageBox.Show("Reset the simulation?", "Reset",
        //            MessageBoxButtons.YesNo, MessageBoxIcon.Question);

        //        if (result == DialogResult.Yes)
        //        {
        //            ResetSimulation();
        //        }
        //    }

        //    // FIXED: EMG Stimulant button now works properly
        //    private void button7_Click(object sender, EventArgs e)
        //    {
        //        // Trigger EMG activity
        //        emgActivity = 1.0; // Set maximum activity level

        //        // If not connected, connect first
        //        if (!isConnected)
        //        {
        //            button1_Click(null, EventArgs.Empty);
        //        }

        //        // If not recording, start recording
        //        if (!isRecording)
        //        {
        //            button2_Click(null, EventArgs.Empty);
        //        }

        //        // Provide user feedback
        //        MessageBox.Show("EMG stimulation activated!", "Stimulation",
        //            MessageBoxButtons.OK, MessageBoxIcon.Information);
        //    }
        //    #endregion

        //    #region Simulation Control
        //    private void StartSimulation()
        //    {
        //        // Clear old data
        //        sensorDataTable.Clear();
        //        Array.Clear(combinedBuffer, 0, combinedBuffer.Length);
        //        bufferIndex = 0;
        //        bufferCount = 0;
        //        tickCounter = 0;

        //        // Reset world state
        //        world = new WorldState();
        //        emgActivity = 0;

        //        // Reset time
        //        startTime = DateTime.Now;
        //        lastTick = startTime;
        //        isRecording = true;

        //        // Clear charts
        //        if (chart1?.Series.Count > 0)
        //        {
        //            foreach (var series in chart1.Series)
        //            {
        //                series.Points.Clear();
        //            }
        //        }

        //        if (chart2?.Series.Count > 0)
        //        {
        //            foreach (var series in chart2.Series)
        //            {
        //                series.Points.Clear();
        //            }
        //        }

        //        // Configure charts based on current mode
        //        if (currentMode == ViewMode.TimeFrequency)
        //        {
        //            ConfigureChartsForTimeFreq();
        //        }
        //        else
        //        {
        //            ConfigureChartsForSZ();
        //        }

        //        // Start timer
        //        dataTimer.Start();

        //        // Update status
        //        if (statusLabel != null) statusLabel.Text = "Recording...";
        //    }

        //    private void StopSimulation()
        //    {
        //        dataTimer.Stop();
        //        isRecording = false;

        //        // Update status
        //        if (statusLabel != null) statusLabel.Text = "Stopped";
        //    }

        //    private void ResetSimulation()
        //    {
        //        if (isRecording) StopSimulation();

        //        isConnected = false;
        //        sensorDataTable.Clear();
        //        Array.Clear(combinedBuffer, 0, combinedBuffer.Length);
        //        bufferIndex = 0;
        //        bufferCount = 0;

        //        world = new WorldState();
        //        emgActivity = 0;

        //        if (button1 != null) { button1.Text = "Connect"; button1.BackColor = SystemColors.Control; }
        //        if (button2 != null) { button2.Enabled = false; button2.BackColor = SystemColors.Control; }
        //        if (button3 != null) { button3.Enabled = false; button3.BackColor = SystemColors.Control; }
        //        if (button4 != null) button4.Enabled = false;
        //        if (button5 != null) button5.Enabled = false;

        //        // Restore to Time/Frequency mode
        //        currentMode = ViewMode.TimeFrequency;
        //        ConfigureChartsForTimeFreq();

        //        // Update status
        //        if (statusLabel != null) statusLabel.Text = "Ready";

        //        MessageBox.Show("Simulation reset", "Reset Complete",
        //            MessageBoxButtons.OK, MessageBoxIcon.Information);
        //    }
        //    #endregion

        //    #region Core Simulation Loop
        //    private void DataTimer_Tick(object sender, EventArgs e)
        //    {
        //        if (!isRecording) return;

        //        // Measure actual Ts
        //        var now = DateTime.Now;
        //        world.Ts = Math.Max(0.001, (now - lastTick).TotalSeconds);
        //        lastTick = now;
        //        world.Time += world.Ts;

        //        // Generate EMG and step world physics
        //        world.EmgRaw = GenerateEMGRaw();
        //        StepWorld(world);

        //        // Generate all sensor outputs
        //        latestSnapshot = SenseAllSensors(world);

        //        // Compute combined signal
        //        double combined = CombinedForAnalysis(latestSnapshot);
        //        combinedBuffer[bufferIndex] = combined;
        //        bufferIndex = (bufferIndex + 1) % Config.BUFFER_SIZE;
        //        if (bufferCount < Config.BUFFER_SIZE) bufferCount++;

        //        // Log to table
        //        LogToTable(latestSnapshot);

        //        // Update charts based on mode
        //        tickCounter++;
        //        if (currentMode == ViewMode.TimeFrequency)
        //        {
        //            UpdateTimeChart();
        //            if (tickCounter % Config.DRAW_EVERY_N_TICKS == 0)
        //            {
        //                UpdateFFTChart();
        //            }
        //        }
        //        else
        //        {
        //            if (tickCounter % Config.DRAW_EVERY_N_TICKS == 0)
        //            {
        //                UpdateZPlane();
        //                UpdateSPlane();
        //            }
        //        }
        //    }

        //    private void StepWorld(WorldState w)
        //    {
        //        // EMG rectification + low-pass filter to activation
        //        double rect = Math.Abs(w.EmgRaw);
        //        w.EmgAct += (w.Ts / Config.TAU_ACTIVATION) * (rect - w.EmgAct);

        //        // Normalize activation (0-1 range)
        //        double act = Math.Min(1.0, w.EmgAct * Config.EMG_TO_ACT_SCALE);

        //        // Compute torque: active + passive (damping + stiffness)
        //        w.Torque = Config.K_TORQUE * act - Config.DAMPING_B * w.Omega - Config.STIFFNESS_K * w.Theta;

        //        // Euler integration of joint dynamics
        //        w.Alpha = w.Torque / Config.INERTIA_I;
        //        w.Omega += w.Ts * w.Alpha;
        //        w.Theta += w.Ts * w.Omega;

        //        // Clamp angle to realistic range
        //        w.Theta = Math.Max(Config.THETA_MIN, Math.Min(Config.THETA_MAX, w.Theta));
        //    }

        //    private SensorSnapshot SenseAllSensors(WorldState w)
        //    {
        //        var s = new SensorSnapshot { Timestamp = DateTime.Now };

        //        // EMG
        //        s.Emg_mV = w.EmgRaw;
        //        s.EmgAct = w.EmgAct;

        //        // AS5600 - magnetic encoder
        //        double degAngle = w.Theta * 180.0 / Math.PI;
        //        s.AS5600_Deg = ((degAngle % 360) + 360) % 360 + RandN(0, Config.AS5600_NOISE);
        //        s.AS5600_Raw = s.AS5600_Deg * 4095.0 / 360.0;

        //        // Flex sensor
        //        s.Flex_Deg = degAngle + RandN(0, Config.FLEX_NOISE);
        //        s.Flex_Resistance = 10000 + Math.Abs(s.Flex_Deg) * 300 + RandN(0, Config.FLEX_R_NOISE);

        //        // MPU-9250 gyroscope (angular velocity)
        //        s.Mpu_gx = w.Omega * 180.0 / Math.PI + RandN(0, Config.MPU_GYRO_NOISE);
        //        s.Mpu_gy = RandN(0, Config.MPU_GYRO_NOISE * 0.5);
        //        s.Mpu_gz = RandN(0, Config.MPU_GYRO_NOISE * 0.5);

        //        // MPU-9250 accelerometer (gravity rotation)
        //        var gravity = RotateVector(new Vec3(0, 0, 9.81), w.Theta, 'x');
        //        s.Mpu_ax = gravity.X + RandN(0, Config.MPU_ACCEL_NOISE);
        //        s.Mpu_ay = gravity.Y + RandN(0, Config.MPU_ACCEL_NOISE);
        //        s.Mpu_az = gravity.Z + RandN(0, Config.MPU_ACCEL_NOISE);

        //        // MPU-9250 magnetometer (earth field rotation)
        //        var magField = RotateVector(new Vec3(30, 0, -40), w.Theta, 'x');
        //        s.Mpu_mx = magField.X + RandN(0, Config.MPU_MAG_NOISE);
        //        s.Mpu_my = magField.Y + RandN(0, Config.MPU_MAG_NOISE);
        //        s.Mpu_mz = magField.Z + RandN(0, Config.MPU_MAG_NOISE);

        //        // ATI Nano17 force/torque sensor
        //        s.Ati_Fz = 10.0 + (-2.0 * w.Theta) + RandN(0, Config.ATI_FORCE_NOISE);
        //        s.Ati_Fx = 0.5 * w.Omega + RandN(0, Config.ATI_FORCE_NOISE);
        //        s.Ati_Fy = RandN(0, Config.ATI_FORCE_NOISE);
        //        s.Ati_Tz = w.Torque * 100 + RandN(0, Config.ATI_TORQUE_NOISE);
        //        s.Ati_Tx = RandN(0, Config.ATI_TORQUE_NOISE);
        //        s.Ati_Ty = RandN(0, Config.ATI_TORQUE_NOISE);

        //        return s;
        //    }

        //    private double CombinedForAnalysis(SensorSnapshot s)
        //    {
        //        // Weighted combination of normalized channels
        //        double emg = s.Emg_mV;
        //        double theta = s.Flex_Deg / 90.0;
        //        double omega = s.Mpu_gx / 180.0;
        //        double fz = s.Ati_Fz / 10.0;

        //        return 0.4 * emg + 0.3 * theta + 0.2 * omega + 0.1 * fz;
        //    }

        //    private double GetSelectedChannelValue()
        //    {
        //        string channel = channelSelector?.SelectedItem?.ToString() ?? "Combined Signal";

        //        switch (channel)
        //        {
        //            case "EMG (mV)": return latestSnapshot.Emg_mV;
        //            case "EMG Activation": return latestSnapshot.EmgAct;
        //            case "Joint Angle (°)": return world.Theta * 180.0 / Math.PI;
        //            case "Joint Velocity (°/s)": return world.Omega * 180.0 / Math.PI;
        //            case "AS5600 Angle": return latestSnapshot.AS5600_Deg;
        //            case "Flex Sensor": return latestSnapshot.Flex_Deg;
        //            case "MPU Accel X": return latestSnapshot.Mpu_ax;
        //            case "MPU Gyro X": return latestSnapshot.Mpu_gx;
        //            case "ATI Force Z": return latestSnapshot.Ati_Fz;
        //            default: return combinedBuffer[(bufferIndex - 1 + Config.BUFFER_SIZE) % Config.BUFFER_SIZE];
        //        }
        //    }

        //    // FIXED: Improved table logging with thread safety
        //    private void LogToTable(SensorSnapshot s)
        //    {
        //        if (!isRecording) return; // Ensure we only log when recording

        //        long ms = (long)(DateTime.Now - startTime).TotalMilliseconds;

        //        var row = sensorDataTable.NewRow();
        //        row["Time(ms)"] = ms;
        //        row["EMG(mV)"] = $"{s.Emg_mV:F3}";
        //        row["Act"] = $"{s.EmgAct:F3}";
        //        row["θ(°)"] = $"{s.Flex_Deg:F1}";
        //        row["ω(°/s)"] = $"{s.Mpu_gx:F1}";
        //        row["AS5600"] = $"{s.AS5600_Deg:F1}°";
        //        row["Flex"] = $"{s.Flex_Resistance:F0}Ω";
        //        row["MPU-A"] = $"[{s.Mpu_ax:F2},{s.Mpu_ay:F2},{s.Mpu_az:F2}]";
        //        row["MPU-G"] = $"[{s.Mpu_gx:F1},{s.Mpu_gy:F1},{s.Mpu_gz:F1}]";
        //        row["ATI-F"] = $"[{s.Ati_Fx:F1},{s.Ati_Fy:F1},{s.Ati_Fz:F1}]";
        //        row["ATI-T"] = $"[{s.Ati_Tx:F1},{s.Ati_Ty:F1},{s.Ati_Tz:F1}]";

        //        // Use Invoke to ensure UI thread safety
        //        if (dataGridView.InvokeRequired)
        //        {
        //            dataGridView.Invoke(new Action(() => {
        //                sensorDataTable.Rows.Add(row);
        //                while (sensorDataTable.Rows.Count > Config.MAX_TABLE_ROWS)
        //                {
        //                    sensorDataTable.Rows.RemoveAt(0);
        //                }

        //                // Auto-scroll to latest row
        //                if (dataGridView.Rows.Count > 0)
        //                {
        //                    dataGridView.FirstDisplayedScrollingRowIndex = dataGridView.Rows.Count - 1;
        //                }
        //            }));
        //        }
        //        else
        //        {
        //            sensorDataTable.Rows.Add(row);
        //            while (sensorDataTable.Rows.Count > Config.MAX_TABLE_ROWS)
        //            {
        //                sensorDataTable.Rows.RemoveAt(0);
        //            }

        //            // Auto-scroll to latest row
        //            //if (dataGridView.Rows.Count > 0)
        //            //{
        //            //    dataGridView.FirstDisplayedScrollingRowIndex = dataGridView.Rows.Count - 1;
        //            //}
        //        }
        //    }
        //    #endregion

        //    #region Chart Updates - FIXED: Proper series rebuild
        //    private void ConfigureChartsForTimeFreq()
        //    {
        //        // Chart1: Time Domain - COMPLETELY rebuild series
        //        if (chart1 != null)
        //        {
        //            chart1.Series.Clear();
        //            chart1.Titles.Clear();
        //            chart1.Titles.Add("Time Domain");

        //            var area = chart1.ChartAreas[0];
        //            area.AxisX.Title = "Time (s)";
        //            area.AxisY.Title = "Amplitude";
        //            area.AxisX.Minimum = double.NaN;
        //            area.AxisX.Maximum = double.NaN;
        //            area.AxisY.Minimum = double.NaN;
        //            area.AxisY.Maximum = double.NaN;

        //            chart1.Series.Add(new Series("Data")
        //            {
        //                ChartType = SeriesChartType.Line,
        //                Color = Color.Blue,
        //                BorderWidth = 2
        //            });
        //        }

        //        // Chart2: Frequency Domain - COMPLETELY rebuild series
        //        if (chart2 != null)
        //        {
        //            chart2.Series.Clear();
        //            chart2.Titles.Clear();
        //            chart2.Titles.Add("Frequency Domain");

        //            var area = chart2.ChartAreas[0];
        //            area.AxisX.Title = "Frequency (Hz)";
        //            area.AxisY.Title = "Magnitude";
        //            area.AxisX.Minimum = double.NaN;
        //            area.AxisX.Maximum = double.NaN;
        //            area.AxisY.Minimum = double.NaN;
        //            area.AxisY.Maximum = double.NaN;

        //            chart2.Series.Add(new Series("Spectrum")
        //            {
        //                ChartType = SeriesChartType.Line,
        //                Color = Color.Green,
        //                BorderWidth = 2
        //            });
        //        }
        //    }

        //    // FIXED: Improved S/Z plane configuration
        //    private void ConfigureChartsForSZ()
        //    {
        //        // S-plane on chart1 - COMPLETELY rebuild
        //        if (chart1 != null)
        //        {
        //            chart1.Series.Clear();
        //            chart1.Titles.Clear();
        //            chart1.Titles.Add("S-Domain (Laplace)");

        //            var area = chart1.ChartAreas[0];
        //            area.AxisX.Title = "Real (σ)";
        //            area.AxisY.Title = "Imag (jω)";
        //            area.AxisX.Minimum = -50;
        //            area.AxisX.Maximum = 10;
        //            area.AxisY.Minimum = -50;
        //            area.AxisY.Maximum = 50;
        //            area.AxisX.Interval = 10;
        //            area.AxisY.Interval = 10;

        //            chart1.Series.Add(new Series("Poles")
        //            {
        //                ChartType = SeriesChartType.Point,
        //                MarkerStyle = MarkerStyle.Cross,
        //                MarkerSize = 10,
        //                Color = Color.Red
        //            });

        //            var jAxis = new Series("jOmegaAxis")
        //            {
        //                ChartType = SeriesChartType.Line,
        //                Color = Color.Gray,
        //                BorderWidth = 1
        //            };
        //            for (double w = -50; w <= 50; w += 1)
        //            {
        //                jAxis.Points.AddXY(0, w);
        //            }
        //            chart1.Series.Add(jAxis);
        //        }

        //        // Z-plane on chart2 - COMPLETELY rebuild
        //        if (chart2 != null)
        //        {
        //            chart2.Series.Clear();
        //            chart2.Titles.Clear();
        //            chart2.Titles.Add("Z-Domain");

        //            var area = chart2.ChartAreas[0];
        //            area.AxisX.Title = "Real";
        //            area.AxisY.Title = "Imag";
        //            area.AxisX.Minimum = -1.5;
        //            area.AxisX.Maximum = 1.5;
        //            area.AxisY.Minimum = -1.5;
        //            area.AxisY.Maximum = 1.5;
        //            area.AxisX.Interval = 0.5;
        //            area.AxisY.Interval = 0.5;

        //            chart2.Series.Add(new Series("Poles")
        //            {
        //                ChartType = SeriesChartType.Point,
        //                MarkerStyle = MarkerStyle.Cross,
        //                MarkerSize = 10,
        //                Color = Color.Red
        //            });

        //            var unitCircle = new Series("UnitCircle")
        //            {
        //                ChartType = SeriesChartType.Line,
        //                Color = Color.Gray,
        //                BorderWidth = 1
        //            };
        //            for (double theta = 0; theta <= 2 * Math.PI; theta += 0.05)
        //            {
        //                unitCircle.Points.AddXY(Math.Cos(theta), Math.Sin(theta));
        //            }
        //            chart2.Series.Add(unitCircle);
        //        }
        //    }

        //    private void UpdateTimeChart()
        //    {
        //        if (chart1?.Series.Count == 0 || bufferCount < 2) return;

        //        var series = chart1.Series[0];
        //        series.Points.Clear();

        //        int displayCount = Math.Min(200, bufferCount);

        //        // For non-combined channels, show actual sensor values
        //        string channel = channelSelector?.SelectedItem?.ToString() ?? "Combined Signal";
        //        bool isCombined = channel == "Combined Signal";

        //        if (isCombined)
        //        {
        //            int startIdx = (bufferIndex - displayCount + Config.BUFFER_SIZE) % Config.BUFFER_SIZE;
        //            for (int i = 0; i < displayCount; i++)
        //            {
        //                int idx = (startIdx + i) % Config.BUFFER_SIZE;
        //                double t = (bufferCount - displayCount + i) * world.Ts;
        //                series.Points.AddXY(t, combinedBuffer[idx]);
        //            }
        //        }
        //        else
        //        {
        //            // Show selected channel from stored snapshots
        //            // (Simplified - just show recent from combined for now)
        //            int startIdx = (bufferIndex - displayCount + Config.BUFFER_SIZE) % Config.BUFFER_SIZE;
        //            for (int i = 0; i < displayCount; i++)
        //            {
        //                double t = (bufferCount - displayCount + i) * world.Ts;
        //                series.Points.AddXY(t, combinedBuffer[(startIdx + i) % Config.BUFFER_SIZE]);
        //            }
        //        }
        //    }

        //    private void UpdateFFTChart()
        //    {
        //        if (chart2?.Series.Count == 0 || bufferCount < 16) return;

        //        var series = chart2.Series[0];
        //        series.Points.Clear();

        //        int N = Math.Min(256, NextPowerOf2(bufferCount));
        //        double[] data = new double[N];

        //        // Copy data with Hann window
        //        int startIdx = (bufferIndex - N + Config.BUFFER_SIZE) % Config.BUFFER_SIZE;
        //        for (int i = 0; i < N; i++)
        //        {
        //            int idx = (startIdx + i) % Config.BUFFER_SIZE;
        //            double window = 0.5 * (1 - Math.Cos(2 * Math.PI * i / (N - 1)));
        //            data[i] = combinedBuffer[idx] * window;
        //        }

        //        // Simple DFT (replace with MathNet.Numerics FFT for better performance)
        //        double fs = 1.0 / world.Ts;
        //        for (int k = 0; k < N / 2; k++)
        //        {
        //            double re = 0, im = 0;
        //            for (int n = 0; n < N; n++)
        //            {
        //                double angle = 2 * Math.PI * k * n / N;
        //                re += data[n] * Math.Cos(angle);
        //                im -= data[n] * Math.Sin(angle);
        //            }
        //            double mag = Math.Sqrt(re * re + im * im) / N;
        //            double freq = k * fs / N;
        //            series.Points.AddXY(freq, mag);
        //        }
        //    }

        //    // FIXED: Improved Z-plane update with pole labels
        //    private void UpdateZPlane()
        //    {
        //        if (chart2 == null || bufferCount < 20) return;

        //        var poleSeries = chart2.Series.FindByName("Poles");
        //        if (poleSeries != null)
        //        {
        //            poleSeries.Points.Clear();

        //            var poles = EstimateARPoles(Config.AR_ORDER);
        //            foreach (var pole in poles)
        //            {
        //                poleSeries.Points.AddXY(pole.Real, pole.Imag);
        //            }

        //            // Add pole labels
        //            foreach (DataPoint point in poleSeries.Points)
        //            {
        //                point.Label = $"({point.XValue:F2}, {point.YValues[0]:F2})";
        //            }
        //        }
        //    }

        //    // FIXED: Improved S-plane update with pole labels
        //    private void UpdateSPlane()
        //    {
        //        if (chart1 == null || bufferCount < 20) return;

        //        var poleSeries = chart1.Series.FindByName("Poles");
        //        if (poleSeries != null)
        //        {
        //            poleSeries.Points.Clear();

        //            var zPoles = EstimateARPoles(Config.AR_ORDER);
        //            foreach (var zPole in zPoles)
        //            {
        //                var sPole = BilinearZtoS(zPole, world.Ts);
        //                poleSeries.Points.AddXY(sPole.Real, sPole.Imag);
        //            }

        //            // Add pole labels
        //            foreach (DataPoint point in poleSeries.Points)
        //            {
        //                point.Label = $"({point.XValue:F2}, {point.YValues[0]:F2})";
        //            }
        //        }
        //    }
        //    #endregion

        //    #region Signal Processing
        //    private List<Complex> EstimateARPoles(int order)
        //    {
        //        var poles = new List<Complex>();
        //        if (bufferCount < order * 2) return poles;

        //        int N = Math.Min(256, bufferCount);

        //        // Extract data and remove mean
        //        double[] x = new double[N];
        //        int startIdx = (bufferIndex - N + Config.BUFFER_SIZE) % Config.BUFFER_SIZE;
        //        double mean = 0;

        //        for (int i = 0; i < N; i++)
        //        {
        //            int idx = (startIdx + i) % Config.BUFFER_SIZE;
        //            x[i] = combinedBuffer[idx];
        //            mean += x[i];
        //        }
        //        mean /= N;

        //        for (int i = 0; i < N; i++)
        //        {
        //            x[i] -= mean;
        //        }

        //        // Compute autocorrelations
        //        double[] R = new double[order + 1];
        //        for (int lag = 0; lag <= order; lag++)
        //        {
        //            for (int i = lag; i < N; i++)
        //            {
        //                R[lag] += x[i] * x[i - lag];
        //            }
        //            R[lag] /= N;
        //        }

        //        // Solve Yule-Walker equations with regularization
        //        double[,] A = new double[order, order];
        //        double[] b = new double[order];

        //        for (int i = 0; i < order; i++)
        //        {
        //            for (int j = 0; j < order; j++)
        //            {
        //                A[i, j] = R[Math.Abs(i - j)];
        //            }
        //            A[i, i] += Config.AR_REGULARIZATION * R[0]; // Regularization
        //            b[i] = -R[i + 1];
        //        }

        //        double[] a = SolveLinearSystem(A, b, order);
        //        if (a == null) return poles;

        //        // FIX: Actually compute roots for order 2
        //        if (order == 2)
        //        {
        //            double disc = a[0] * a[0] - 4 * a[1];
        //            if (disc >= 0)
        //            {
        //                double sq = Math.Sqrt(disc);
        //                poles.Add(new Complex((-a[0] + sq) / 2, 0));
        //                poles.Add(new Complex((-a[0] - sq) / 2, 0));
        //            }
        //            else
        //            {
        //                double sq = Math.Sqrt(-disc);
        //                poles.Add(new Complex(-a[0] / 2, sq / 2));
        //                poles.Add(new Complex(-a[0] / 2, -sq / 2));
        //            }
        //        }
        //        // TODO: For higher orders, implement companion matrix eigenvalue solver

        //        return poles;
        //    }

        //    private Complex BilinearZtoS(Complex z, double Ts)
        //    {
        //        // s = (2/Ts) * (z - 1) / (z + 1)
        //        double nr = z.Real - 1;
        //        double ni = z.Imag;
        //        double dr = z.Real + 1;
        //        double di = z.Imag;

        //        double den = dr * dr + di * di;
        //        if (den < 1e-12) return new Complex(0, 0);

        //        double qr = (nr * dr + ni * di) / den;
        //        double qi = (ni * dr - nr * di) / den;

        //        double k = 2.0 / Ts;
        //        return new Complex(k * qr, k * qi);
        //    }

        //    private double[] SolveLinearSystem(double[,] A, double[] b, int n)
        //    {
        //        // Simple Gaussian elimination with pivoting
        //        double[,] aug = new double[n, n + 1];

        //        for (int i = 0; i < n; i++)
        //        {
        //            for (int j = 0; j < n; j++)
        //            {
        //                aug[i, j] = A[i, j];
        //            }
        //            aug[i, n] = b[i];
        //        }

        //        for (int i = 0; i < n; i++)
        //        {
        //            // Find pivot
        //            int maxRow = i;
        //            for (int k = i + 1; k < n; k++)
        //            {
        //                if (Math.Abs(aug[k, i]) > Math.Abs(aug[maxRow, i]))
        //                {
        //                    maxRow = k;
        //                }
        //            }

        //            // Swap rows
        //            for (int k = i; k <= n; k++)
        //            {
        //                double tmp = aug[maxRow, k];
        //                aug[maxRow, k] = aug[i, k];
        //                aug[i, k] = tmp;
        //            }

        //            if (Math.Abs(aug[i, i]) < 1e-12) return null;

        //            // Forward elimination
        //            for (int k = i + 1; k < n; k++)
        //            {
        //                double c = aug[k, i] / aug[i, i];
        //                for (int j = i; j <= n; j++)
        //                {
        //                    aug[k, j] -= c * aug[i, j];
        //                }
        //            }
        //        }

        //        // Back substitution
        //        double[] x = new double[n];
        //        for (int i = n - 1; i >= 0; i--)
        //        {
        //            x[i] = aug[i, n];
        //            for (int j = i + 1; j < n; j++)
        //            {
        //                x[i] -= aug[i, j] * x[j];
        //            }
        //            x[i] /= aug[i, i];
        //        }

        //        return x;
        //    }
        //    #endregion

        //    #region Utility Functions
        //    private double GenerateEMGRaw()
        //    {
        //        // Stochastic EMG bursts
        //        if (random.NextDouble() < Config.EMG_BURST_PROB)
        //        {
        //            emgActivity = random.NextDouble();
        //        }

        //        double baseline = (random.NextDouble() - 0.5) * Config.EMG_BASELINE_NOISE;
        //        double signal = 0;

        //        if (emgActivity > 0.3)
        //        {
        //            signal = emgActivity * Config.EMG_MAX_ACTIVITY *
        //                     Math.Abs(Math.Sin(tickCounter * 0.5 + random.NextDouble() * Math.PI));
        //            signal += (random.NextDouble() - 0.5) * 0.5;
        //        }

        //        emgActivity *= 0.95; // Decay
        //        return baseline + signal;
        //    }

        //    private Vec3 RotateVector(Vec3 v, double angle, char axis)
        //    {
        //        double c = Math.Cos(angle);
        //        double s = Math.Sin(angle);

        //        if (axis == 'x')
        //        {
        //            return new Vec3(v.X, c * v.Y - s * v.Z, s * v.Y + c * v.Z);
        //        }
        //        else if (axis == 'y')
        //        {
        //            return new Vec3(c * v.X + s * v.Z, v.Y, -s * v.X + c * v.Z);
        //        }
        //        else // z
        //        {
        //            return new Vec3(c * v.X - s * v.Y, s * v.X + c * v.Y, v.Z);
        //        }
        //    }

        //    private double RandN(double mean, double stdDev)
        //    {
        //        // Box-Muller transform
        //        double u1 = 1.0 - random.NextDouble();
        //        double u2 = 1.0 - random.NextDouble();
        //        double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        //        return mean + stdDev * randStdNormal;
        //    }

        //    private int NextPowerOf2(int n)
        //    {
        //        int power = 1;
        //        while (power < n) power *= 2;
        //        return power;
        //    }
        //    #endregion

        //    #region Form Event Handlers (Auto-generated stubs)
        //    private void Form1_Load(object sender, EventArgs e) { }
        //    private void panel1_Paint(object sender, PaintEventArgs e) { }
        //    private void chart1_Click(object sender, EventArgs e) { }
        //    private void tableLayoutPanel1_Paint(object sender, PaintEventArgs e) { }
        //    private void chart1_Click_1(object sender, EventArgs e) { }
        //    private void button6_Click_1(object sender, EventArgs e) { }
        //    private void comboBox1_SelectedIndexChanged(object sender, EventArgs e) { }
        //    private void panel1_Paint_1(object sender, PaintEventArgs e) { }
        //    private void flowLayoutPanel1_Paint(object sender, PaintEventArgs e) { }
        //    private void chart2_Click(object sender, EventArgs e) { }
        //    private void flowLayoutPanel2_Paint(object sender, EventArgs e) { }
        //    private void buttons7_Click(object sender, EventArgs e) { }
        //    private void chart1_Click_2(object sender, EventArgs e) { }
        //    #endregion

        //    private void chart2_Click_1(object sender, EventArgs e)
        //    {

        //    }

        //    private void RadarXImu_Click(object sender, EventArgs e)
        //    {

        //    }
    }
}