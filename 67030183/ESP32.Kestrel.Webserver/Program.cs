using ESP32.Kestrel.Webserver.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<CalibrationService>();
var app = builder.Build();

// Route หน้าแรก: แสดงแดชบอร์ดแนะนำการใช้งานและลิงก์ไปยัง Telemetry (ป้องกัน 404)
app.MapGet("/", () => Results.Content(@"
<!DOCTYPE html>
<html lang='th'>
<head>
    <meta charset='utf-8'>
    <title>ESP32 Kestrel Web Server</title>
    <style>
        body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background: #0f172a; color: #f8fafc; text-align: center; padding: 50px; }
        .card { background: #1e293b; max-width: 600px; margin: auto; padding: 30px; border-radius: 12px; box-shadow: 0 4px 20px rgba(0,0,0,0.5); }
        h1 { color: #38bdf8; margin-bottom: 8px; }
        p { color: #cbd5e1; line-height: 1.6; }
        .btn { display: inline-block; background: #0284c7; color: white; padding: 12px 24px; border-radius: 8px; margin: 15px 0; text-decoration: none; font-weight: bold; }
        .btn:hover { background: #0369a1; }
        .info { background: #0f172a; padding: 12px; border-radius: 8px; text-align: left; font-size: 0.9em; color: #94a3b8; }
        code { color: #f43f5e; background: #1e293b; padding: 2px 6px; border-radius: 4px; }
    </style>
</head>
<body>
    <div class='card'>
        <h1>🚀 ESP32 Kestrel Web Server (.NET 10)</h1>
        <p>สถานะ: <strong>ออนไลน์สมบูรณ์ (Running)</strong></p>
        <a class='btn' href='/api/telemetry' target='_blank'>📊 เปิดดูข้อมูล Telemetry (/api/telemetry)</a>
        <div class='info'>
            <strong>📌 วิธีทำแลปและแคปเจอร์หลักฐาน:</strong><br>
            1. กดปุ่มด้านบน หรือเข้า URL: <code>/api/telemetry</code><br>
            2. กด <strong>F12</strong> (หรือคลิกขวา -> ตรวจสอบ) เลือกแท็บ <strong>Network</strong><br>
            3. กด <strong>F5</strong> รีเฟรช เพื่อแคปเจอร์ Raw HTTP Response Headers (Server: Kestrel)
        </div>
    </div>
</body>
</html>", "text/html"));

// Route 1: อ่านข้อมูล Telemetry
app.MapGet("/api/telemetry", (CalibrationService cal) =>
{
    int simulatedRaw = 2048; // หรือดึงจาก Serial Stream
    double calibrated = cal.Compute(simulatedRaw);
    return Results.Ok(new
    {
        raw = simulatedRaw,
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
