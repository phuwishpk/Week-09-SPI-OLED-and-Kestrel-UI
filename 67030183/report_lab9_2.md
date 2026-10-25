# รายงานผลการทดลอง Lab 9.2: การพัฒนา API ปรับเทียบเซนเซอร์และการควบคุมหน้าจอด้วย Kestrel Web Server

**รหัสนักศึกษา:** 67030183  
**วิชา:** IoT Engineering & Embedded Systems  
**วันที่ทำการทดลอง:** 25 ตุลาคม 2569  

---

## 1. วัตถุประสงค์
1. พัฒนาเอนจินคำนวณการปรับเทียบเซนเซอร์เชิงเส้นแบบสองจุด (Two-Point Linear Calibration) บน C#
2. สร้าง Minimal API รับค่าปรับเทียบผ่าน `POST /api/potentiometer/calibrate`
3. สร้าง Endpoint สำหรับรับข้อความแสดงผลบนจอ OLED ผ่าน `POST /api/oled/message`
4. ทดสอบและตรวจสอบการทำงานของ HTTP Request/Response ด้วย `curl` และเบราว์เซอร์
5. จัดการข้อผิดพลาด เช่น การหารด้วยศูนย์ (Divide-by-Zero) และการจำกัดขอบเขตข้อมูล (Math.Clamp)

---

## 2. โครงสร้าง REST Minimal API

```text
[ Kestrel Web Server (Port 5117) ]
  ├── GET  /api/telemetry                : อ่านค่า Raw ADC, ค่าที่คำนวณแล้ว, หน่วยวัด, ข้อความ OLED
  ├── POST /api/potentiometer/calibrate  : กำหนดค่า RawMin, RawMax, ScaleMin, ScaleMax, Unit
  └── POST /api/oled/message             : ส่งข้อความไปแสดงผลบนจอ OLED
```

---

## 3. รายละเอียดการทำงานของโค้ด (C# Services)

### 3.1 `Services/CalibrationService.cs`
- กำหนดค่าเริ่มต้น: `RawMin = 150`, `RawMax = 3950`, `ScaleMin = 0.0`, `ScaleMax = 100.0`, `Unit = "%"`
- ป้องกันการหารด้วยศูนย์:
  ```csharp
  if (newSettings.RawMax <= newSettings.RawMin)
  {
      throw new ArgumentException("RawMax ต้องมีค่ามากกว่า RawMin เสมอ!");
  }
  ```
- คำนวณสเกลเชิงเส้นและใช้ `Math.Clamp` ป้องกันค่าล้นช่วง:
  ```csharp
  int clamped = Math.Clamp(rawAdc, _settings.RawMin, _settings.RawMax);
  return ((double)(clamped - _settings.RawMin) / (_settings.RawMax - _settings.RawMin)) 
         * (_settings.ScaleMax - _settings.ScaleMin) + _settings.ScaleMin;
  ```

### 3.2 Endpoints ใน `Program.cs`
- `GET /api/telemetry` : ส่งคืนสถานะล่าสุดในรูปแบบ JSON
- `POST /api/potentiometer/calibrate` : อัปเดตการตั้งค่าสเกล และตอบกลับ `400 Bad Request` เมื่อป้อนค่าผิดพลาด
- `POST /api/oled/message` : อัปเดตข้อความ OLED และตอบกลับ `400 Bad Request` หากข้อความเป็นค่าว่าง

---

## 4. ผลการทดสอบ API

### 4.1 ทดสอบอ่านค่า Telemetry (HTTP GET)
**คำสั่ง:**
```bash
curl -i -X GET http://localhost:5117/api/telemetry
```
**Response:**
```http
HTTP/1.1 200 OK
Content-Type: application/json; charset=utf-8
Server: Kestrel

{"raw":2048,"calibrated":49.9,"unit":"%","displayMsg":"SYSTEM READY","timestamp":"2026-10-25T09:36:20.593687Z"}
```

![ผลการรันคำสั่ง curl GET telemetry](./images/lab9_2_curl_get_telemetry.png)
*ภาพที่ 2.1: ทดสอบคำสั่ง curl GET /api/telemetry บน Terminal*

![ข้อมูล JSON บนเบราว์เซอร์](./images/lab9_2_browser_telemetry.png)
*ภาพที่ 2.2: การเปิดดูข้อมูล /api/telemetry ผ่าน Google Chrome*

![การตรวจสอบ Network Headers ใน Chrome DevTools](./images/lab9_2_network_devtools.png)
*ภาพที่ 2.3: ตรวจสอบ HTTP Response Headers ผ่าน Chrome DevTools (Status: 200 OK, Server: Kestrel)*

---

