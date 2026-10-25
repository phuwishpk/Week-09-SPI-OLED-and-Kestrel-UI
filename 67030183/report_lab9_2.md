# รายงานผลการทดลอง Lab 9.2: การพัฒนาเอนจินปรับเทียบเซนเซอร์และ API ควบคุมการแสดงผลบน Kestrel Web Server พร้อมการพิสูจน์หลักฐานเครือข่าย (HTTP Payload Forensics)

**รหัสนักศึกษา:** 67030183  
**วิชา:** IoT Engineering & Embedded Systems  
**วัน/เวลาที่ทำการทดลอง:** 25 ตุลาคม 2569  

---

## 1. วัตถุประสงค์การทดลอง
1. เข้าใจหลักการและสามารถเขียนเอนจินคำนวณการปรับเทียบเซนเซอร์เชิงเส้นแบบสองจุด (**Two-Point Linear Calibration**) ในภาษา C# ได้
2. สามารถสร้าง Minimal API Endpoint สำหรับการรับพารามิเตอร์ Calibrate (`POST /api/potentiometer/calibrate`) ได้
3. สามารถสร้าง Endpoint สำหรับส่งข้อความสั่งการหน้าจอ OLED (`POST /api/oled/message`) ได้
4. สามารถตรวจสอบและตรวจพิสูจน์หลักฐานแพ็กเก็ตเครือข่าย (**HTTP Request/Response Forensics**) ผ่าน `curl` ได้
5. เข้าใจและป้องกันข้อผิดพลาดทางคณิตศาสตร์ (เช่น Divide-by-Zero และ Out-of-Bounds Clamping) ในบริการ IoT

---

## 2. โครงสร้าง REST Minimal API ที่พัฒนา

```text
[ Kestrel Web Server (Port 5000 / 5117) ]
  ├── GET  /api/telemetry                : ดึงค่า Raw ADC, Calibrated Value, Unit, ข้อความ OLED และ Timestamp
  ├── POST /api/potentiometer/calibrate  : ปรับเทียบค่าศูนย์และค่าเต็มสเกล (Zero & Span Calibration)
  └── POST /api/oled/message             : สั่งส่งข้อความ Broadcast ไปแสดงผลยังหน้าจอ OLED
```

---

## 3. การสร้างและพัฒนาโมเดลบริการ (C# Services)

### 3.1 โมเดลและการคำนวณใน `Services/CalibrationService.cs`
- คลาส `CalibrationSettings`:
  ```csharp
  public class CalibrationSettings
  {
      public int RawMin { get; set; } = 150;     // ค่าดิบต่ำสุด (Zero Point)
      public int RawMax { get; set; } = 3950;    // ค่าดิบสูงสุด (Span Point)
      public double ScaleMin { get; set; } = 0.0;
      public double ScaleMax { get; set; } = 100.0;
      public string Unit { get; set; } = "%";
  }
  ```
- มีกลไกป้องกัน **Divide-by-Zero Exception**:
  ```csharp
  if (newSettings.RawMax <= newSettings.RawMin)
  {
      throw new ArgumentException("RawMax ต้องมีค่ามากกว่า RawMin เสมอ!");
  }
  ```
- มีกลไก **Math.Clamp** ในฟังก์ชัน `Compute(rawAdc)` ป้องกันไม่ให้ค่าที่แปลงแล้วล้นสเกล:
  ```csharp
  int clamped = Math.Clamp(rawAdc, _settings.RawMin, _settings.RawMax);
  return ((double)(clamped - _settings.RawMin) / (_settings.RawMax - _settings.RawMin)) 
         * (_settings.ScaleMax - _settings.ScaleMin) + _settings.ScaleMin;
  ```
- ฟังก์ชัน `SetOledMessage` จำกัดความยาวข้อความไม่เกิน 20 อักขระ

### 3.2 โมเดลใน `Services/DisplayMessageRequest.cs`
```csharp
namespace ESP32.Kestrel.Webserver.Services;

public class DisplayMessageRequest
{
    public string Message { get; set; } = string.Empty;
}
```

### 3.3 การผูก Routes ใน `Program.cs`
- ลงทะเบียน Service `builder.Services.AddSingleton<CalibrationService>();`
- Map Endpoints:
  1. `GET /api/telemetry` $\rightarrow$ ส่งคืน JSON สถานะเซนเซอร์
  2. `POST /api/potentiometer/calibrate` $\rightarrow$ ปรับแต่งค่าสเกล พร้อมคืน `400 Bad Request` หากค่าผิดพลาด
  3. `POST /api/oled/message` $\rightarrow$ อัปเดตข้อความหน้าจอ พร้อมคืน `400 Bad Request` หากข้อความเป็นค่าว่าง

