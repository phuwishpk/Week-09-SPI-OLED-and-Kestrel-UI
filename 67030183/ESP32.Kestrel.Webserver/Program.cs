using ESP32.Kestrel.Webserver.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<CalibrationService>();
var app = builder.Build();

// ค่าสถานะ Telemetry ส่วนกลางที่สามารถอัปเดตแบบเรียลไทม์
int currentRawAdc = 2048;

// Route หน้าแรก: แดชบอร์ด SVG Gauge แบบอินเทอร์แอคทีฟ (Lab 9.2 & Lab 9.3)
app.MapGet("/", (CalibrationService cal) =>
{
    return Results.Content(@"
<!DOCTYPE html>
<html lang='th'>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1'>
    <title>ESP32 Closed-Loop IoT Dashboard (Lab 9.3)</title>
    <style>
        :root {
            --bg: #0b1120;
            --card-bg: rgba(30, 41, 59, 0.7);
            --border: rgba(255, 255, 255, 0.1);
            --primary: #38bdf8;
            --accent: #0ea5e9;
            --yellow: #facc15;
            --text-main: #f8fafc;
            --text-muted: #94a3b8;
        }
        * { box-sizing: border-box; margin: 0; padding: 0; }
        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
            background: radial-gradient(circle at 50% 20%, #1e293b 0%, #0b1120 100%);
            color: var(--text-main);
            min-height: 100vh;
            display: flex;
            flex-direction: column;
            align-items: center;
            padding: 30px 20px;
        }
        .container {
            width: 100%;
            max-width: 900px;
            display: flex;
            flex-direction: column;
            gap: 24px;
        }
        header {
            text-align: center;
        }
        header h1 {
            font-size: 2rem;
            color: var(--primary);
            margin-bottom: 6px;
            letter-spacing: -0.5px;
        }
        header p {
            color: var(--text-muted);
            font-size: 0.95rem;
        }
        .badge {
            display: inline-block;
            background: rgba(14, 165, 233, 0.15);
            color: #38bdf8;
            padding: 4px 12px;
            border-radius: 9999px;
            font-size: 0.8rem;
            font-weight: 600;
            margin-top: 8px;
            border: 1px solid rgba(56, 189, 248, 0.3);
        }
        .card {
            background: var(--card-bg);
            backdrop-filter: blur(12px);
            border: 1px solid var(--border);
            border-radius: 16px;
            padding: 24px;
            box-shadow: 0 10px 30px rgba(0, 0, 0, 0.4);
        }
        .gauge-section {
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 24px;
            align-items: center;
        }
        @media (max-width: 768px) {
            .gauge-section { grid-template-columns: 1fr; }
        }
        .gauge-container {
            display: flex;
            flex-direction: column;
            align-items: center;
            justify-content: center;
        }
        svg#speedometer {
            width: 260px;
            height: 170px;
            overflow: visible;
        }
        .gauge-track {
            fill: none;
            stroke: #334155;
            stroke-width: 16;
            stroke-linecap: round;
        }
        .gauge-fill {
            fill: none;
            stroke: url(#gauge-gradient);
            stroke-width: 16;
            stroke-linecap: round;
            stroke-dasharray: 251.3;
            stroke-dashoffset: 125.6;
            transition: stroke-dashoffset 0.15s ease-out;
        }
        .gauge-needle {
            stroke: #ef4444;
            stroke-width: 3;
            stroke-linecap: round;
            transform-origin: 130px 140px;
            transition: transform 0.15s ease-out;
        }
        .gauge-center-dot {
            fill: #f8fafc;
        }
        .metrics-grid {
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 16px;
        }
        .metric-box {
            background: rgba(15, 23, 42, 0.6);
            border: 1px solid rgba(255, 255, 255, 0.05);
            border-radius: 12px;
            padding: 16px;
            text-align: center;
        }
        .metric-title {
            font-size: 0.8rem;
            text-transform: uppercase;
            color: var(--text-muted);
            margin-bottom: 6px;
            letter-spacing: 0.5px;
        }
        .metric-value {
            font-size: 1.8rem;
            font-weight: 700;
            color: #f8fafc;
        }
        .metric-value.highlight {
            color: var(--primary);
        }
        .metric-value.yellow {
            color: var(--yellow);
        }
        .slider-box {
            margin-top: 20px;
            background: rgba(15, 23, 42, 0.4);
            padding: 16px;
            border-radius: 12px;
            border: 1px solid rgba(255, 255, 255, 0.05);
        }
        .slider-box label {
            display: flex;
            justify-content: space-between;
            font-size: 0.85rem;
            color: var(--text-muted);
            margin-bottom: 10px;
        }
        input[type='range'] {
            width: 100%;
            height: 8px;
            border-radius: 5px;
            background: #334155;
            outline: none;
            -webkit-appearance: none;
        }
        input[type='range']::-webkit-slider-thumb {
            -webkit-appearance: none;
            width: 22px;
            height: 22px;
            border-radius: 50%;
            background: var(--primary);
            cursor: pointer;
            box-shadow: 0 0 10px rgba(56, 189, 248, 0.8);
        }
        .co-verify-buttons {
            display: flex;
            flex-wrap: wrap;
            gap: 8px;
            margin-top: 12px;
        }
        .btn-point {
            flex: 1;
            min-width: 70px;
            background: #1e293b;
            color: #cbd5e1;
            border: 1px solid #475569;
            padding: 8px 4px;
            border-radius: 8px;
            font-size: 0.8rem;
            font-weight: 600;
            cursor: pointer;
            transition: all 0.2s;
        }
        .btn-point:hover {
            background: #0284c7;
            color: white;
            border-color: #38bdf8;
        }
        .footer-status {
            text-align: center;
            font-size: 0.85rem;
            color: var(--text-muted);
            margin-top: 10px;
        }
    </style>
</head>
<body>
    <div class='container'>
        <header>
            <h1>⚡ ESP32 Closed-Loop IoT Dashboard</h1>
            <p>ระบบวงปิด Potentiometer (GPIO 34) &rarr; Kestrel (.NET 10) &rarr; SPI OLED SSD1306</p>
            <div class='badge'>STUDENT ID: 67030183 | LAB 9.3 TELEMETRY</div>
        </header>

        <div class='card'>
            <div class='gauge-section'>
                <div class='gauge-container'>
                    <svg id='speedometer' viewBox='0 0 260 170'>
                        <defs>
                            <linearGradient id='gauge-gradient' x1='0%' y1='0%' x2='100%' y2='0%'>
                                <stop offset='0%' stop-color='#0ea5e9'/>
                                <stop offset='50%' stop-color='#38bdf8'/>
                                <stop offset='100%' stop-color='#facc15'/>
                            </linearGradient>
                        </defs>
                        <!-- พื้นหลังเกจครึ่งวงกลม รัศมี 80, จุดศูนย์กลาง (130, 140) -->
                        <path class='gauge-track' d='M 50 140 A 80 80 0 0 1 210 140'/>
                        <path id='gauge-progress' class='gauge-fill' d='M 50 140 A 80 80 0 0 1 210 140'/>
                        <!-- เข็มวัด -->
                        <line id='gauge-needle' class='gauge-needle' x1='130' y1='140' x2='60' y2='140'/>
                        <circle class='gauge-center-dot' cx='130' cy='140' r='6'/>
                    </svg>
                    <div style='margin-top: -20px; font-weight: bold; font-size: 1.5rem; color: #38bdf8;' id='gauge-display-pct'>0.0 %</div>
                    <div style='font-size: 0.8rem; color: #94a3b8;'>SVG GAUGE SPEEDOMETER</div>
                </div>

                <div class='metrics-grid'>
                    <div class='metric-box'>
                        <div class='metric-title'>Raw ADC (12-Bit)</div>
                        <div class='metric-value highlight' id='val-raw'>150</div>
                    </div>
                    <div class='metric-box'>
                        <div class='metric-title'>Calibrated Output</div>
                        <div class='metric-value yellow' id='val-cal'>0.0 %</div>
                    </div>
                    <div class='metric-box'>
                        <div class='metric-title'>OLED Message</div>
                        <div class='metric-value' style='font-size: 1rem;' id='val-msg'>SYSTEM READY</div>
                    </div>
                    <div class='metric-box'>
                        <div class='metric-title'>End-to-End Latency</div>
                        <div class='metric-value' style='font-size: 1.2rem; color: #4ade80;'>~32 ms</div>
                    </div>
                </div>
            </div>

            <div class='slider-box'>
                <label>
                    <span><strong>🎛️ ปรับค่า Potentiometer (ADC Simulation & Co-Verification):</strong></span>
                    <span id='slider-display'>150 (Min)</span>
                </label>
                <input type='range' id='adc-slider' min='150' max='3950' value='150' oninput='updateFromSlider(this.value)'>

                <div class='co-verify-buttons'>
                    <button class='btn-point' onclick='setPoint(150)'>Point 1<br>(Min: 0%)</button>
                    <button class='btn-point' onclick='setPoint(1100)'>Point 2<br>(25%)</button>
                    <button class='btn-point' onclick='setPoint(2050)'>Point 3<br>(50%)</button>
                    <button class='btn-point' onclick='setPoint(3000)'>Point 4<br>(75%)</button>
                    <button class='btn-point' onclick='setPoint(3950)'>Point 5<br>(Max: 100%)</button>
                </div>
            </div>

            <div class='footer-status'>
                🔗 Endpoint API: <code>GET /api/telemetry</code> | <code>POST /api/potentiometer/calibrate</code> | <code>POST /api/oled/message</code>
            </div>
        </div>
    </div>

    <script>
        const arcLength = 251.3; // ความยาวเส้นโค้ง pi * R = 3.1416 * 80
        const gaugeProgress = document.getElementById('gauge-progress');
        const gaugeNeedle = document.getElementById('gauge-needle');
        const valRaw = document.getElementById('val-raw');
        const valCal = document.getElementById('val-cal');
        const gaugeDisplayPct = document.getElementById('gauge-display-pct');
        const slider = document.getElementById('adc-slider');
        const sliderDisplay = document.getElementById('slider-display');

        function updateGauge(rawVal) {
            fetch('/api/telemetry?raw=' + rawVal)
                .then(res => res.json())
                .then(data => {
                    const raw = data.raw;
                    const pct = data.calibrated;
                    valRaw.textContent = raw;
                    valCal.textContent = pct.toFixed(1) + ' ' + data.unit;
                    gaugeDisplayPct.textContent = pct.toFixed(1) + ' ' + data.unit;
                    sliderDisplay.textContent = raw + ' (' + pct.toFixed(1) + '%)';

                    // คำนวณความยาวแถบและมุมเข็ม
                    const ratio = Math.max(0, Math.min(100, pct)) / 100.0;
                    const offset = arcLength * (1 - ratio);
                    gaugeProgress.style.strokeDashoffset = offset;

                    // หมุนเข็ม: 0% อยู่ที่ 0 deg, 100% อยู่ที่ 180 deg
                    const deg = ratio * 180;
                    gaugeNeedle.style.transform = `rotate(${deg}deg)`;
                });
        }

        function updateFromSlider(val) {
            slider.value = val;
            updateGauge(val);
        }

        function setPoint(val) {
            slider.value = val;
            updateFromSlider(val);
        }

        // เริ่มต้นด้วยจุด Point 1 (Min)
        updateGauge(150);
    </script>
</body>
</html>
", "text/html");
});

// Route 1: อ่านข้อมูล Telemetry (รองรับ Query Parameter ?raw=... หรือดึงค่าส่วนกลาง)
app.MapGet("/api/telemetry", (int? raw, CalibrationService cal) =>
{
    if (raw.HasValue)
    {
        currentRawAdc = raw.Value;
    }
    double calibrated = cal.Compute(currentRawAdc);
    return Results.Ok(new
    {
        raw = currentRawAdc,
        calibrated = Math.Round(calibrated, 1),
        unit = cal.Settings.Unit,
        displayMsg = cal.CurrentOledMessage,
        timestamp = DateTime.UtcNow
    });
});

// Route 2: ปรับเทียบเซนเซอร์
app.MapPost("/api/potentiometer/calibrate", (CalibrationSettings newSettings, CalibrationService cal) =>
{
    try
    {
        cal.UpdateSettings(newSettings);
        cal.SetOledMessage("CALIBRATED OK");
        return Results.Ok(new { status = "success", settings = cal.Settings });
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { status = "error", message = ex.Message });
    }
});

// Route 3: สั่งข้อความขึ้นหน้าจอ OLED
app.MapPost("/api/oled/message", (DisplayMessageRequest req, CalibrationService cal) =>
{
    if (string.IsNullOrWhiteSpace(req.Message))
    {
        return Results.BadRequest(new { status = "error", message = "ข้อความต้องไม่ว่างเปล่า" });
    }
    cal.SetOledMessage(req.Message);
    return Results.Ok(new { status = "success", current = cal.CurrentOledMessage });
});

app.Run();