### 4.2 ทดสอบปรับเทียบเซนเซอร์ใหม่ (HTTP POST)
**คำสั่ง (เปลี่ยนสเกลเป็น 0 - 1000 RPM):**
```bash
curl -i -X POST http://localhost:5117/api/potentiometer/calibrate \
  -H "Content-Type: application/json" \
  -d '{"rawMin": 200, "rawMax": 3800, "scaleMin": 0, "scaleMax": 1000, "unit": "RPM"}'
```
**Response:**
```http
HTTP/1.1 200 OK
Content-Type: application/json; charset=utf-8
Server: Kestrel

{"status":"success","settings":{"rawMin":200,"rawMax":3800,"scaleMin":0,"scaleMax":1000,"unit":"RPM"}}
```

![ผลการรันคำสั่ง curl POST calibrate](./images/lab9_2_curl_post_calibrate.png)
*ภาพที่ 2.4: ทดสอบคำสั่ง curl POST /api/potentiometer/calibrate*

---

### 4.3 ทดสอบส่งข้อความขึ้นจอ OLED (HTTP POST)
**คำสั่ง:**
```bash
curl -i -X POST http://localhost:5117/api/oled/message \
  -H "Content-Type: application/json" \
  -d '{"message":"Hello OLED"}'
```
**Response:**
```http
HTTP/1.1 200 OK
Content-Type: application/json; charset=utf-8
Server: Kestrel

{"status":"success","current":"Hello OLED"}
```

![ผลการรันคำสั่ง curl POST oled message](./images/lab9_2_curl_post_oled_message.png)
*ภาพที่ 2.5: ทดสอบคำสั่ง curl POST /api/oled/message*

---

## 5. การทดสอบกรณีข้อมูลผิดพลาด (Fault Injection)

| กรณีทดสอบ | ข้อมูลที่ส่ง | Status Code | ผลการทำงาน |
| :--- | :--- | :---: | :--- |
| **RawMax น้อยกว่า RawMin** | `{"rawMin": 4000, "rawMax": 1000, ...}` | **`400 Bad Request`** | ตรวจจับด้วย `ArgumentException` เซิร์ฟเวอร์ไม่แครช |
| **ส่งข้อความว่างเปล่า** | `{"message": ""}` | **`400 Bad Request`** | ตรวจจับด้วย `string.IsNullOrWhiteSpace` ตอบกลับแจ้งเตือน |
| **ไม่ระบุ Content-Type** | ตัด `-H "Content-Type: application/json"` | **`415 Unsupported Media Type`** | Kestrel ปฏิเสธ Request ทันทีที่ระดับ Middleware |

---

## 6. สรุปคำถามท้ายการทดลอง

### ข้อ 1: ทำไมการคำนวณสเกลเซนเซอร์จึงควรทำที่ Kestrel Server แทนที่จะทำบน ESP32?
- **ตอบ:** 
  1. **ปรับเปลี่ยนค่าง่าย:** ปรับเปลี่ยนช่วงการวัดและหน่วยวัดได้ทันทีผ่าน API โดยไม่ต้องแฟลชเฟิร์มแวร์ ESP32 ใหม่
  2. **ลดภาระของไมโครคอนโทรลเลอร์:** ให้ ESP32 เน้นการอ่านค่าแอนะล็อกความเร็วสูง ไม่ต้องประมวลผล Floating-point
  3. **ข้อมูลตรงกันทุกระบบ:** มี Server เป็นศูนย์กลางในการคำนวณ ทำให้เว็บหรือแอปพลิเคชันอื่นอ่านค่าได้ตรงกัน

### ข้อ 2: หากไม่มีการตรวจสอบ `RawMax <= RawMin` ในโค้ด จะเกิดปัญหาใด?
- **ตอบ:** ตัวหาร `(RawMax - RawMin)` จะกลายเป็นศูนย์ ทำให้เกิดค่า **`Infinity` หรือ `NaN`** ในตัวแปรชนิด `double` ซึ่งส่งผลให้การแปลง JSON หรือการประมวลผลต่อล้มเหลว และอาจเกิดข้อผิดพลาดระดับ **HTTP 500 Internal Server Error**

### ข้อ 3: ทำไมเมื่อไม่ใส่ Header `Content-Type: application/json` จึงได้รหัส `415 Unsupported Media Type`?
- **ตอบ:** เพราะ ASP.NET Core ใช้ Header `Content-Type` เพื่อเลือกตัวอ่านข้อมูล (Body Formatter) เมื่อไม่ระบุ เซิร์ฟเวอร์จะไม่สามารถแปลงข้อมูล JSON เข้าสู่ Model Class ได้ จึงตอบกลับด้วยรหัส 415 เพื่อปฏิเสธข้อมูลที่ไม่รองรับ