---

## 4. ผลการทดสอบเชิงนิติวิทยาศาสตร์เครือข่าย (HTTP Payload Forensics)

### 4.1 การตรวจสอบ Telemetry เริ่มต้น (HTTP GET)
**คำสั่งทดสอบ:**
```bash
curl -i -X GET http://localhost:5117/api/telemetry
```

**ผลลัพธ์ Raw HTTP Response:**
```http
HTTP/1.1 200 OK
Content-Type: application/json; charset=utf-8
Date: Sun, 25 Oct 2026 09:36:20 GMT
Server: Kestrel
Transfer-Encoding: chunked

{"raw":2048,"calibrated":49.9,"unit":"%","displayMsg":"SYSTEM READY","timestamp":"2026-10-25T09:36:20.593687Z"}
```

#### หลักฐานการทำงานจริง (Verification Screenshots):
![ผลการรันคำสั่ง curl GET telemetry ใน Terminal](./images/lab9_2_curl_get_telemetry.png)
*ภาพที่ 2.1: ผลการรันคำสั่ง curl -i -X GET http://localhost:5117/api/telemetry บน Terminal จริง*

![ข้อมูลดิบ JSON บน Web Browser](./images/lab9_2_browser_telemetry.png)
*ภาพที่ 2.2: การเปิดดูข้อมูล JSON บน Google Chrome ที่ URL: http://localhost:5117/api/telemetry*

![หลักฐานการตรวจสอบ Network Headers ใน Chrome DevTools](./images/lab9_2_network_devtools.png)
*ภาพที่ 2.3: การตรวจสอบ HTTP Response Headers ด้วย Chrome DevTools (ยืนยัน Status Code: 200 OK และ Server: Kestrel)*

---

### 4.2 การ Calibrate เซนเซอร์ใหม่ (HTTP POST)
**คำสั่งทดสอบ:**
```bash
curl -i -X POST http://localhost:5117/api/potentiometer/calibrate \
  -H "Content-Type: application/json" \
  -d '{"rawMin": 200, "rawMax": 3800, "scaleMin": 0, "scaleMax": 1000, "unit": "RPM"}'
```

**ผลลัพธ์ Raw HTTP Response:**
```http
HTTP/1.1 200 OK
Content-Type: application/json; charset=utf-8
Date: Sun, 25 Oct 2026 09:36:31 GMT
Server: Kestrel
Transfer-Encoding: chunked

{"status":"success","settings":{"rawMin":200,"rawMax":3800,"scaleMin":0,"scaleMax":1000,"unit":"RPM"}}
```

#### หลักฐานการทำงานจริง (Verification Screenshot):
![ผลการรันคำสั่ง curl POST calibrate ใน Terminal](./images/lab9_2_curl_post_calibrate.png)
*ภาพที่ 2.4: ผลการรันคำสั่ง curl POST ปรับสเกลเซนเซอร์ใหม่เป็น 0 - 1000 RPM บน Terminal จริง*

---

### 4.3 การส่งข้อความสั่งการหน้าจอ OLED (HTTP POST)
**คำสั่งทดสอบ:**
```bash
curl -i -X POST http://localhost:5117/api/oled/message \
  -H "Content-Type: application/json" \
  -d '{"message":"Hello OLED"}'
```

**ผลลัพธ์ Raw HTTP Response:**
```http
HTTP/1.1 200 OK
Content-Type: application/json; charset=utf-8
Date: Sun, 25 Oct 2026 09:36:47 GMT
Server: Kestrel
Transfer-Encoding: chunked

{"status":"success","current":"Hello OLED"}
```

#### หลักฐานการทำงานจริง (Verification Screenshot):
![ผลการรันคำสั่ง curl POST oled message ใน Terminal](./images/lab9_2_curl_post_oled_message.png)
*ภาพที่ 2.5: ผลการรันคำสั่ง curl POST ส่งข้อความ "Hello OLED" ขึ้นหน้าจอสำเร็จ บน Terminal จริง*

---

## 5. การทดสอบฉีดข้อผิดพลาด (Fault Injection & Vulnerability Probe)

| กรณีทดสอบ (Test Case) | ข้อมูล Payload ที่ส่ง | รหัสสถานะ HTTP ที่ได้รับ | ผลการทำงานของ Kestrel Server |
| :--- | :--- | :---: | :--- |
| **1. ป้อนค่าสเกลผิดตรรกะ (Span < Zero)** | `{"rawMin": 4000, "rawMax": 1000, ...}` | **`400 Bad Request`** | ดักจับด้วย `ArgumentException` ตอบกลับว่า *"RawMax ต้องมีค่ามากกว่า RawMin เสมอ!"* เซิร์ฟเวอร์ไม่ล่ม (No Crash) |
| **2. ส่งข้อความว่างเปล่า (Empty String)** | `{"message": ""}` | **`400 Bad Request`** | ดักจับด้วย `string.IsNullOrWhiteSpace` ตอบกลับข้อความแจ้งเตือน เซิร์ฟเวอร์ทำงานต่อเนื่องได้ปกติ |
| **3. ลืมใส่ Content-Type Header** | ตัด `-H "Content-Type: application/json"` | **`415 Unsupported Media Type`** | Kestrel ปฏิเสธ Request ทันทีที่ระดับ Middleware ก่อนเข้าสู่ฟังก์ชันประมวลผล |

---

## 6. คำถามท้ายการทดลอง Lab 9.2

### ข้อ 1: เหตุใดการคำนวณสเกลเซนเซอร์จึงควรทำที่ฝั่ง Kestrel Server แทนที่จะคำนวณบนไมโครคอนโทรลเลอร์ ESP32 ตั้งแต่แรก?
- **ตอบ:** 
  1. **ความยืดหยุ่นในการปรับแต่ง (Dynamic Re-calibration):** สามารถเปลี่ยนช่วงการวัด (Range), หน่วยวัด (Unit) หรือจุดศูนย์/สแปนได้แบบเรียลไทม์ผ่าน REST API โดยไม่ต้องแฟลชเฟิร์มแวร์ลงบอร์ด ESP32 ใหม่
  2. **ลดภาระการประมวลผลของ MCU (Resource Offloading):** ให้ ESP32 ทำหน้าที่หลักคืออ่านค่าแอนะล็อกความเร็วสูงและส่งข้อมูลดิบ (Raw ADC) ออกไปเท่านั้น ไม่ต้องคำนวณเลขทศนิยม (Floating Point Math) ที่กินทรัพยากร
  3. **Single Source of Truth:** ระบบส่วนกลาง (Server/Cloud) เป็นผู้ควบคุมสเกลและการแปลงค่า ทำให้ไคลเอนต์ทุกตัว (เช่น Web Dashboard, Mobile App, ฐานข้อมูล) ได้รับค่าที่มีความสอดคล้องกันอย่างแท้จริง

### ข้อ 2: จากการทำ HTTP Forensics หากไม่มีการตรวจสอบเงื่อนไข `RawMax <= RawMin` ในโค้ด จะเกิด Exception ชนิดใดขึ้นในภาษา C# และส่งผลต่อการทำงานของเซิร์ฟเวอร์อย่างไร?
- **ตอบ:** หากไม่มีการตรวจสอบเงื่อนไขนี้ ในฟังก์ชันคำนวณ `Compute()` ตัวหารจะเป็น `(_settings.RawMax - _settings.RawMin)` ซึ่งถ้าค่าทั้งสองเท่ากัน ตัวหารจะมีค่าเป็น `0` ทำให้เกิดผลลัพธ์เป็น **`Infinity` หรือ `NaN`** สำหรับเลขทศนิยมแบบ `double` (หรือเกิด `DivideByZeroException` ในกรณีจำนวนเต็ม) และหากมีการนำค่านั้นไปประมวลผลต่อ อาจทำให้เกิดข้อผิดพลาดรุนแรงระดับ **HTTP 500 Internal Server Error** หรือส่งผลให้การแปลง JSON ล้มเหลว ส่งผลเสียต่อความเสถียรของระบบ

### ข้อ 3: อธิบายสาเหตุทางเทคนิคว่าทำไมคำขอ HTTP POST ที่ไม่มี Header `Content-Type: application/json` จึงถูกปฏิเสธด้วยรหัสสถานะ `415 Unsupported Media Type`?
- **ตอบ:** เพราะโปรโตคอล HTTP กำหนดให้ Header `Content-Type` เป็นตัวระบุชนิดของข้อมูล (MIME Type) ที่แนบมาในส่วน Body เมื่อ Kestrel Minimal API มีการผูกโมเดลพารามิเตอร์ที่เป็น Class (เช่น `CalibrationSettings` หรือ `DisplayMessageRequest`) ตัวจัดการ Request Pipeline ของ ASP.NET Core จะมองหาตัวอ่านที่รองรับ `application/json` หาก Client ไม่ส่ง Header นี้มา Kestrel จะไม่สามารถทราบฟอร์แมตของข้อมูลและไม่ยอมทำการ Deserialize ข้อมูล จึงตัดการทำงานและตอบกลับด้วยรหัส **`415 Unsupported Media Type`** เพื่อความปลอดภัย
